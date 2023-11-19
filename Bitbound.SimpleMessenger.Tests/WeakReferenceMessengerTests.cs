namespace Bitbound.SimpleMessenger.Tests;

[TestClass]
public class WeakReferenceMessengerTests
{
    [TestMethod]
    public async Task Send_GivenSubscriberHasBeenGCed_DoesNotInvokeHandler()
    {
        var messenger = new WeakReferenceMessenger();
        var count = 0;

        await Task.Run(async () =>
        {
            var subscriber = new object();
            messenger.Register<CountObject>(subscriber, (sub, message) =>
            {
                Assert.ReferenceEquals(sub, subscriber);
                count += message.Value;
                return Task.CompletedTask;
            });

            await messenger.Send(new CountObject(3));
            Assert.AreEqual(3, count);

            await messenger.Send(new CountObject(7));
            Assert.AreEqual(10, count);
        });

        GC.Collect();
        GC.WaitForPendingFinalizers();

        var exceptions = await messenger.Send(new CountObject(5));
        // The value should not change here because the subscriber
        // has been garbage-collected.
        Assert.AreEqual(10, count);
        Assert.AreEqual(0, exceptions.Count);
    }

    [TestMethod]
    public async Task Send_GivenRegistrationTokenDisposed_DoesNotInvokeHandler()
    {
        var messenger = new WeakReferenceMessenger();
        var count = 0;

        var subscriber = new object();
        var token = messenger.Register<CountObject>(subscriber, (sub, message) =>
        {
            Assert.ReferenceEquals(sub, subscriber);
            count += message.Value;
            return Task.CompletedTask;
        });

        await messenger.Send(new CountObject(3));
        Assert.AreEqual(3, count);

        await messenger.Send(new CountObject(7));
        Assert.AreEqual(10, count);

        token.Dispose();

        var exceptions = await messenger.Send(new CountObject(5));
        // The value should not change here because the subscriber
        // has been garbage-collected.
        Assert.AreEqual(10, count);
        Assert.AreEqual(0, exceptions.Count);
    }

    [TestMethod]
    public async Task Send_GivenSubscriberExplicitlyUnregistered_DoesNotInvokeHandler()
    {
        var messenger = new WeakReferenceMessenger();
        var count = 0;

        var subscriber = new object();
        messenger.Register<CountObject>(subscriber, (sub, message) =>
        {
            Assert.ReferenceEquals(sub, subscriber);
            count += message.Value;
            return Task.CompletedTask;
        });

        await messenger.Send(new CountObject(3));
        Assert.AreEqual(3, count);

        await messenger.Send(new CountObject(7));
        Assert.AreEqual(10, count);

        messenger.Unregister<CountObject>(subscriber);

        var exceptions = await messenger.Send(new CountObject(5));
        // The value should not change here because the subscriber
        // has been garbage-collected.
        Assert.AreEqual(10, count);
        Assert.AreEqual(0, exceptions.Count);
    }

    [TestMethod]
    public async Task Send_GivenDifferentChannelsDifferentTypes_MessagesAreSentToCorrectChannels()
    {
        var messenger = new WeakReferenceMessenger();
        var count1 = 0;
        var count2 = 0;
        var count3 = 0;
        var count4 = 0;

        var channel1 = Guid.NewGuid();
        var channel2 = "channel2";
        var channel3 = 5;

        var subscriber1 = new object();
        var subscriber2 = new object();
        var subscriber3 = new object();

        messenger.Register(subscriber1, channel1, (object sub, CountObject obj) =>
        {
            Assert.ReferenceEquals(sub, subscriber1);
            count1 += obj.Value;
            return Task.CompletedTask;
        });

        // Same subscriber, different channel.
        messenger.Register(subscriber1, channel2, (object sub, CountObject obj) =>
        {
            Assert.ReferenceEquals(sub, subscriber1);
            count2 += obj.Value;
            return Task.CompletedTask;
        });

        // Different subscriber, different channel.
        messenger.Register(subscriber2, channel3, (object sub, CountObject obj) =>
        {
            Assert.ReferenceEquals(sub, subscriber2);
            count3 += obj.Value;
            return Task.CompletedTask;
        });

        // Different subscriber, same channel.
        messenger.Register(subscriber3, channel1, (object sub, CountObject obj) =>
        {
            Assert.ReferenceEquals(sub, subscriber3);
            count4 += obj.Value;
            return Task.CompletedTask;
        });

        // Counts 1 and 4 should be affected.
        await messenger.Send(new CountObject(3), channel1);
        // Count 2 should be affected.
        await messenger.Send(new CountObject(5), channel2);
        // Count 3 should be affected.
        await messenger.Send(new CountObject(7), channel3);

        Assert.AreEqual(3, count1);
        Assert.AreEqual(5, count2);
        Assert.AreEqual(7, count3);
        Assert.AreEqual(3, count4);
    }

    [TestMethod]
    public async Task Send_GivenDifferentChannelsSameType_MessagesAreSentToCorrectChannels()
    {
        var messenger = new WeakReferenceMessenger();
        var count1 = 0;
        var count2 = 0;
        var count3 = 0;
        var count4 = 0;

        var channel1 = "channel1";
        var channel2 = "channel2";
        var channel3 = "channel3";

        var subscriber1 = new object();
        var subscriber2 = new object();
        var subscriber3 = new object();

        messenger.Register(subscriber1, channel1, (object sub, CountObject obj) =>
        {
            Assert.ReferenceEquals(sub, subscriber1);
            count1 += obj.Value;
            return Task.CompletedTask;
        });

        // Same subscriber, different channel.
        messenger.Register(subscriber1, channel2, (object sub, CountObject obj) =>
        {
            Assert.ReferenceEquals(sub, subscriber1);
            count2 += obj.Value;
            return Task.CompletedTask;
        });

        // Different subscriber, different channel.
        messenger.Register(subscriber2, channel3, (object sub, CountObject obj) =>
        {
            Assert.ReferenceEquals(sub, subscriber2);
            count3 += obj.Value;
            return Task.CompletedTask;
        });

        // Different subscriber, same channel.
        messenger.Register(subscriber3, channel1, (object sub, CountObject obj) =>
        {
            Assert.ReferenceEquals(sub, subscriber3);
            count4 += obj.Value;
            return Task.CompletedTask;
        });

        // Counts 1 and 4 should be affected.
        await messenger.Send(new CountObject(3), channel1);
        // Count 2 should be affected.
        await messenger.Send(new CountObject(5), channel2);
        // Count 3 should be affected.
        await messenger.Send(new CountObject(7), channel3);

        Assert.AreEqual(3, count1);
        Assert.AreEqual(5, count2);
        Assert.AreEqual(7, count3);
        Assert.AreEqual(3, count4);
    }

    [TestMethod]
    public async Task Send_GivenFrequentMessages_MessagesAreSentToCorrectChannels()
    {
        var sendCount = 1_000;
        var messenger = new WeakReferenceMessenger();
        var updateLock = new object();
        var count1 = 0;
        var count2 = 0;
        var count3 = 0;
        var count4 = 0;

        var channel1 = Guid.NewGuid();
        var channel2 = "channel2";
        var channel3 = 5;

        var subscriber1 = new object();
        var subscriber2 = new object();
        var subscriber3 = new object();

        messenger.Register(subscriber1, channel1, (object sub, CountObject obj) =>
        {
            lock (updateLock)
            {
                Assert.ReferenceEquals(sub, subscriber1);
                count1 += obj.Value;
                return Task.CompletedTask;
            }
        });

        // Same subscriber, different channel.
        messenger.Register(subscriber1, channel2, (object sub, CountObject obj) =>
        {
            lock (updateLock)
            {
                Assert.ReferenceEquals(sub, subscriber1);
                count2 += obj.Value;
                return Task.CompletedTask;
            }
        });

        // Different subscriber, different channel.
        messenger.Register(subscriber2, channel3, (object sub, CountObject obj) =>
        {
            lock (updateLock)
            {
                Assert.ReferenceEquals(sub, subscriber2);
                count3 += obj.Value;
                return Task.CompletedTask;
            }
        });

        // Different subscriber, same channel.
        messenger.Register(subscriber3, channel1, (object sub, CountObject obj) =>
        {
            lock (updateLock)
            {
                Assert.ReferenceEquals(sub, subscriber3);
                count4 += obj.Value;
                return Task.CompletedTask;
            }
        });

        var range = Enumerable.Range(0, sendCount);
        await Parallel.ForEachAsync(range, async (int i, CancellationToken ct) =>
        {
            // Counts 1 and 4 should be affected.
            await messenger.Send(new CountObject(3), channel1);
            // Count 2 should be affected.
            await messenger.Send(new CountObject(5), channel2);
            // Count 3 should be affected.
            await messenger.Send(new CountObject(7), channel3);
        });

        Assert.AreEqual(3 * sendCount, count1);
        Assert.AreEqual(5 * sendCount, count2);
        Assert.AreEqual(7 * sendCount, count3);
        Assert.AreEqual(3 * sendCount, count4);
    }

    [TestMethod]
    public async Task Send_GivenHandlerThrows_ReturnsException()
    {
        var messenger = new WeakReferenceMessenger();

        messenger.Register(this, (object sender, CountObject message) =>
        {
            throw new InvalidOperationException("Test");
        });

        var exceptions = await messenger.Send(new CountObject(5));

        Assert.AreEqual(1, exceptions.Count);
        Assert.IsInstanceOfType(exceptions[0], typeof(InvalidOperationException));
        Assert.AreEqual("Test", exceptions[0].Message);
    }

    [TestMethod]
    public async Task Send_GivenSubscriberExplicitlyUnregistered_DoesNotInvokeHandler()
    {
        var messenger = new WeakReferenceMessenger();
        var count = 0;

        var subscriber = new object();
        messenger.Register<CountObject>(subscriber, obj =>
        {
            count += obj.Value;
            return Task.CompletedTask;
        });

        await messenger.Send(new CountObject(3));
        Assert.AreEqual(3, count);

        await messenger.Send(new CountObject(7));
        Assert.AreEqual(10, count);

        messenger.Unregister<CountObject>(subscriber);

        var exceptions = await messenger.Send(new CountObject(5));
        // The value should not change here because the subscriber
        // has been garbage-collected.
        Assert.AreEqual(10, count);
        Assert.AreEqual(0, exceptions.Count);
    }

    [TestMethod]
    public async Task Send_GivenSubscriberHasBeenGCed_DoesNotInvokeHandler()
    {
        var messenger = new WeakReferenceMessenger();
        var count = 0;

        await Task.Run(async () =>
        {
            var subscriber = new object();
            messenger.Register<CountObject>(subscriber, obj =>
            {
                count += obj.Value;
                return Task.CompletedTask;
            });

            await messenger.Send(new CountObject(3));
            Assert.AreEqual(3, count);

            await messenger.Send(new CountObject(7));
            Assert.AreEqual(10, count);
        });

        GC.Collect();
        GC.WaitForPendingFinalizers();

        var exceptions = await messenger.Send(new CountObject(5));
        // The value should not change here because the subscriber
        // has been garbage-collected.
        Assert.AreEqual(10, count);
        Assert.AreEqual(0, exceptions.Count);
    }

    [TestMethod]
    public void UnregisterAll_GivenMultipleChannelSubscriptions_Ok()
    {
        var messenger = new WeakReferenceMessenger();

        var channel1 = "channel1";
        var channel2 = "channel2";
        var channel3 = "channel3";

        var subscriber1 = new object();
        var subscriber2 = new object();
        var subscriber3 = new object();

        var channels = new[] { channel1, channel2, channel3 };
        var subscribers = new[] { subscriber1, subscriber2, subscriber3 };

        foreach (var channel in channels)
        {
            foreach (var subscriber in subscribers)
            {
                messenger.Register(subscriber, channel, (CountObject obj) => Task.CompletedTask);
            }
        }

        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber1, channel1));
        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber1, channel2));
        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber1, channel3));

        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber2, channel1));
        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber2, channel2));
        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber2, channel3));

        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber3, channel1));
        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber3, channel2));
        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber3, channel3));

        messenger.UnregisterAll(subscriber1);

        Assert.IsFalse(messenger.IsRegistered<CountObject, string>(subscriber1, channel1));
        Assert.IsFalse(messenger.IsRegistered<CountObject, string>(subscriber1, channel2));
        Assert.IsFalse(messenger.IsRegistered<CountObject, string>(subscriber1, channel3));

        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber2, channel1));
        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber2, channel2));
        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber2, channel3));

        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber3, channel1));
        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber3, channel2));
        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber3, channel3));
    }

    [TestMethod]
    public void UnregisterAll_GivenMultipleChannelSubscriptionsAndSpecificChannelToUnregister_Ok()
    {
        var messenger = new WeakReferenceMessenger();

        var channel1 = "channel1";
        var channel2 = "channel2";
        var channel3 = "channel3";

        var subscriber1 = new object();
        var subscriber2 = new object();
        var subscriber3 = new object();

        var channels = new[] { channel1, channel2, channel3 };
        var subscribers = new[] { subscriber1, subscriber2, subscriber3 };

        foreach (var channel in channels)
        {
            foreach (var subscriber in subscribers)
            {
                messenger.Register(subscriber, channel, (CountObject obj) => Task.CompletedTask);
            }
        }

        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber1, channel1));
        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber1, channel2));
        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber1, channel3));

        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber2, channel1));
        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber2, channel2));
        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber2, channel3));

        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber3, channel1));
        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber3, channel2));
        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber3, channel3));

        messenger.UnregisterAll(subscriber1, channel2);

        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber1, channel1));
        Assert.IsFalse(messenger.IsRegistered<CountObject, string>(subscriber1, channel2));
        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber1, channel3));

        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber2, channel1));
        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber2, channel2));
        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber2, channel3));

        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber3, channel1));
        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber3, channel2));
        Assert.IsTrue(messenger.IsRegistered<CountObject, string>(subscriber3, channel3));
    }

    private class CountObject
    {
        public CountObject(int value)
        {
            Value = value;
        }

        public int Value { get; }
    }
}