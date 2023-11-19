
namespace Bitbound.SimpleMessenger;

public delegate Task RegistrationCallback<TMessage>(object subscriber, TMessage message);