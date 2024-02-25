using System;
using System.Collections.Generic;

namespace Quark.Mvvm;

public class DisposableCollection : List<IDisposable>, IDisposable
{
    public void Dispose()
    {
        foreach (var item in this)
        {
            try
            {
                item.Dispose();
            }
            catch (Exception e)
            {
                // TODO: ログ出力とか
                // pass
            }
        }
    }
}
