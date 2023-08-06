namespace Nihs.SimpleMessenger.Tests;

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
            await messenger.Register<CountObject>(subscriber, obj =>
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
    public async Task Send_GivenSubscriberExplicitlyUnregistered_DoesNotInvokeHandler()
    {
        var messenger = new WeakReferenceMessenger();
        var count = 0;

        var subscriber = new object();
        await messenger.Register<CountObject>(subscriber, obj =>
        {
            count += obj.Value;
            return Task.CompletedTask;
        });

        await messenger.Send(new CountObject(3));
        Assert.AreEqual(3, count);

        await messenger.Send(new CountObject(7));
        Assert.AreEqual(10, count);

        await messenger.Unregister<CountObject>(subscriber);

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

        await messenger.Register(subscriber1, channel1, (CountObject obj) =>
        {
            count1 += obj.Value;
            return Task.CompletedTask;
        });

        // Same subscriber, different channel.
        await messenger.Register(subscriber1, channel2, (CountObject obj) =>
        {
            count2 += obj.Value;
            return Task.CompletedTask;
        });

        // Different subscriber, different channel.
        await messenger.Register(subscriber2, channel3, (CountObject obj) =>
        {
            count3 += obj.Value;
            return Task.CompletedTask;
        });

        // Different subscriber, same channel.
        await messenger.Register(subscriber3, channel1, (CountObject obj) =>
        {
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

        await messenger.Register(subscriber1, channel1, (CountObject obj) =>
        {
            count1 += obj.Value;
            return Task.CompletedTask;
        });

        // Same subscriber, different channel.
        await messenger.Register(subscriber1, channel2, (CountObject obj) =>
        {
            count2 += obj.Value;
            return Task.CompletedTask;
        });

        // Different subscriber, different channel.
        await messenger.Register(subscriber2, channel3, (CountObject obj) =>
        {
            count3 += obj.Value;
            return Task.CompletedTask;
        });

        // Different subscriber, same channel.
        await messenger.Register(subscriber3, channel1, (CountObject obj) =>
        {
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

        await messenger.Register(subscriber1, channel1, (CountObject obj) =>
        {
            count1 += obj.Value;
            return Task.CompletedTask;
        });

        // Same subscriber, different channel.
        await messenger.Register(subscriber1, channel2, (CountObject obj) =>
        {
            count2 += obj.Value;
            return Task.CompletedTask;
        });

        // Different subscriber, different channel.
        await messenger.Register(subscriber2, channel3, (CountObject obj) =>
        {
            count3 += obj.Value;
            return Task.CompletedTask;
        });

        // Different subscriber, same channel.
        await messenger.Register(subscriber3, channel1, (CountObject obj) =>
        {
            count4 += obj.Value;
            return Task.CompletedTask;
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

        await messenger.Register<CountObject>(this, obj =>
        {
            throw new InvalidOperationException("Test");
        });

        var exceptions = await messenger.Send(new CountObject(5));

        Assert.AreEqual(1, exceptions.Count);
        Assert.IsInstanceOfType(exceptions[0], typeof(InvalidOperationException));
        Assert.AreEqual("Test", exceptions[0].Message);
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
