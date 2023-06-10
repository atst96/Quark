using System.Collections.ObjectModel;

namespace Quark.Components;

public class DisposableCollection : List<IDisposable>, IDisposable
{
    public DisposableCollection() : base() { }

    public DisposableCollection(int capacity) : base(capacity) { }

    public void Dispose()
    {
        foreach (var disposable in this)
        {
            disposable.Dispose();
        }
    }
}
