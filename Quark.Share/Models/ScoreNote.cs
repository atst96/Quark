using Quark.Models.MusicXML;

namespace Quark.Models;

/// <summary>
/// 音符情報
/// </summary>
public class ScoreNote
{
    /// <summary>楽譜全体の音符のインデックス</summary>
    public required int Index { get; set; }

    /// <summary>小節のインデックス</summary>
    public required int MeasureIdx { get; init; }

    /// <summary>小節内の先頭からの音符の位置(Divisions単位)</summary>
    public required int Offset { get; init; }

    /// <summary>開始フレーム</summary>
    public required int BeginTime { get; init; }

    /// <summary>終了フレーム</summary>
    public required int EndTime { get; set; }

    /// <summary>歌詞</summary>
    public required string Lyrics { get; init; }

    /// <summary>ピッチ</summary>
    public required int Pitch { get; init; }

    /// <summary>ブレス記号</summary>
    public required bool IsBreath { get; set; }

    public required List<Note> Notes { get; init; } = [];

    public void SetEndFrame(int endFrame)
        => this.EndTime = endFrame;

    public void SetBreath(bool breath)
        => this.IsBreath = breath;

}
