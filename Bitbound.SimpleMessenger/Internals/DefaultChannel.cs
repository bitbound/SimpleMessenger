using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bitbound.SimpleMessenger.Internals;
internal readonly struct DefaultChannel : IEquatable<DefaultChannel>
{
    private static readonly int _hashCode = 
        HashCode.Combine(
            Guid.Parse("a40d3b94-6559-4d7f-9524-3c345467ab1c"),
            "DefaultChannel");

    public static DefaultChannel Instance { get; } = new();

    public bool Equals(DefaultChannel other)
    {
        return true;
    }

    public override bool Equals(object? obj)
    {
        return 
            obj is DefaultChannel defaultChannel &&
            Equals(defaultChannel);
    }

    public override int GetHashCode()
    {
        return _hashCode;
    }
}
