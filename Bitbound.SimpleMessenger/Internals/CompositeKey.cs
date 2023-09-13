using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bitbound.SimpleMessenger.Internals;

/// <summary>
/// A dictionary key composed of the message type and channel type.
/// </summary>
internal readonly struct CompositeKey : IEquatable<CompositeKey>
{
    public CompositeKey(Type messageType, Type channelType)
    {
        MessageType = messageType;
        ChannelType = channelType;
    }

    public Type MessageType { get; }
    public Type ChannelType { get; }

    public bool Equals(CompositeKey other)
    {
        return
            MessageType == other.MessageType &&
            ChannelType == other.ChannelType;
    }

    public override bool Equals(object? obj)
    {
        return 
            obj is CompositeKey other &&
            Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(MessageType, ChannelType);
    }

    public static bool operator ==(CompositeKey left, CompositeKey right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(CompositeKey left, CompositeKey right)
    {
        return !left.Equals(right);
    }
}
