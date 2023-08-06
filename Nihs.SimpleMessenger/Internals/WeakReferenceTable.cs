using System.Runtime.CompilerServices;

namespace Nihs.SimpleMessenger.Internals;


internal class WeakReferenceTable
{
    private readonly SemaphoreSlim _tableLock = new(1, 1);
    private readonly ConditionalWeakTable<object, object?> _weakTable = new();

    internal async Task AddOrUpdate<TMessageType>(object subscriber, Func<TMessageType, Task> handler)
    {
        await _tableLock.WaitAsync();
        try
        {
            _weakTable.AddOrUpdate(subscriber, handler);
        }
        finally
        {
            _tableLock.Release();
        }
    }

    internal async Task<IEnumerable<Func<TMessageType, Task>>> GetHandlers<TMessageType>()
    {
        await _tableLock.WaitAsync();
        try
        {
            var list = new List<Func<TMessageType, Task>>();
            foreach (var kvp in _weakTable)
            {
                if (kvp.Value is Func<TMessageType, Task> func)
                {
                    list.Add(func);
                }
            }
            return list;
        }
        finally
        {
            _tableLock.Release();
        }
    }

    internal async Task<bool> Remove(object subscriber)
    {
        await _tableLock.WaitAsync();
        try
        {
            return _weakTable.Remove(subscriber);
        }
        finally
        {
            _tableLock.Release();
        }
    }

    internal async Task<Result<object>> TryGetValue(object subscriber)
    {
        await _tableLock.WaitAsync();
        try
        {
            if (_weakTable.TryGetValue(subscriber, out var handler) &&
                handler is not null)
            {
                return Result.Ok(handler);
            }
            return Result.Fail<object>("Key not found.");
        }
        finally
        {
            _tableLock.Release();
        }
    }
}
