﻿using System;
using System.Threading;
using NUnit.Framework;
using SevenDigital.Messaging.EventHooks;
using SevenDigital.Messaging.Integration.Tests._Helpers;
using SevenDigital.Messaging.Integration.Tests._Helpers.Messages;
using SevenDigital.Messaging.MessageReceiving;

namespace SevenDigital.Messaging.Integration.Tests.Api
{
	[TestFixture]
	public class UnregisteringHandlersTests
	{
		IReceiver _receiver;
		private ISenderNode _senderNode;

		protected TimeSpan LongInterval { get { return TimeSpan.FromSeconds(20); } }
		protected TimeSpan ShortInterval { get { return TimeSpan.FromSeconds(3); } }

		[SetUp]
		public void SetUp()
		{
			Helper.SetupTestMessaging();
			MessagingSystem.Events.AddEventHook<ConsoleEventHook>();
			_receiver = MessagingSystem.Receiver();
			_senderNode = MessagingSystem.Sender();
		}

		[Test, Ignore("This test is flaky at the moment")]
		public void can_deregister_a_handler_causing_no_further_messages_to_be_processed()
		{
			UnregisterSample.handledTimes = 0;
			using (var receiverNode = _receiver.Listen(_=>_.Handle<IColourMessage>().With<UnregisterSample>()))
			{
				_senderNode.SendMessage(new RedMessage());

				Thread.Sleep(250);
				receiverNode.Unregister<UnregisterSample>();
				Thread.Sleep(50);
				_senderNode.SendMessage(new RedMessage());

				Thread.Sleep(250);
				Assert.That(UnregisterSample.handledTimes, Is.EqualTo(1));

				receiverNode.Register(new Binding().Handle<IColourMessage>().With<UnregisterSample>());
				_senderNode.SendMessage(new RedMessage());
				Thread.Sleep(500);
				Assert.That(UnregisterSample.handledTimes, Is.EqualTo(3));
			}
		}

		[TearDown]
		public void Stop() { MessagingSystem.Control.Shutdown(); }


		public class UnregisterSample : IHandle<IColourMessage>
		{
			public static int handledTimes = 0;

			public void Handle(IColourMessage message)
			{
				Interlocked.Increment(ref handledTimes);
			}
		}

	}
}