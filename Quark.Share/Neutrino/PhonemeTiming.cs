namespace Quark.Neutrino;

/// <summary>
/// 音素のタイミング情報
/// </summary>
/// <param name="TimeMs">音素の開始時間</param>
/// <param name="EditedTimeMs">編集後の音素の開始時間</param>
/// <param name="Phoneme"></param>
/// <param name="PhraseNo">音素が含まれるフレーズのインデックス</param>
public class PhonemeTiming(int TimeMs, int EditedTimeMs, string Phoneme, int PhraseNo)
{
    /// <summary>音素の位置(ミリ秒)</summary>
    public int TimeMs { get; } = TimeMs;

    /// <summary>編集中の音素の位置(ミリ秒)</summary>
    public int EditingTimeMs { get; set; } = EditedTimeMs;

    /// <summary>編集後の音素の位置(ミリ秒)</summary>
    public int EditedTimeMs { get; set; } = EditedTimeMs;

    /// <summary>音素</summary>
    public string Phoneme { get; } = Phoneme;

    /// <summary>音素が含まれるフレーズのインデックス</summary>
    public int PhraseIndex { get; } = PhraseNo;

    /// <summary>
    /// 編集を開始する
    /// </summary>
    public void BeginEdit()
    {
        this.EditingTimeMs = this.EditedTimeMs;
    }

    /// <summary>
    /// 編集を完了して編集後の値を確定する
    /// </summary>
    public void DetermineEdit()
    {
        this.EditedTimeMs = this.EditingTimeMs;
    }

    /// <summary>
    /// 編集内容をキャンセルして編集前の値に戻す
    /// </summary>
    public void CancelEdit()
    {
        this.EditingTimeMs = this.EditedTimeMs;
    }
}
