using System.Runtime.CompilerServices;
using Quark.Projects.Tracks;

namespace Quark.Services;

/// <summary>
/// 推論キュー情報
/// </summary>
public class EstimateQueueInfo
{
    /// <summary>トラック</summary>
    public INeutrinoTrack Track { get; }

    /// <summary>フレーズ情報</summary>
    public INeutrinoPhrase Phrase { get; }

    /// <summary>優先度</summary>
    public EstimatePriority Priority { get; }

    /// <summary>キャンセル用トークン</summary>
    private readonly CancellationTokenSource _tokenSource = new();

    public EstimateQueueInfo(INeutrinoTrack track, INeutrinoPhrase phrase, EstimatePriority priority)
    {
        this.Track = track;
        this.Phrase = phrase;
        this.Priority = priority;
    }

    /// <summary>キャンセル用トークンを取得する</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CancellationToken GetCancellationToken() => this._tokenSource.Token;

    /// <summary>現在のタスクをキャンセルする</summary>
    public void Cancel()
        => this._tokenSource.Cancel();
}
