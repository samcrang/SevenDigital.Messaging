using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using SevenDigital.Messaging.MessageReceiving;
using StructureMap;

namespace SevenDigital.Messaging.Loopback
{
	/// <summary>
	/// Node factory for loopback.
	/// You don't need to create this yourself, use `Messaging.Receiver()` in loopback mode
	/// </summary>
	public class LoopbackReceiver : IReceiver, ILoopbackReceiver
	{
		readonly ILoopbackBinding listenerBindings;
		readonly ConcurrentBag<string> capturedEndpoints;

		/// <summary>
		/// Create a loopback factory.
		/// </summary>
		public LoopbackReceiver(ILoopbackBinding bindings)
		{
			listenerBindings = bindings;//new Dictionary<Type, ConcurrentBag<Type>>();
			capturedEndpoints = new ConcurrentBag<string>();
		}

		/// <summary>
		/// Map handlers to a listener on a named endpoint.
		/// All other listeners on this endpoint will compete for messages
		/// (i.e. only one listener will get a given message)
		/// </summary>
		public IReceiverNode TakeFrom(Endpoint endpoint, Action<IMessageBinding> bindings)
		{
			return TakeFrom(endpoint, string.Empty, bindings);
		}

		/// <summary>
		/// Map handlers to a listener on a named endpoint.
		/// All other listeners on this endpoint will compete for messages
		/// (i.e. only one listener will get a given message)
		/// </summary>
		public IReceiverNode TakeFrom(Endpoint endpoint, string routingKey, Action<IMessageBinding> bindings)
		{
			// In the real version, agents compete for incoming messages.
			// In this test version, we only really bind the first listener for a given endpoint -- roughly the same effect!
			if (capturedEndpoints.Contains(endpoint.ToString())) return new DummyReceiver();

			capturedEndpoints.Add(endpoint.ToString());
			return Listen(routingKey, bindings);
		}

		/// <summary>
		/// Map handlers to a listener on a unique endpoint.
		/// All listeners mapped this way will receive all messages.
		/// </summary>
		public IReceiverNode Listen(Action<IMessageBinding> bindings)
		{
			return Listen(string.Empty, bindings);
		}

		/// <summary>
		/// Map handlers to a listener on a unique endpoint.
		/// All listeners mapped this way will receive all messages.
		/// </summary>
		public IReceiverNode Listen(string routingKey, Action<IMessageBinding> bindings)
		{
			var node = new LoopbackReceiverNode(this, routingKey);

			var binding = new Binding();
			if (bindings != null) bindings(binding);
			node.Register(binding);

			return node;
		}

		/// <summary>
		/// Bind a message to a handler
		/// </summary>
		public void Bind(Type msg, string routingKey, Type handler)
		{
			var key = new TypeRoutingKeyPair(msg, routingKey);
			if (!listenerBindings.IsMessageRegistered(key))
				listenerBindings.AddMessageType(key);

			if (listenerBindings[key].Contains(handler)) return;

			listenerBindings[key].Add(handler);
		}

		/// <summary>
		/// Send a message to appropriate handlers
		/// </summary>
		public void Send<T>(T message, string routingKey) where T : IMessage
		{
			var hooks = ObjectFactory.GetAllInstances<IEventHook>();
			foreach (var hook in hooks)
			{
				hook.MessageSent(message);
			}

			FireCooperativeListeners(message, routingKey);
		}

		void FireCooperativeListeners<T>(T message, string routingKey) where T : IMessage
		{
			var msg = message.GetType();

			var matches = listenerBindings.MessagesRegistered.Where(k => IsBindingMatching(k, msg, routingKey));
			foreach (var key in matches)
			{
				var handlers = listenerBindings[key].Select(ObjectFactory.GetInstance);
				foreach (var handler in handlers)
				{
					try
					{
						handler.GetType().InvokeMember("Handle", BindingFlags.InvokeMethod, null, handler, new object[] { message });


						var hooks = ObjectFactory.GetAllInstances<IEventHook>();
						foreach (var hook in hooks)
						{
							hook.MessageReceived(message);
						}
					}
					catch (Exception ex)
					{
						object handler1 = handler;


						var hooks = ObjectFactory.GetAllInstances<IEventHook>();
						foreach (var hook in hooks)
						{
							hook.HandlerFailed(message, handler1.GetType(), ex.InnerException);
						}
					}
				}
			}
		}

		private static bool IsBindingMatching(TypeRoutingKeyPair bindingInfo, Type msg, string routingKey)
		{
			return bindingInfo.Type.IsAssignableFrom(msg)
				&& (bindingInfo.RoutingKey == routingKey
				|| bindingInfo.RoutingKey == "#");
		}

		/// <summary>
		/// Remove bindings for the given handler
		/// </summary>
		public void Unregister<T>()
		{
			lock (listenerBindings)
			{
				foreach (var kvp in listenerBindings)
				{
					if (kvp.Value.Any(t => t == typeof(T)))
					{
						var newList = BagOf(kvp.Value, t => t != typeof(T));
						listenerBindings[kvp.Key] = newList;
					}
				}
			}
		}

		static ConcurrentBag<T> BagOf<T>(ConcurrentBag<T> source, Func<T, bool> predicate)
		{
			var b = new ConcurrentBag<T>();
			var s = source.ToArray();
			foreach (var source1 in s.Where(predicate))
			{
				b.Add(source1);
			}
			return b;
		}
	}
}