using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Nihs.SimpleMessenger.Internals;

internal class WeakReferenceTable
{
    private readonly object _tableLock = new();
    private readonly ConditionalWeakTable<object, object?> _weakTable = new();

    internal void AddOrUpdate<TMessageType>(object subscriber, Func<TMessageType, Task> handler)
    {
        lock (_tableLock)
        {
            _weakTable.AddOrUpdate(subscriber, handler);
        }
    }

    internal IEnumerable<Func<TMessageType, Task>> GetHandlers<TMessageType>()
    {
        lock (_tableLock)
        {
            foreach (var kvp in _weakTable.ToArray())
            {
                if (kvp.Value is Func<TMessageType, Task> func)
                {
                    yield return func;
                }
            }
        }
    }

    internal bool Remove(object subscriber)
    {
        lock (_tableLock)
        {
            return _weakTable.Remove(subscriber);
        }
    }

    internal bool TryGetValue(object subscriber, [NotNullWhen(true)] out object? handler)
    {
        lock (_tableLock)
        {
            return _weakTable.TryGetValue(subscriber, out handler);
        }
    }
}
