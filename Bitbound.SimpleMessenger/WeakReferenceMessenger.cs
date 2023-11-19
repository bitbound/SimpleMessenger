using Bitbound.SimpleMessenger.Internals;
using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Bitbound.SimpleMessenger;

/// <summary>
/// A service for sending and receiving messages between decoupled objects.
/// The default implementation, <see cref="WeakReferenceMessenger"/>, will
/// automatically remove handlers when the subscriber is garbage-collected.
/// </summary>
public interface IMessenger
{
    /// <summary>
    /// Whether the specified subscriber is registered for a particular
    /// message type and channel.
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <typeparam name="TChannel"></typeparam>
    /// <param name="subscriber"></param>
    /// <param name="channel"></param>
    /// <returns></returns>
    bool IsRegistered<TMessage, TChannel>(object subscriber, TChannel channel)
        where TMessage : class
        where TChannel : IEquatable<TChannel>;

    /// <summary>
    /// Whether the specified subscriber is registered for a particular
    /// message type under the default channel.
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <param name="subscriber"></param>
    /// <returns></returns>
    bool IsRegistered<TMessage>(object subscriber)
        where TMessage : class;

    /// <summary>
    /// Registers the subscriber using the specified message type, channel, and handler.
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <typeparam name="TChannel"></typeparam>
    /// <param name="subscriber"></param>
    /// <param name="channel"></param>
    /// <param name="handler"></param>
    /// <returns>A token that, when disposed, will unregister the handler.</returns>
    IDisposable Register<TMessage, TChannel>(
        object subscriber, 
        TChannel channel,
        RegistrationCallback<TMessage> handler)
            where TMessage : class
            where TChannel : IEquatable<TChannel>;

    /// <summary>
    /// Registers the subscriber using the specified message type and handler,
    /// under the default channel.
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <param name="subscriber"></param>
    /// <param name="handler"></param>
    /// <returns>A token that, when disposed, will unregister the handler.</returns>
    IDisposable Register<TMessage>(object subscriber, RegistrationCallback<TMessage> handler)
        where TMessage : class;

    /// <summary>
    /// Sends a message to the specified channel.
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <typeparam name="TChannel"></typeparam>
    /// <param name="message"></param>
    /// <param name="channel"></param>
    /// <returns>A list of exceptions, if any, that occurred while invoking the handlers.</returns>
    Task<IImmutableList<Exception>> Send<TMessage, TChannel>(TMessage message, TChannel channel)
        where TMessage : class
        where TChannel : IEquatable<TChannel>;

    /// <summary>
    /// Sends a message to the default channel.
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <param name="message"></param>
    /// <returns>A list of exceptions, if any, that occurred while invoking the handlers.</returns>
    Task<IImmutableList<Exception>> Send<TMessage>(TMessage message)
        where TMessage : class;

    /// <summary>
    /// Unregistered the subscriber from the specified message type and channel.
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <typeparam name="TChannel"></typeparam>
    /// <param name="subscriber"></param>
    /// <param name="channel"></param>
    /// <returns></returns>
    void Unregister<TMessage, TChannel>(object subscriber, TChannel channel)
        where TMessage : class
        where TChannel : IEquatable<TChannel>;

    /// <summary>
    /// Unregistered the subscriber from the default channel.
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <param name="subscriber"></param>
    /// <returns></returns>
    void Unregister<TMessage>(object subscriber)
        where TMessage : class;

    /// <summary>
    /// Unregister all handlers for the subscriber on the default channel.
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <param name="subscriber"></param>
    /// <returns></returns>
    void UnregisterAll(object subscriber);

    /// <summary>
    /// Unregister all handlers for the subscriber on the given channel.
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <param name="subscriber"></param>
    /// <returns></returns>
    void UnregisterAll<TChannel>(object subscriber, TChannel channel)
        where TChannel : IEquatable<TChannel>;
}

/// <summary>
/// An implementation of <see cref="IMessenger"/> that will automatically remove
/// handlers when the subscriber is garbage-collected.
/// </summary>
public class WeakReferenceMessenger : IMessenger
{
    private readonly SemaphoreSlim _registrationLock = new(1, 1);
    private readonly ConcurrentDictionary<CompositeKey, ConcurrentDictionary<object, WeakReferenceTable>> _subscribers = new();
    public static IMessenger Default { get; } = new WeakReferenceMessenger();

    /// <inheritdoc />
    public bool IsRegistered<TMessage>(object subscriber)
        where TMessage : class
    {
        return IsRegistered<TMessage, DefaultChannel>(subscriber, DefaultChannel.Instance);
    }

    /// <inheritdoc />
    public bool IsRegistered<TMessage, TChannel>(object subscriber, TChannel channel)
        where TMessage : class
        where TChannel : IEquatable<TChannel>
    {
        _registrationLock.Wait();
        try
        {
            var table = GetWeakReferenceTable<TMessage, TChannel>(channel);

            return table.TryGetValue(subscriber, out _);
        }
        finally
        {
            _registrationLock.Release();
        }
    }

    /// <inheritdoc />
    public IDisposable Register<TMessage>(
        object subscriber, 
        RegistrationCallback<TMessage> handler)
        where TMessage : class
    {
        return Register(subscriber, DefaultChannel.Instance, handler);
    }

    /// <inheritdoc />
    public IDisposable Register<TMessage, TChannel>(
        object subscriber, 
        TChannel channel, 
        RegistrationCallback<TMessage> handler)
        where TMessage : class
        where TChannel : IEquatable<TChannel>
    {
        _registrationLock.Wait();
        try
        {
            var table = GetWeakReferenceTable<TMessage, TChannel>(channel);

            if (table.TryGetValue(subscriber, out _))
            {
                throw new InvalidOperationException(
                    "Subscriber is already registered to the specified message and channel.");
            }

            table.AddOrUpdate(subscriber, handler);

            return new RegistrationToken(() =>
            {
                Unregister<TMessage, TChannel>(subscriber, channel);
            });
        }
        finally
        {
            _registrationLock.Release();
        }
    }

    /// <inheritdoc />
    public Task<IImmutableList<Exception>> Send<TMessage>(TMessage message)
        where TMessage : class
    {
        return Send(message, DefaultChannel.Instance);
    }

    /// <inheritdoc />
    public async Task<IImmutableList<Exception>> Send<TMessage, TChannel>(TMessage message, TChannel channel)
        where TMessage : class
        where TChannel : IEquatable<TChannel>
    {
        var subscriberRefs = await GetSubscribers<TMessage, TChannel>(channel);
        var exceptions = new List<Exception>();

        foreach (var subscriberRef in subscriberRefs)
        {
            try
            {
                var handler = subscriberRef.Handler;
                var subscriber = subscriberRef.Subscriber;
                await handler.Invoke(subscriber, message);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }
        return exceptions.ToImmutableList();
    }

    /// <inheritdoc />
    public void Unregister<TMessage>(object subscriber)
        where TMessage : class
    {
        Unregister<TMessage, DefaultChannel>(subscriber, DefaultChannel.Instance);
    }

    /// <inheritdoc />
    public void Unregister<TMessage, TChannel>(object subscriber, TChannel channel)
        where TMessage : class
        where TChannel : IEquatable<TChannel>
    {
        _registrationLock.Wait();
        try
        {
            var table = GetWeakReferenceTable<TMessage, TChannel>(channel);
            table.Remove(subscriber);
        }
        finally
        {
            _registrationLock.Release();
        }
    }

    /// <inheritdoc />
    public void UnregisterAll(object subscriber)
    {
        _registrationLock.Wait();
        try
        {
            foreach (var channelMap in _subscribers.Values)
            {
                foreach (var table in channelMap.Values)
                {
                    _ = table.Remove(subscriber);
                }
            }
        }
        finally
        {
            _registrationLock.Release();
        }
    }

    /// <inheritdoc />
    public void UnregisterAll<TChannel>(object subscriber, TChannel channel)
        where TChannel : IEquatable<TChannel>
    {
        _registrationLock.Wait();
        try
        {
            var channels = _subscribers.Where(x => x.Key.ChannelType == typeof(TChannel));

            foreach (var channelMap in channels)
            {
                if (channelMap.Value.TryGetValue(channel, out var table))
                {
                    _ = table.Remove(subscriber);
                }
            }
        }
        finally
        {
            _registrationLock.Release();
        }
    }

    private async Task<IEnumerable<SubscriberReference<TMessage>>> GetSubscribers<TMessage, TChannel>(TChannel channel)
        where TMessage : class
        where TChannel : IEquatable<TChannel>
    {
        await _registrationLock.WaitAsync();
        try
        {
            var table = GetWeakReferenceTable<TMessage, TChannel>(channel);
            return table.GetSubscribers<TMessage>();
        }
        finally
        {
            _registrationLock.Release();
        }
    }

    private WeakReferenceTable GetWeakReferenceTable<TMessage, TChannel>(TChannel channel)
        where TMessage : class
        where TChannel : IEquatable<TChannel>
    {
        var key = new CompositeKey(typeof(TMessage), typeof(TChannel));
        var channelMap = _subscribers.GetOrAdd(key, k => new());
        return channelMap.GetOrAdd(channel, key => new());
    }
}