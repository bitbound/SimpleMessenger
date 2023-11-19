using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bitbound.SimpleMessenger.Internals;
internal class SubscriberReference<TMessage>
{
    public SubscriberReference(object subscriber, RegistrationCallback<TMessage> handler)
    {
        Subscriber = subscriber;
        Handler = handler;
    }

    public object Subscriber { get; }
    public RegistrationCallback<TMessage> Handler { get; }
}
