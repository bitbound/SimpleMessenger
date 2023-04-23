# SimpleMessenger
A simple, lightweight event messenger that uses a `ConditionalWeakTable` internally.

NuGet: https://www.nuget.org/packages/Nihs.SimpleMessenger


### Usage

``` C#
// Program.cs
using Nihs.SimpleMessenger;

// ...

services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
```

``` C#
// MyService.cs

public class MyService : IMyService
{
    public MyService(IMessenger messenger)
    {
        // Registers a handler under the default channel.
        // The handler will be removed automatically if
        // this MyService instance is garbage-collected.
        messenger.Register<SomeMessageType>(this, MyHandler);
    }

    private async Task MyHandler(SomeMessageType message)
    {
        // Handle the message.
    }
}
```