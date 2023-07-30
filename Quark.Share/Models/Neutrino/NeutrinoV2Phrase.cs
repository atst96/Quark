using System.Diagnostics.CodeAnalysis;
using Quark.Constants;
using Quark.Projects.Tracks;
using Quark.Utils;

namespace Quark.Models.Neutrino;

internal class NeutrinoV2Phrase : INeutrinoPhrase
{
    public const int FramePeriod = NeutrinoConfig.FramePeriod;

    private const int MspecDimension = NeutrinoConfig.MspecDimension;

    public int No { get; }

    public int BeginTime { get; private set; }

    public int EndTime { get; private set; }

    public string[][] Phonemes { get; }

    public PhraseStatus Status { get; private set; }

    public float[]? Mspec { get; private set; }

    public float[]? F0 { get; private set; }

    public float[]? Mgc { get; private set; }

    public float[]? Bap { get; private set; }

    public float?[]? EditedF0 { get; private set; }

    public float?[]? EditedDynamics { get; private set; }

    public DateTime LastUpdated { get; private set; }

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
    {
        (this.F0, this.Mspec, this.Mgc, this.Bap) = (f0, mspec, mgc, bap);

        this.EditedF0 ??= new float?[f0.Length];
        this.EditedDynamics ??= new float?[mspec.Length];
    }

    public void SetEdited(float?[]? editedF0, float?[]? editedDynamics)
    {
        this.EditedF0 = editedF0 == null && this.F0 is { } f0
            ? new float?[f0.Length]
            : editedF0;

        this.EditedDynamics = editedDynamics == null && this.EditedDynamics is { } dynamics
            ? new float?[dynamics.Length]
            : editedDynamics;
    }

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
        this.EditedF0 = null;
        this.EditedDynamics = null;
    }

    /// <summary>
    /// 編集内容を反映したF0を取得する。
    /// </summary>
    /// <returns></returns>
    [return: NotNullIfNotNull(nameof(F0))]
    public float[]? GetEditedF0()
    {
        float[]? srcF0 = this.F0;
        float?[]? edited = this.EditedF0;

        // 編集対象が未設定(null)であればnullを返す
        // 未編集であれば、編集前の値を返す
        if (srcF0 == null)
            return null;
        else if (ArrayUtil.IsNullOrEmpty(edited))
            return srcF0;

        // F0をコピーし、各フレームのF0値を編集後の値に書き換える
        float[] destF0 = ArrayUtil.Clone(srcF0);
        int frameLength = Math.Min(edited.Length, srcF0.Length);
        for (int frameIdx = 0; frameIdx < frameLength; ++frameIdx)
            if (edited[frameIdx] is { } value)
                destF0[frameIdx] = value;

        return destF0;
    }

    /// <summary>
    /// 編集内容を反映したMspecを取得する。
    /// </summary>
    /// <returns></returns>
    [return: NotNullIfNotNull(nameof(this.Mspec))]
    public float[]? GetEditedMspec()
    {
        float[]? srcMspec = this.Mspec;
        float?[]? edited = this.EditedDynamics;

        // 編集対象が未設定(null)であればnullを返す
        // 未編集であれば、編集前の値を返す
        if (srcMspec == null)
            return null;
        else if (ArrayUtil.IsNullOrEmpty(edited))
            return srcMspec;

        // Mspecをコピーし、各フレームごとの全要素に"編集後の値と編集前の平均値との差分"を加算する
        float[] destMspec = ArrayUtil.Clone(srcMspec);
        int frameLength = Math.Min(edited.Length, srcMspec.Length / MspecDimension);
        for (int frameIdx = 0; frameIdx < frameLength; ++frameIdx)
        {
            if (edited[frameIdx] is { } value)
            {
                var frameValues = destMspec.AsSpan(frameIdx * MspecDimension, MspecDimension);
                frameValues.Add(value - frameValues.Average());
            }
        }

        return destMspec;
    }

    internal void EditF0(int time, float frequency)
    {
        var f0 = this.EditedF0;
        if (ArrayUtil.IsNullOrEmpty(f0) || !(this.BeginTime <= time && time < this.EndTime))
            return;

        int index = (time - this.BeginTime) / FramePeriod;

        if (f0.Length < index)
            return;

        f0[index] = frequency;

        this.LastUpdated = DateTime.Now;
    }

    /// <summary>
    /// 音の強さを編集する。
    /// </summary>
    /// <param name="time">編集時点</param>
    /// <param name="value">値</param>
    public void EditDynamics(int time, float value)
    {
        var dynamics = this.EditedDynamics;
        if (ArrayUtil.IsNullOrEmpty(dynamics) || !(this.BeginTime <= time && time < this.EndTime))
            return;

        int index = (time - this.BeginTime) / FramePeriod;

        if (dynamics.Length < index)
            return;

        dynamics[index] = value;

        this.LastUpdated = DateTime.Now;
    }

    /// <summary>
    /// 編集した音の強さを元に戻す。
    /// </summary>
    /// <param name="time">編集時点</param>
    public void ClearDynamics(int time)
    {
        var dynamics = this.EditedDynamics;
        if (ArrayUtil.IsNullOrEmpty(dynamics) || !(this.BeginTime <= time && time < this.EndTime))
            return;

        int index = (time - this.BeginTime) / FramePeriod;
        if (dynamics.Length < index)
            return;

        dynamics[index] = null;

        this.LastUpdated = DateTime.Now;
    }
}
