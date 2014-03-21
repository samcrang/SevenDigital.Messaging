using System;

namespace SevenDigital.Messaging.Loopback
{
	public class TypeRoutingKeyPair : IEquatable<TypeRoutingKeyPair>
	{
		public bool Equals(TypeRoutingKeyPair other)
		{
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return string.Equals(_routingKey, other._routingKey) && Equals(_type, other._type);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((TypeRoutingKeyPair) obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return ((_routingKey != null ? _routingKey.GetHashCode() : 0)*397) ^ (_type != null ? _type.GetHashCode() : 0);
			}
		}

		public static bool operator ==(TypeRoutingKeyPair left, TypeRoutingKeyPair right)
		{
			return Equals(left, right);
		}

		public static bool operator !=(TypeRoutingKeyPair left, TypeRoutingKeyPair right)
		{
			return !Equals(left, right);
		}

		readonly string _routingKey;
		readonly Type _type;

		public TypeRoutingKeyPair(Type type, string routingKey)
		{
			_type = type;
			_routingKey = routingKey;
		}

		public Type Type
		{
			get { return _type; }
		}

		public string RoutingKey
		{
			get { return _routingKey; }
		}
	}
}