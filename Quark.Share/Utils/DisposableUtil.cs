namespace Quark.Utils;

/// <summary>
/// IDIsposableに関するユーティリティ
/// </summary>
public static class DisposableUtil
{
    /// <summary>
    /// 古い<see cref="IDisposable"/>インスタンスと新しい<see cref="IDisposable"/>を入れ替え、古いインスタンスを破棄する
    /// </summary>
    /// <typeparam name="T">型情報</typeparam>
    /// <param name="oldDisposable"></param>
    /// <param name="newDisposable"></param>
    public static void ExchangeDisposable<T>(ref T? oldDisposable, T? newDisposable)
        where T : class, IDisposable
    {
        using (var disposeTarget = oldDisposable)
        {
            oldDisposable = newDisposable;
        }
    }
}
