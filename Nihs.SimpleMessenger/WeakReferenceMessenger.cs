using Nihs.SimpleMessenger.Internals;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Nihs.SimpleMessenger;

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
    /// <typeparam name="TMessageType"></typeparam>
    /// <typeparam name="TChannelType"></typeparam>
    /// <param name="subscriber"></param>
    /// <param name="channel"></param>
    /// <returns></returns>
    Task<bool> IsRegistered<TMessageType, TChannelType>(object subscriber, TChannelType channel)
        where TMessageType : class
        where TChannelType : IEquatable<TChannelType>;

    /// <summary>
    /// Whether the specified subscriber is registered for a particular 
    /// message type under the default channel.
    /// </summary>
    /// <typeparam name="TMessageType"></typeparam>
    /// <param name="subscriber"></param>
    /// <returns></returns>
    Task<bool> IsRegistered<TMessageType>(object subscriber)
        where TMessageType : class;

    /// <summary>
    /// Registers the subscriber using the specified message type, channel, and handler.
    /// </summary>
    /// <typeparam name="TMessageType"></typeparam>
    /// <typeparam name="TChannelType"></typeparam>
    /// <param name="subscriber"></param>
    /// <param name="channel"></param>
    /// <param name="handler"></param>
    /// <returns></returns>
    Task Register<TMessageType, TChannelType>(
        object subscriber,
        TChannelType channel,
        Func<TMessageType, Task> handler)
            where TMessageType : class
            where TChannelType : IEquatable<TChannelType>;

    /// <summary>
    /// Registers the subscriber using the specified message type and handler, 
    /// under the default channel.
    /// </summary>
    /// <typeparam name="TMessageType"></typeparam>
    /// <param name="subscriber"></param>
    /// <param name="handler"></param>
    /// <returns></returns>
    Task Register<TMessageType>(object subscriber, Func<TMessageType, Task> handler)
        where TMessageType : class;

    /// <summary>
    /// Sends a message to the specified channel.
    /// </summary>
    /// <typeparam name="TMessageType"></typeparam>
    /// <typeparam name="TChannelType"></typeparam>
    /// <param name="message"></param>
    /// <param name="channel"></param>
    /// <returns>A list of exceptions, if any, that occurred while invoking the handlers.</returns>
    Task<List<Exception>> Send<TMessageType, TChannelType>(TMessageType message, TChannelType channel)
        where TMessageType : class
        where TChannelType : IEquatable<TChannelType>;

    /// <summary>
    /// Sends a message to the default channel.
    /// </summary>
    /// <typeparam name="TMessageType"></typeparam>
    /// <param name="message"></param>
    /// <returns>A list of exceptions, if any, that occurred while invoking the handlers.</returns>
    Task<List<Exception>> Send<TMessageType>(TMessageType message)
        where TMessageType : class;

    /// <summary>
    /// Unregistered the subscriber from the specified message type and channel.
    /// </summary>
    /// <typeparam name="TMessageType"></typeparam>
    /// <typeparam name="TChannelType"></typeparam>
    /// <param name="subscriber"></param>
    /// <param name="channel"></param>
    /// <returns></returns>
    Task Unregister<TMessageType, TChannelType>(object subscriber, TChannelType channel)
        where TMessageType : class
        where TChannelType : IEquatable<TChannelType>;

    /// <summary>
    /// Unregistered the subscriber from the default channel.
    /// </summary>
    /// <typeparam name="TMessageType"></typeparam>
    /// <param name="subscriber"></param>
    /// <returns></returns>
    Task Unregister<TMessageType>(object subscriber)
        where TMessageType : class;
}


/// <summary>
/// An implementation of <see cref="IMessenger"/> that will automatically remove
/// handlers when the subscriber is garbage-collected.
/// </summary>
public class WeakReferenceMessenger : IMessenger
{
    private readonly ConcurrentDictionary<CompositeKey, ConcurrentDictionary<object, WeakReferenceTable>> _subscribers = new();
    private readonly SemaphoreSlim _subscribersLock = new(1, 1);

    public static IMessenger Default { get; } = new WeakReferenceMessenger();

    /// <inheritdoc />
    public Task<bool> IsRegistered<TMessageType>(object subscriber)
        where TMessageType : class
    {
        return IsRegistered<TMessageType, DefaultChannel>(subscriber, DefaultChannel.Instance);
    }

    /// <inheritdoc />
    public async Task<bool> IsRegistered<TMessageType, TChannelType>(object subscriber, TChannelType channel)
        where TMessageType : class
        where TChannelType : IEquatable<TChannelType>
    {
        await _subscribersLock.WaitAsync();
        try
        {
            var table = GetWeakReferenceTable<TMessageType, TChannelType>(channel);
            var result = await table.TryGetValue(subscriber);
            return result.IsSuccess;
        }
        finally
        {
            _subscribersLock.Release();
        }
    }

    /// <inheritdoc />
    public Task Register<TMessageType>(object subscriber, Func<TMessageType, Task> handler)
        where TMessageType : class
    {
        return Register(subscriber, DefaultChannel.Instance, handler);
    }

    /// <inheritdoc />
    public async Task Register<TMessageType, TChannelType>(object subscriber, TChannelType channel, Func<TMessageType, Task> handler)
        where TMessageType : class
        where TChannelType : IEquatable<TChannelType>
    {
        await _subscribersLock.WaitAsync();
        try
        {
            var table = GetWeakReferenceTable<TMessageType, TChannelType>(channel);

            var result = await table.TryGetValue(subscriber);
            if (result.IsSuccess)
            {
                throw new InvalidOperationException(
                    "Subscriber is already registered to the specified message and channel.");
            }

            await table.AddOrUpdate(subscriber, handler);
        }
        finally
        {
            _subscribersLock.Release();
        }
    }

    /// <inheritdoc />
    public Task<List<Exception>> Send<TMessageType>(TMessageType message)
        where TMessageType : class
    {
        return Send(message, DefaultChannel.Instance);
    }

    /// <inheritdoc />
    public async Task<List<Exception>> Send<TMessageType, TChannelType>(TMessageType message, TChannelType channel)
        where TMessageType : class
        where TChannelType : IEquatable<TChannelType>
    {
        await _subscribersLock.WaitAsync();
        try
        {
            var table = GetWeakReferenceTable<TMessageType, TChannelType>(channel);
            var handlers = await table.GetHandlers<TMessageType>();
            var exceptions = new List<Exception>();

            foreach (var handler in handlers)
            {
                try
                {
                    await handler.Invoke(message);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
            return exceptions;
        }
        finally
        {
            _subscribersLock.Release();
        }
    }

    /// <inheritdoc />
    public Task Unregister<TMessageType>(object subscriber)
        where TMessageType : class
    {
        return Unregister<TMessageType, DefaultChannel>(subscriber, DefaultChannel.Instance);
    }

    /// <inheritdoc />
    public async Task Unregister<TMessageType, TChannelType>(object subscriber, TChannelType channel)
        where TMessageType : class
        where TChannelType : IEquatable<TChannelType>
    {
        await _subscribersLock.WaitAsync();
        try
        {
            var table = GetWeakReferenceTable<TMessageType, TChannelType>(channel);
            await table.Remove(subscriber);
        }
        finally
        {
            _subscribersLock.Release();
        }
    }

    private WeakReferenceTable GetWeakReferenceTable<TMessageType, TChannelType>(TChannelType channel)
        where TMessageType : class
        where TChannelType : IEquatable<TChannelType>
    {
        var key = new CompositeKey(typeof(TMessageType), typeof(TChannelType));
        var channelMap = _subscribers.GetOrAdd(key, k => new());
        return channelMap.GetOrAdd(channel, key => new());
    }
}