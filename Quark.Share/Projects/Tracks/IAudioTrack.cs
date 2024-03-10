using Quark.Audio;

namespace Quark.Projects.Tracks;

/// <summary>
/// 音声出力トラック
/// </summary>
public interface IAudioTrack
{
    public string TrackName { get; }

    /// <summary>(共通)トラックID</summary>
    public string TrackId { get; }

    /// <summary>音声ストリーム</summary>
    public TrackSampleProvider AudioStream { get; }

    /// <summary>ミュート状態</summary>
    public bool IsMute { get; set; }

    /// <summary>ボリューム(0.0～1.0)</summary>
    public float Volume { get; set; }
}
