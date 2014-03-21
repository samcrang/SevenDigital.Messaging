using System;
using NUnit.Framework;
using SevenDigital.Messaging.EventHooks;
using SevenDigital.Messaging.Integration.Tests._Helpers.Handlers;
using SevenDigital.Messaging.Integration.Tests._Helpers.Messages;

namespace SevenDigital.Messaging.Integration.Tests.MessageSending.BaseCases
{
	public abstract class SendingAndReceivingBase
	{
		IReceiver _receiver;
		private ISenderNode _sender;

		protected TimeSpan LongInterval { get { return TimeSpan.FromSeconds(20); } }
		protected TimeSpan ShortInterval { get { return TimeSpan.FromSeconds(2); } }

		/// <summary>
		/// Concretes should setup their specific messaging configs here
		/// </summary>
		public abstract void ConfigureMessaging();

		[SetUp]
		public void SetUp()
		{
			ConfigureMessaging();

			_receiver = MessagingSystem.Receiver();
			_sender = MessagingSystem.Sender();

			MessagingSystem.Events.ClearEventHooks();
			MessagingSystem.Events.AddEventHook<ConsoleEventHook>();
		}

		[TearDown]
		public void TearDown()
		{
			MessagingSystem.Control.Shutdown();
		}

		[Test]
		public void Handler_should_react_for_all_message_types_it_is_handling()
		{
			using (_receiver.Listen(_=>_
				.Handle<IColourMessage>().With<AllColourMessagesHandler>()
				.Handle<ITwoColoursMessage>().With<AllColourMessagesHandler>()
				))
			{
				AllColourMessagesHandler.Prepare();
	
				_sender.SendMessage(new RedMessage());
				var signal1 = AllColourMessagesHandler.AutoResetEventForColourMessage.WaitOne(LongInterval);
				_sender.SendMessage(new GreenWhiteMessage());

				var signal2 = AllColourMessagesHandler.AutoResetEventForTwoColourMessage.WaitOne(LongInterval);
				Assert.That(signal1, Is.True);
				Assert.That(signal2, Is.True);
			}
		}

		[Test]
		public void Handler_should_react_when_a_registered_message_type_is_received_for_unnamed_endpoint()
		{
			using (_receiver.Listen(_=>_
				.Handle<IColourMessage>().With<ColourMessageHandler>()))
			{
				_sender.SendMessage(new RedMessage());

				var colourSignal = ColourMessageHandler.AutoResetEvent.WaitOne(LongInterval);
				Assert.That(colourSignal, Is.True);
			}
		}

		[Test]
		public void Handler_should_react_when_a_registered_message_type_is_received_for_named_endpoint()
		{
			using (_receiver.TakeFrom(
				new Endpoint("Test_listener_registered-message-endpoint"),
				_=>_.Handle<IColourMessage>().With<ColourMessageHandler>()
				))
			{
				_sender.SendMessage(new RedMessage());
				var colourSignal = ColourMessageHandler.AutoResetEvent.WaitOne(LongInterval);

				Assert.That(colourSignal, Is.True);
			}
		}

		[Test]
		public void Handler_should_get_message_with_proper_correlation_id()
		{
			using (_receiver.Listen(_=>_
				.Handle<ITwoColoursMessage>().With<TwoColourMessageHandler>()))
			{
				var message = new GreenWhiteMessage();
				TwoColourMessageHandler.Prepare();

				_sender.SendMessage(message);
				var colourSignal = TwoColourMessageHandler.AutoResetEvent.WaitOne(ShortInterval);

				Assert.That(colourSignal, Is.True, "Did not get message!");
				Assert.That(TwoColourMessageHandler.ReceivedCount, Is.EqualTo(1), "Got a wrong number of messages");
				Assert.That(TwoColourMessageHandler.ReceivedMessage.CorrelationId, Is.EqualTo(message.CorrelationId));
			}
		}

		[Test]
		public void Handler_should_not_react_when_an_unregistered_message_type_is_received_for_unnamed_endpoint()
		{
			using (_receiver.Listen(_ => _.Handle<IColourMessage>().With<ColourMessageHandler>()))
			{
				_sender.SendMessage(new JokerMessage());
				var colourSignal = ColourMessageHandler.AutoResetEvent.WaitOne(ShortInterval);

				Assert.That(colourSignal, Is.False);
			}
		}

		[Test]
		public void Handler_should_not_react_when_an_unregistered_message_type_is_received_for_named_endpoint()
		{
			ColourMessageHandler.AutoResetEvent.Reset();
			using (_receiver.TakeFrom(
				new Endpoint("Test_listener_unregistered-message-endpoint"),
				_ => _.Handle<IColourMessage>().With<ColourMessageHandler>()))
			{
				_sender.SendMessage(new JokerMessage());
				var colourSignal = ColourMessageHandler.AutoResetEvent.WaitOne(ShortInterval);

				Assert.That(colourSignal, Is.False);
			}
		}

		[Test]
		public void Only_one_handler_should_fire_when_competing_for_an_endpoint()
		{
			using (_receiver.TakeFrom("Test_listener_shared-endpoint", _ => _.Handle<IComicBookCharacterMessage>().With<SuperHeroMessageHandler>()))
			using (_receiver.TakeFrom("Test_listener_shared-endpoint", _ => _.Handle<IComicBookCharacterMessage>().With<VillainMessageHandler>()))
			{
				_sender.SendMessage(new BatmanMessage());
				var superheroSignal = SuperHeroMessageHandler.AutoResetEvent.WaitOne(ShortInterval);
				var villainSignal = VillainMessageHandler.AutoResetEvent.WaitOne(ShortInterval);

				Assert.That(superheroSignal || villainSignal, Is.True);
				Assert.That(superheroSignal && villainSignal, Is.False);
			}
		}

		[Test]
		public void Should_not_send_to_queue_if_the_routing_key_does_not_match()
		{
			using (
				_receiver.TakeFrom("Test_listener_shared-endpoint", "routingKey",
				_ => _.Handle<IComicBookCharacterMessage>().With<SuperHeroMessageHandler>())
				)
			{
				_sender.SendMessage(new BatmanMessage(),"foo");
				var superheroSignal = SuperHeroMessageHandler.AutoResetEvent.WaitOne(ShortInterval);
				Assert.That(superheroSignal, Is.False);
				
			}
		}

		[Test]
		public void Should_send_to_queue_if_the_routing_key_is_a_special_match()
		{
			using (
				_receiver.TakeFrom("Test_listener_shared-endpoint", "#",
				_ => _.Handle<IComicBookCharacterMessage>().With<SuperHeroMessageHandler>())
				)
			{
				_sender.SendMessage(new BatmanMessage(), "foo");
				var superheroSignal = SuperHeroMessageHandler.AutoResetEvent.WaitOne(ShortInterval);
				Assert.That(superheroSignal, Is.True);
			}
		}

		[Test]
		public void Should_send_to_queue_if_the_routing_key_matches()
		{
			using (
				_receiver.TakeFrom("Test_listener_shared-endpoint", "routingKey",
				_ => _.Handle<IComicBookCharacterMessage>().With<SuperHeroMessageHandler>())
				)
			{
				_sender.SendMessage(new BatmanMessage(), "routingKey");
				var superheroSignal = SuperHeroMessageHandler.AutoResetEvent.WaitOne(ShortInterval);
				Assert.That(superheroSignal, Is.True);
			}
		}

		[Test]
		public void Should_use_all_registered_handlers_when_a_message_is_received()
		{
			using (_receiver.Listen(_ => _
				.Handle<IComicBookCharacterMessage>().With<SuperHeroMessageHandler>()
				.Handle<IComicBookCharacterMessage>().With<VillainMessageHandler>()
				))
			{
				_sender.SendMessage(new JokerMessage());
				var superheroSignal = SuperHeroMessageHandler.AutoResetEvent.WaitOne(LongInterval);
				var villainSignal = VillainMessageHandler.AutoResetEvent.WaitOne(LongInterval);

				Assert.That(superheroSignal, Is.True);
				Assert.That(villainSignal, Is.True);
			}
		}

		[Test]
		public void Handler_which_sends_a_new_message_should_get_that_message_handled()
		{
			using (_receiver.Listen(_ => _
				.Handle<IColourMessage>().With<ChainHandler>()
				.Handle<IComicBookCharacterMessage>().With<VillainMessageHandler>()
				))
			{
				_sender.SendMessage(new GreenMessage());

				var villainSignal = VillainMessageHandler.AutoResetEvent.WaitOne(LongInterval);

				Assert.That(villainSignal, Is.True);
			}
		}

		[Test]
		public void Should_not_send_to_queue_if_the_routing_key_does_not_match_for_listen()
		{
			using (
				_receiver.Listen("routingKey", _ => _.Handle<IComicBookCharacterMessage>().With<SuperHeroMessageHandler>())
				)
			{
				_sender.SendMessage(new BatmanMessage(), "foo");
				var superheroSignal = SuperHeroMessageHandler.AutoResetEvent.WaitOne(ShortInterval);
				Assert.That(superheroSignal, Is.False);

			}
		}

		[Test]
		public void Should_send_to_queue_if_the_routing_key_is_a_special_match_for_listen()
		{
			using (
				_receiver.Listen("#", _ => _.Handle<IComicBookCharacterMessage>().With<SuperHeroMessageHandler>())
				)
			{
				_sender.SendMessage(new BatmanMessage(), "foo");
				var superheroSignal = SuperHeroMessageHandler.AutoResetEvent.WaitOne(ShortInterval);
				Assert.That(superheroSignal, Is.True);
			}
		}

		[Test]
		public void Should_send_to_queue_if_the_routing_key_matches_for_listen()
		{
			using (
				_receiver.Listen("routingKey", _ => _.Handle<IComicBookCharacterMessage>().With<SuperHeroMessageHandler>())
				)
			{
				_sender.SendMessage(new BatmanMessage(), "routingKey");
				var superheroSignal = SuperHeroMessageHandler.AutoResetEvent.WaitOne(ShortInterval);
				Assert.That(superheroSignal, Is.True);
			}
		}
	}
}