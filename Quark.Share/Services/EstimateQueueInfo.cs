using System.Runtime.CompilerServices;
using Quark.Projects.Tracks;

namespace Quark.Services;

/// <summary>
/// 推論キュー情報
/// </summary>
public class EstimateQueueInfo : ITaskQueueElement
{
    /// <summary>トラック</summary>
    public INeutrinoTrack Track { get; }

    /// <summary>フレーズ情報。トラック全体の場合はNULL</summary>
    public INeutrinoPhrase? Phrase { get; }

    /// <summary>優先度</summary>
    public EstimatePriority Priority { get; }

    /// <summary>実行中のタスク</summary>
    private Task? _task;

    /// <summary>実行中のタスク</summary>
    Task? ITaskQueueElement.Task => this._task;

    /// <summary>キャンセル用トークン</summary>
    private readonly CancellationTokenSource _tokenSource = new();

    public EstimateQueueInfo(INeutrinoTrack track, INeutrinoPhrase? phrase, EstimatePriority priority)
    {
        this.Track = track;
        this.Phrase = phrase;
        this.Priority = priority;
    }

    /// <summary>キャンセル用トークンを取得する</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CancellationToken GetCancellationToken() => this._tokenSource.Token;

    public bool IsRunningOrRunnable
        => this._task == null || this._task.Status is not (
        TaskStatus.WaitingForChildrenToComplete or TaskStatus.RanToCompletion or TaskStatus.Canceled or TaskStatus.Faulted);

    /// <summary>現在のタスクをキャンセルする</summary>
    public void Cancel()
        => this._tokenSource.Cancel();

    /// <summary>
    /// 実行中のタスクを設定する。
    /// </summary>
    /// <param name="task">実行中のタスク</param>
    internal void SetTask(Task task)
        => this._task = task;
}
