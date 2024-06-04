using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Quark.Components;

/// <summary>
/// 通知可能オブジェクト
/// </summary>
public abstract class NotificationObject : INotifyPropertyChanged
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// プロパティの変更を通知する
    /// </summary>
    /// <param name="e">変更されたぷおrパティ</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void OnPropertyChanged(PropertyChangedEventArgs e)
        => this.PropertyChanged?.Invoke(this, e);

    /// <summary>
    /// プロパティの変更を通知する
    /// </summary>
    /// <param name="propertyName">変更されたプロパティ名</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => this.OnPropertyChanged(new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// プロパティに変更があれば通知する
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="target">変更対象</param>
    /// <param name="newValue">新しい値</param>
    /// <param name="propertyName">プロパティ名</param>
    /// <returns>プロパティ変更の有無</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool SetIfChanged<T>(ref T target, ref T newValue, [CallerMemberName] string propertyName = "")
    {
        bool isNotEqual = !EqualityComparer<T>.Default.Equals(target, newValue);
        if (isNotEqual)
        {
            target = newValue;
            this.OnPropertyChanged(propertyName);
        }

        return isNotEqual;
    }

    /// <summary>
    /// プロパティに変更があれば通知する
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="target">変更対象</param>
    /// <param name="newValue">新しい値</param>
    /// <param name="propertyName">プロパティ名</param>
    /// <returns>プロパティ変更の有無</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool SetIfChanged<T>(ref T target, T newValue, [CallerMemberName] string propertyName = "")
    {
        bool isNotEqual = !EqualityComparer<T>.Default.Equals(target, newValue);
        if (isNotEqual)
        {
            target = newValue;
            this.OnPropertyChanged(propertyName);
        }

        return isNotEqual;
    }
}
