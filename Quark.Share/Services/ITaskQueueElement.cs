namespace Quark.Services;

/// <summary>
/// TaskQueueの要素
/// </summary>
public interface ITaskQueueElement
{
    /// <summary></summary>
    public bool IsRunningOrRunnable { get; }

    /// <summary>実行中のタスク</summary>
    public Task? Task { get; }

    /// <summary>タスクをキャンセルする</summary>
    public void Cancel();
}
