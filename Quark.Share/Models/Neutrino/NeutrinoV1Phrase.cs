using System.Diagnostics.CodeAnalysis;
using Quark.Constants;
using Quark.Projects.Tracks;
using Quark.Utils;

namespace Quark.Models.Neutrino;

public class NeutrinoV1Phrase : INeutrinoPhrase
{
    public const int FramePeriod = NeutrinoConfig.FramePeriod;

    public const int MgcDimension = NeutrinoConfig.MgcDimension;

    public int No { get; }

    public int BeginTime { get; private set; }

    public int EndTime { get; private set; }

    public string[][] Phonemes { get; }

    public PhraseStatus Status { get; private set; }

    public double[]? F0 { get; private set; }

    public double[]? Mgc { get; private set; }

    public double[]? Bap { get; private set; }

    public double?[]? EditedF0 { get; private set; }

    public double?[]? EditedDynamics { get; private set; }

    public DateTime LastUpdated { get; private set; }

    public NeutrinoV1Phrase(int no, int beginTime, int endTime, string[][] label, PhraseStatus status)
    {
        this.No = no;
        this.BeginTime = beginTime;
        this.EndTime = endTime;
        this.Phonemes = label;
        this.Status = status;
    }

    public override string ToString()
        => $"{nameof(NeutrinoV1Phrase)} {{ No: {this.No}, BeginTime: {this.BeginTime}, EndTime: {this.EndTime}, Label: {this.Phonemes}, Status: {this.Status} }}";

    public void SetAudioFeatures(double[] f0, double[] mgc, double[] bap)
    {
        (this.F0, this.Mgc, this.Bap) = (f0, mgc, bap);
        this.EditedF0 ??= new double?[f0.Length];
        this.EditedDynamics ??= new double?[mgc.Length];
    }

    public void SetEdited(double?[]? f0, double?[]? dynamics)
    {
        this.EditedF0 = f0;
        this.EditedDynamics = dynamics;
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
    public double[]? GetEditedF0()
    {
        double[]? srcF0 = this.F0;
        double?[]? edited = this.EditedF0;

        // 編集対象が未設定(null)であればnullを返す
        // 未編集であれば、編集前の値を返す
        if (srcF0 == null)
            return null;
        else if (ArrayUtil.IsNullOrEmpty(edited))
            return srcF0;

        // F0をコピーし、各フレームのF0値を編集後の値に書き換える
        double[] destF0 = ArrayUtil.Clone(srcF0);
        int frameLength = Math.Min(edited.Length, srcF0.Length);
        for (int frameIdx = 0; frameIdx < frameLength; ++frameIdx)
            if (edited[frameIdx] is { } value)
                destF0[frameIdx] = value;

        return destF0;
    }

    /// <summary>
    /// 編集内容を反映したMGCを取得する。
    /// </summary>
    /// <returns></returns>
    [return: NotNullIfNotNull(nameof(this.Mgc))]
    public double[]? GetEditedMgc()
    {
        double[]? srcMgc = this.Mgc;
        double?[]? editedDynamics = this.EditedDynamics;

        // 編集対象が未設定(null)であればnullを返す
        // 未編集であれば、編集前の値を返す
        if (srcMgc == null)
            return null;
        else if (ArrayUtil.IsNullOrEmpty(editedDynamics))
            return srcMgc;

        // MGCをコピーし、フレーム毎の先頭の値を編集後の値に置き換える
        double[] destMgc = ArrayUtil.Clone(srcMgc);
        int frameLength = Math.Min(editedDynamics.Length, srcMgc.Length / MgcDimension);
        for (int frameIdx = 0; frameIdx < frameLength; ++frameIdx)
            if (editedDynamics[frameIdx] is { } value)
                destMgc[frameIdx * MgcDimension] = value;

        return destMgc;
    }

    public void EditF0(int time, double frequency)
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
    public void EditDynamics(int time, double value)
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
