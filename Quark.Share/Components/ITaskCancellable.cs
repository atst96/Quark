namespace Quark.Components;

/// <summary>
/// キャンセル可能タスクのインタフェース
/// </summary>
public interface ITaskCancellable
{
    /// <summary>
    /// キャンセル済みかどうかを取得する
    /// </summary>
    public bool IsCancelled { get; }
}
