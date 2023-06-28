using Quark.Projects.Tracks;

namespace Quark.Models.Neutrino;

internal class NeutrinoV2Phrase : INeutrinoPhrase
{
    public int No { get; }

    public int BeginTime { get; private set; }

    public int EndTime { get; private set; }

    public string[][] Phonemes { get; }

    public PhraseStatus Status { get; private set; }

    public float[]? Mspec { get; private set; }

    public float[]? F0 { get; private set; }

    public float[]? Mgc { get; private set; }

    public float[]? Bap { get; private set; }

    public NeutrinoV2Phrase(int no, int beginTime, int endTime, string[][] label, PhraseStatus status)
    {
        this.No = no;
        this.BeginTime = beginTime;
        this.EndTime = endTime;
        this.Phonemes = label;
        this.Status = status;
    }

    public override string ToString()
        => $"{nameof(NeutrinoV1Phrase)} {{ No: {this.No}, BeginTime: {this.BeginTime}, EndTime: {this.EndTime}, Label: {this.Phonemes}, Status: {this.Status} }}";

    public void SetAudioFeatures(float[] f0, float[] mspec, float[] mgc, float[] bap)
        => (this.F0, this.Mspec, this.Mgc, this.Bap) = (f0, mspec, mgc, bap);

    public void SetStatus(PhraseStatus status)
        => this.Status = status;

    /// <summary>
    /// フレーズの開始位置を変更する
    /// </summary>
    /// <param name="beginTime">開始時間</param>
    /// <param name="isClearAudioFeature">音響情報のクリアフラグ</param>
    public void ChangeBeginTime(int beginTime, bool isClearAudioFeature = true)
    {
        this.BeginTime = beginTime;

        if (isClearAudioFeature)
            this.ClearAudioFeatures();
    }

    /// <summary>
    /// フレーズの終了位置を変更する
    /// </summary>
    /// <param name="endTime">終了時刻</param>
    /// <param name="isClearAudioFeature">音響情報のクリアフラグ</param>
    public void ChangeEndTime(int endTime, bool isClearAudioFeature = true)
    {
        this.EndTime = endTime;

        if (isClearAudioFeature)
            this.ClearAudioFeatures();
    }

    /// <summary>
    /// 音響情報をクリアする
    /// </summary>
    public void ClearAudioFeatures()
    {
        this.F0 = null;
        this.Mspec = null;
        this.Mgc = null;
        this.Bap = null;
    }
}
