﻿using System.Linq;
using NUnit.Framework;
using SevenDigital.Messaging.Loopback;

namespace SevenDigital.Messaging.Unit.Tests.LoopbackMessaging
{
	[TestFixture]
	public class LoopbackNodeFactoryTests
	{
		readonly ILoopbackBinding subject = new LoopbackBinding();

		[Test]
		public void Should_return_empty_list_if_no_listeners_are_registered_for_type()
		{
			var listeners = subject.ForMessage<RandomType>();

			Assert.That(listeners, Is.Not.Null);
			Assert.That(listeners.Count(), Is.EqualTo(0));
		}
	}

	public class RandomType
	{

	}
}