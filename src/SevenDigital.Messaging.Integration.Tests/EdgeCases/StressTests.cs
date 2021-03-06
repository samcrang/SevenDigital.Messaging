﻿using System;
using System.Threading;
using NUnit.Framework;
using SevenDigital.Messaging.Integration.Tests._Helpers;

namespace SevenDigital.Messaging.Integration.Tests.EdgeCases
{
	[TestFixture]
	public class StressTests
	{
		IReceiver _receiver;
		ISenderNode _sender;

		[SetUp]
		public void SetUp()
		{
			Helper.SetupTestMessaging();
			MessagingSystem.Events.ClearEventHooks();
			_receiver = MessagingSystem.Receiver();
			_sender = MessagingSystem.Sender();
		}

		[Test, Explicit("Slow test")]
		public void should_be_able_to_send_and_receive_1000_messages_per_minute()
		{
			using (_receiver.TakeFrom("test_listener_ping-pong-endpoint", _ => _
				.Handle<IPing>().With<PingHandler>()
				.Handle<IPong>().With<PongHandler>()
				))
			{
				_sender.SendMessage(new PingMessage());

				var result = PongHandler.Trigger.WaitOne(TimeSpan.FromSeconds(60));
				Assert.True(result, "Only got " + (PongHandler.Count * 2) + " in a minute");
			}
		}
	}

	#region Type junk
	public class PongHandler : IHandle<IPong>
	{
		public static AutoResetEvent Trigger = new AutoResetEvent(false);
		readonly ISenderNode sender;
		public static int Count = 0;

		public PongHandler(ISenderNode sender)
		{
			this.sender = sender;
		}

		public void Handle(IPong message)
		{
			Interlocked.Increment(ref Count);

			if (Count > 500)
			{
				Console.WriteLine("OK!");
				Trigger.Set();
			}
			else
			{
				sender.SendMessage(new PingMessage());
			}
		}
	}

	public class PingMessage : IPing
	{
		public PingMessage()
		{
			CorrelationId = Guid.NewGuid();
		}
		public Guid CorrelationId { get; set; }
	}

	public class PingHandler : IHandle<IPing>
	{
		readonly ISenderNode sender;

		public PingHandler(ISenderNode sender)
		{
			this.sender = sender;
		}

		public void Handle(IPing message)
		{
			sender.SendMessage(new PongMessage());
		}
	}

	public class PongMessage : IPong
	{
		public PongMessage()
		{
			CorrelationId = Guid.NewGuid();
		}
		public Guid CorrelationId { get; set; }
	}

	public interface IPong : IMessage
	{
	}

	public interface IPing : IMessage
	{
	}
	#endregion
}
