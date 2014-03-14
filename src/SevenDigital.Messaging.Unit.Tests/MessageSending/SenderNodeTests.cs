﻿using System;
using System.Linq;
using System.Text;
using DispatchSharp;
using DispatchSharp.QueueTypes;
using DispatchSharp.WorkerPools;
using NSubstitute;
using NUnit.Framework;
using SevenDigital.Messaging.Base;
using SevenDigital.Messaging.Base.Serialisation;
using SevenDigital.Messaging.Infrastructure;
using SevenDigital.Messaging.MessageReceiving;
using SevenDigital.Messaging.MessageSending;
using StructureMap;

namespace SevenDigital.Messaging.Unit.Tests.MessageSending
{
	[TestFixture]
	public class SenderNodeTests
	{
		private const string _messageContents = "V2=SevenDigital.Messaging.IMessage||{}";
		ISenderNode _subject;
		IMessagingBase _messagingBase;
		IDispatcherFactory _dispatcherFactory;
		ISleepWrapper _sleeper;
		IDispatch<byte[]> _dispatcher;
		IEventHook _eventHook1, _eventHook2;
		IOutgoingQueueFactory _queueFactory;

		[SetUp]
		public void setup()
		{
			_messagingBase = Substitute.For<IMessagingBase>();
			_sleeper = Substitute.For<ISleepWrapper>();
			_dispatcher = Substitute.For<IDispatch<byte[]>>();
			_dispatcherFactory = Substitute.For<IDispatcherFactory>();
			_dispatcherFactory.Create(Arg.Any<IWorkQueue<byte[]>>(), Arg.Any<IWorkerPool<byte[]>>()).Returns(_dispatcher);

			_queueFactory = Substitute.For<IOutgoingQueueFactory>();

			_eventHook1 = Substitute.For<IEventHook>();
			_eventHook2 = Substitute.For<IEventHook>();
			ObjectFactory.Configure(map => {
				map.For<IEventHook>().Use(_eventHook1);
				map.For<IEventHook>().Use(_eventHook2);
			});

			_subject = new SenderNode(_messagingBase, _dispatcherFactory, _sleeper,_queueFactory);
		}

		[TearDown]
		public void teardown()
		{
			_subject.Dispose();
		}

		[Test]
		public void creating_a_sender_node_should_create_a_single_threaded_dispatcher ()
		{
			_dispatcher.Received().SetMaximumInflight(1);
		}

		[Test]
		public void sender_uses_a_persistent_queue ()
		{
			_dispatcherFactory.Received().Create(
				Arg.Any<PersistentWorkQueue>(), // <-- testing this one
				Arg.Any<IWorkerPool<byte[]>>()
				);
		}

		[Test]
		public void dispatcher_work_item_is_SendWaitingMessage ()
		{
			_dispatcher.Received().AddConsumer(((SenderNode)_subject).SendWaitingMessage);
		}

		[Test]
		public void send_waiting_message_fires_event_hooks ()
		{
			var msg = new TestMessage();
			((SenderNode)_subject).SendMessage(msg);

			_eventHook1.Received().MessageSent(msg);
			_eventHook2.Received().MessageSent(msg);
		}

		[Test]
		public void a_failing_event_hook_does_not_stop_other_hooks_being_fired ()
		{
			var msg = new TestMessage();
			_eventHook1.When(m=>m.MessageSent(Arg.Any<IMessage>())).Do(c => { throw new Exception("test exception"); });

			((SenderNode)_subject).SendMessage(msg);

			_eventHook1.Received().MessageSent(msg);
			_eventHook2.Received().MessageSent(msg);
		}

		[Test]
		public void a_failing_event_hook_does_not_prevent_a_message_being_sent ()
		{
			_eventHook1.When(m=>m.MessageSent(Arg.Any<IMessage>())).Do(c => { throw new Exception("test exception"); });

			var msg = Encoding.UTF8.GetBytes(_messageContents);

			((SenderNode)_subject).SendWaitingMessage(msg);
			
			_messagingBase.Received().SendPrepared(Arg.Is<IPreparedMessage>(m=>m.ToBytes().SequenceEqual(msg)));
		}

		[Test]
		public void if_messaging_base_fails_to_send_then_the_sender_sleeps_and_requeues_the_message ()
		{
			bool finished = false, cancelled = false;

			((SenderNode)_subject).SendingExceptions(this,
				new ExceptionEventArgs<byte[]>
				{
					SourceException = new Exception("test exception"),
					WorkItem = new WorkQueueItem<byte[]>(null, o => { finished = true; }, o => { cancelled = true; })
				});

			_sleeper.Received().SleepMore();
			Assert.That(finished, Is.False);
			Assert.That(cancelled, Is.True);
		}

		[Test]
		public void sleeper_is_reset_after_successful_message_sending ()
		{
			var msg = Encoding.UTF8.GetBytes(_messageContents);
			((SenderNode)_subject).SendWaitingMessage(msg);

			_sleeper.Received().Reset();
			_sleeper.DidNotReceive().SleepMore();
			_dispatcher.DidNotReceive().AddWork(msg);
		}

		[Test]
		public void send_waiting_message_sends_message_object_through_messaging_base ()
		{
			var msg = Encoding.UTF8.GetBytes(_messageContents);
			((SenderNode)_subject).SendWaitingMessage(msg);

			_messagingBase.Received().SendPrepared(Arg.Is<IPreparedMessage>(m=>m.ToBytes().SequenceEqual(msg)));
		}

		[Test]
		public void disposing_of_the_sender_node_stops_the_dispatcher ()
		{
			_subject.Dispose();
			_dispatcher.Received().WaitForEmptyQueueAndStop(Arg.Any<TimeSpan>());
		}
		
		public class TestMessage : IMessage { public Guid CorrelationId { get; set; } }
	}

}