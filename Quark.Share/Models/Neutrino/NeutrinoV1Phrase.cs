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

    /// <summary>編集中のF0値</summary>
    public double[]? EditingF0 { get; private set; }

    /// <summary>編集済みF0値</summary>
    public double[]? EditedF0 { get; private set; }

    /// <summary>編集中のダイナミクス値</summary>
    public double[]? EditingDynamics { get; private set; }

    /// <summary>編集後のダイナミクス値</summary>
    public double[]? EditedDynamics { get; private set; }

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
        this.EditedF0 ??= ArrayUtil.Create(f0.Length, double.NaN);
        this.EditedDynamics ??= ArrayUtil.Create(mgc.Length / NeutrinoConfig.MgcDimension, double.NaN);
    }

    public void SetEdited(double[]? f0, double[]? dynamics)
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
        this.EditingF0 = null;
        this.EditedF0 = null;
        this.EditingDynamics = null;
        this.EditedDynamics = null;
    }

    /// <summary>
    /// F0値に編集内容を反映する。
    /// </summary>
    /// <param name="origF0">未編集のF0値</param>
    /// <param name="newF0">編集後のF0値</param>
    /// <returns></returns>
    private static double[]? MergeF0(double[]? origF0, double[]? newF0)
    {
        // 編集対象が未設定(null)であればnullを返す
        // 未編集であれば、編集前の値を返す
        if (origF0 == null)
            return null;
        else if (ArrayUtil.IsNullOrEmpty(newF0))
            return origF0;

        // F0をコピーし、各フレームのF0値を編集後の値に書き換える
        double[] destF0 = ArrayUtil.Clone(origF0);
        int frameLength = Math.Min(newF0.Length, origF0.Length);
        for (int frameIdx = 0; frameIdx < frameLength; ++frameIdx)
        {
            double value = newF0[frameIdx];
            if (!double.IsNaN(value))
                destF0[frameIdx] = value;
        }

        return destF0;
    }

    /// <summary>
    /// 編集中の内容を反映したF0を取得する。
    /// </summary>
    /// <returns></returns>
    [return: NotNullIfNotNull(nameof(F0))]
    public double[]? GetEditingF0()
        => MergeF0(this.F0, (this.EditingF0 ?? this.EditedF0));

    /// <summary>
    /// 編集内容を反映したF0を取得する。
    /// </summary>
    /// <returns></returns>
    [return: NotNullIfNotNull(nameof(F0))]
    public double[]? GetEditedF0()
        => MergeF0(this.F0, this.EditedF0);

    /// <summary>
    /// 編集内容を反映したMGCを取得する。
    /// </summary>
    /// <returns></returns>
    [return: NotNullIfNotNull(nameof(this.Mgc))]
    public double[]? GetEditedMgc()
    {
        double[]? srcMgc = this.Mgc;
        double[]? editedDynamics = this.EditedDynamics;

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
        {
            double value = editedDynamics[frameIdx];

            if (!double.IsNaN(value))
                destMgc[frameIdx * MgcDimension] = value;
        }

        return destMgc;
    }

    /// <summary>
    /// 編集中のF0値を取得する。編集情報がなければ編集後情報から生成する。
    /// </summary>
    /// <returns></returns>
    private Span<double> GetF0ForEdit()
        => this.EditingF0 ??= ArrayUtil.Clone(this.EditedF0);

    /// <summary>
    /// F0値を編集情報に追加する。
    /// </summary>
    /// <param name="editBeginTime">編集開始時間</param>
    /// <param name="frequencies">編集データ</param>
    public void EditF0(int editBeginTime, Span<double> frequencies)
    {
        Span<double> f0 = this.GetEditedF0();
        if (frequencies.Length < 1 || f0.Length < 1)
            return;

        int editBeginFrameIdx = NeutrinoUtil.MsToFrameIndex(editBeginTime);
        int editEndFrameIdx = editBeginFrameIdx + frequencies.Length;

        int phraseBeginIdx = NeutrinoUtil.MsToFrameIndex(this.BeginTime);

        // 編集データ全体の開始時間(beginFrameIdx)とフレーズの開始時間(phraseBeginIdx)の差異
        int relativeFrequencyIdx = editBeginFrameIdx - phraseBeginIdx;

        // フレーズにおけるデータの編集範囲
        int startIdx = Math.Max(0, relativeFrequencyIdx);
        int endIdx = Math.Min(f0.Length, editEndFrameIdx - phraseBeginIdx);

        int framesCount = endIdx - startIdx;
        if (framesCount < 1)
            return; // 編集対象がなければ処理を抜ける

        // 編集内容を反映する
        frequencies.Slice(startIdx - relativeFrequencyIdx, framesCount).CopyTo(f0[startIdx..]);

        this.OnUpdated();
    }

    /// <summary>
    /// 編集中のダイナミクス値を取得する。編集情報がなければ編集後情報から生成する。
    /// </summary>
    /// <returns></returns>
    private Span<double> GetDynamicsForEdit()
        => this.EditingDynamics ??= ArrayUtil.Clone(this.EditedDynamics);

    /// <summary>
    /// ダイナミクス値を編集情報に追加する。
    /// </summary>
    /// <param name="editBeginTime">編集開始時間</param>
    /// <param name="dynamicsValues">編集データ</param>
    public void EditDynamics(int editBeginTime, Span<double> dynamicsValues)
    {
        Span<double> dynamics = this.GetDynamicsForEdit();
        if (dynamicsValues.Length < 1 || dynamics.Length < 1)
            return;

        int editBeginFrameIdx = NeutrinoUtil.MsToFrameIndex(editBeginTime);
        int editEndFrameIdx = editBeginFrameIdx + dynamicsValues.Length;

        int phraseBeginIdx = NeutrinoUtil.MsToFrameIndex(this.BeginTime);

        // 編集データ全体の開始時間(beginFrameIdx)とフレーズの開始時間(phraseBeginIdx)の差異
        int relativeFrequencyIdx = editBeginFrameIdx - phraseBeginIdx;

        // フレーズにおけるデータの編集範囲
        int startIdx = Math.Max(0, relativeFrequencyIdx);
        int endIdx = Math.Min(dynamics.Length, editEndFrameIdx - phraseBeginIdx);

        int framesCount = endIdx - startIdx;
        if (framesCount < 1)
            return; // 編集対象がなければ処理を抜ける

        // 編集内容を反映する
        dynamicsValues.Slice(startIdx - relativeFrequencyIdx, framesCount).CopyTo(dynamics[startIdx..]);

        this.OnUpdated();
    }

    /// <summary>
    /// 音の強さを編集する。
    /// </summary>
    /// <param name="time">編集時点</param>
    /// <param name="value">値</param>
    [Obsolete]
    public void EditDynamics(int time, double value)
    {
        var dynamics = this.GetEditedMgc();
        if (ArrayUtil.IsNullOrEmpty(dynamics) || !(this.BeginTime <= time && time < this.EndTime))
            return;

        int index = (time - this.BeginTime) / FramePeriod;

        if (dynamics.Length < index)
            return;

        dynamics[index] = value;

        this.OnUpdated();
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

        dynamics[index] = double.NaN;

        this.OnUpdated();
    }

    /// <summary>
    /// F0値が編集中かどうかを取得する。
    /// </summary>
    public bool IsF0Editing()
        => this.EditedF0 != null;

    /// <summary>
    /// 編集中のF0値を編集済み値に反映する。
    /// </summary>
    public void DetermineEditingF0()
        => (this.EditedF0, this.EditingF0) = (this.EditingF0, null);

    /// <summary>
    /// ダイナミクス値が編集中かどうかを取得する。
    /// </summary>
    /// <returns></returns>
    public bool IsDynamicsEditing()
        => this.EditedDynamics != null;

    /// <summary>
    /// 編集中のF0値を編集済み値に反映する。
    /// </summary>
    public void DetermineEditingDynamics()
        => (this.EditedDynamics, this.EditingDynamics) = (this.EditingDynamics, null);

    /// <summary>
    /// 音響情報が編集中かどうかを取得する。
    /// </summary>
    /// <returns></returns>
    public bool IsAudioFeatureEditing()
        => this.IsF0Editing() || this.IsDynamicsEditing();

    private void OnUpdated()
    {
        this.LastUpdated = DateTime.Now;
    }
}
