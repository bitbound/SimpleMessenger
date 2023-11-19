using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bitbound.SimpleMessenger.Internals;
internal sealed class RegistrationToken : IDisposable
{
    private readonly Action _disposalAction;
    private bool _disposedValue;

    public RegistrationToken(Action disposalAction)
    {
        _disposalAction = disposalAction;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                try
                {
                    _disposalAction();
                }
                catch
                {
                    // Ignore errors.
                }
            }
            _disposedValue = true;
        }
    }
}
