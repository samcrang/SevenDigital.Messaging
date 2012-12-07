﻿using System;
using System.Threading;
using Moq;
using NUnit.Framework;
using SevenDigital.Messaging.Dispatch;
using SevenDigital.Messaging.Integration.Tests.Handlers;
using SevenDigital.Messaging.Integration.Tests.Messages;
using StructureMap;

namespace SevenDigital.Messaging.Integration.Tests
{
	[TestFixture]
	public class EventHookTests
	{
		INodeFactory node_factory;

		protected TimeSpan LongInterval { get { return TimeSpan.FromSeconds(30); } }
		protected TimeSpan ShortInterval { get { return TimeSpan.FromSeconds(3); } }

		Mock<IEventHook> mock_event_hook;
	    private ISenderNode senderNode;

		[TestFixtureSetUp]
		public void StartMessaging()
		{
			Helper.SetupTestMessaging();
		}


		[SetUp]
		public void SetUp()
		{
			mock_event_hook = new Mock<IEventHook>();

			ObjectFactory.Configure(map=> map.For<IEventHook>().Use(mock_event_hook.Object));

			node_factory = ObjectFactory.GetInstance<INodeFactory>();
            senderNode = ObjectFactory.GetInstance<ISenderNode>();
		}
        
		[Test]
		public void Sender_should_trigger_event_hook_with_message_when_sending()
		{
			var message = new GreenMessage();
			
			senderNode.SendMessage(message);

			mock_event_hook.Verify(h => h.MessageSent(message, It.IsAny<string>(), It.IsAny<string>()));
		}
		
		[Test]
		public void Should_trigger_event_hook_with_message_when_receiving_a_message ()
		{
			var message = new GreenMessage();
			using (var receiverNode = node_factory.Listen())
			{

				receiverNode.Handle<IColourMessage>().With<ColourMessageHandler>();
				senderNode.SendMessage(message);

				Assert.That(ColourMessageHandler.AutoResetEvent.WaitOne(LongInterval));

			} 
			ObjectFactory.GetInstance<IDestinationPoller>().Stop(); // Moq isn't thread safe!
			lock (mock_event_hook)
			{
				mock_event_hook.Verify(h => h.MessageReceived(It.Is<IColourMessage>(im => im.CorrelationId == message.CorrelationId), It.IsAny<string>()));
			}
		}

		[Test]
		public void Should_trigger_event_hook_with_message_when_receiving_a_message_from_a_base_type ()
		{
			using (var receiverNode = node_factory.Listen())
			{
				var message = new GreenMessage();

				receiverNode.Handle<IMessage>().With<GenericHandler>();
				senderNode.SendMessage(message);

				GenericHandler.AutoResetEvent.WaitOne(LongInterval);
				
				mock_event_hook.Verify(h=>h.MessageReceived(It.Is<IMessage>(im => im.CorrelationId == message.CorrelationId), It.IsAny<string>()));
			}
		}

		[Test]
		public void Every_handler_should_trigger_event_hook ()
		{
			using (var receiverNode = node_factory.Listen())
			{
				var message = new GreenMessage();

				receiverNode.Handle<IColourMessage>().With<ColourMessageHandler>();
				receiverNode.Handle<IColourMessage>().With<AnotherColourMessageHandler>();
				senderNode.SendMessage(message);

				ColourMessageHandler.AutoResetEvent.WaitOne(LongInterval);
				AnotherColourMessageHandler.AutoResetEvent.WaitOne(LongInterval);

				mock_event_hook.Verify(h=>h.MessageReceived(It.Is<IMessage>(im=> im.CorrelationId == message.CorrelationId), It.IsAny<string>()),
					Times.Exactly(2));
			}
		}

		[TestFixtureTearDown]
		public void Stop() { new MessagingConfiguration().Shutdown(); }
	}

	public class GenericHandler:IHandle<IMessage>
	{
        public static AutoResetEvent AutoResetEvent = new AutoResetEvent(false);

        public void Handle(IMessage message)
        {
            AutoResetEvent.Set();
        }
	}
}
