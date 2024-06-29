using System.Diagnostics.CodeAnalysis;
using Quark.Components;
using Quark.Constants;
using Quark.Converters;
using Quark.Projects.Tracks;
using Quark.Utils;

namespace Quark.Models.Neutrino;

internal class NeutrinoV2Phrase : INeutrinoPhrase, IF0Phrase<float>, IMspecDynamicsPhrase<float>
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

    /// <summary>編集中のF0値</summary>
    public float[]? EditingF0 { get; private set; }

    /// <summary>編集済みF0値</summary>
    public float[]? EditedF0 { get; private set; }

    /// <summary>編集中のダイナミクス値</summary>
    public float[]? EditingDynamics { get; private set; }

    /// <summary>編集後のダイナミクス値</summary>
    public float[]? EditedDynamics { get; private set; }

    public DateTime LastUpdated { get; private set; }

    /// <summary>推論モード</summary>
    public EstimateMode? EstimateMode { get; private set; } = null;

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

    public void SetAudioFeatures(EstimateMode estiamteMode, float[] f0, float[] mspec, float[] mgc, float[] bap)
    {
        this.EstimateMode = estiamteMode;
        (this.F0, this.Mspec, this.Mgc, this.Bap) = (f0, mspec, mgc, bap);

        this.EditedF0 ??= ArrayUtil.Create(f0.Length, float.NaN);
        this.EditedDynamics ??= ArrayUtil.Create(mspec.Length / NeutrinoConfig.MspecDimension, float.NaN);
    }

    public void SetEdited(float[]? editedF0, float[]? editedDynamics)
    {
        this.EditedF0 = editedF0 == null && this.F0 is { } f0
            ? ArrayUtil.Create(f0.Length, float.NaN)
            : editedF0;

        this.EditedDynamics = editedDynamics == null && this.EditedDynamics is { } dynamics
            ? ArrayUtil.Create(dynamics.Length, float.NaN)
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
        this.EditingF0 = null;
        this.EditedF0 = null;
        this.EditingDynamics = null;
        this.EditedDynamics = null;
    }

    private static float[]? MergeF0(float[]? srcF0, float[]? edited)
    {
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
        {
            float value = edited[frameIdx];
            if (!float.IsNaN(value))
                destF0[frameIdx] = value;
        }

        return destF0;
    }

    /// <summary>
    /// 編集中の内容を反映したF0を取得する。
    /// </summary>
    /// <returns></returns>
    [return: NotNullIfNotNull(nameof(F0))]
    public float[]? GetEditingF0()
        => MergeF0(this.F0, this.EditingF0 ?? this.EditedF0);

    /// <summary>
    /// 編集内容を反映したF0を取得する。
    /// </summary>
    /// <returns></returns>
    [return: NotNullIfNotNull(nameof(F0))]
    public float[]? GetEditedF0()
        => MergeF0(this.F0, this.EditedF0);

    /// <summary>
    /// 編集内容を反映したMspecを取得する。
    /// </summary>
    /// <param name="origMspec"></param>
    /// <param name="dynamics"></param>
    /// <returns></returns>
    public static float[]? MergeMspec(float[]? origMspec, float[]? dynamics)
    {
        // 編集対象が未設定(null)であればnullを返す
        // 未編集であれば、編集前の値を返す
        if (origMspec == null)
            return null;
        else if (ArrayUtil.IsNullOrEmpty(dynamics))
            return origMspec;

        // Mspecをコピーし、各フレームごとの全要素に"編集後の値と編集前の平均値との差分"を加算する
        float[] destMspec = ArrayUtil.Clone(origMspec);
        int frameLength = Math.Min(dynamics.Length, origMspec.Length / MspecDimension);
        for (int frameIdx = 0; frameIdx < frameLength; ++frameIdx)
        {
            float value = dynamics[frameIdx];
            if (!float.IsNaN(value))
            {
                var frameValues = destMspec.AsSpan(frameIdx * MspecDimension, MspecDimension);
                frameValues.Add(value - frameValues.Average());
            }
        }

        return destMspec;
    }

    /// <summary>
    /// 編集中内容を反映したMspecを取得する。
    /// </summary>
    /// <returns></returns>
    [return: NotNullIfNotNull(nameof(this.Mspec))]
    public float[]? GetEditingMspec()
        => MergeMspec(this.Mspec, this.EditingDynamics ?? this.EditedDynamics);

    /// <summary>
    /// 編集内容を反映したMspecを取得する。
    /// </summary>
    /// <returns></returns>
    [return: NotNullIfNotNull(nameof(this.Mspec))]
    public float[]? GetEditedMspec()
        => MergeMspec(this.Mspec, this.EditedDynamics);

    /// <summary>
    /// 編集中のF0値を取得する。編集情報がなければ編集後情報から生成する。
    /// </summary>
    /// <returns></returns>
    private Span<float> GetF0ForEdit()
        => this.EditingF0 ??= ArrayUtil.Clone(this.EditedF0);

    /// <summary>
    /// F0値を編集情報に追加する。
    /// </summary>
    /// <param name="editBeginTime">編集開始時間</param>
    /// <param name="frequencies">編集データ</param>
    public void EditF0(int editBeginTime, Span<float> frequencies)
    {
        Span<float> f0 = this.GetF0ForEdit();
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
    /// ピッチに12音階の値を加算する
    /// </summary>
    /// <param name="editBeginTime"></param>
    /// <param name="pitches"></param>
    public void AddPitch12(int editBeginTime, Span<float> pitches)
    {
        Span<float> f0 = this.GetF0ForEdit();
        Span<float> editedF0 = this.GetEditingF0();
        if (pitches.Length < 1 || f0.Length < 1)
            return;

        int editBeginFrameIdx = NeutrinoUtil.MsToFrameIndex(editBeginTime);
        int editEndFrameIdx = editBeginFrameIdx + pitches.Length;

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
        for (int idx = 0; idx < framesCount; ++idx)
        {
            float previousPitch = f0[startIdx + idx];
            if (float.IsNaN(previousPitch))
                previousPitch = editedF0[startIdx + idx];

            f0[startIdx + idx] = AudioDataConverter.Pitch12ToFrequency(
                AudioDataConverter.FrequencyToPitch12(previousPitch) + pitches[startIdx - relativeFrequencyIdx + idx]);
        }

        this.OnUpdated();
    }

    /// <summary>
    /// 編集中のダイナミクス値を取得する。編集情報がなければ編集後情報から生成する。
    /// </summary>
    /// <returns></returns>
    private Span<float> GetDynamicsForEdit()
        => this.EditingDynamics ??= ArrayUtil.Clone(this.EditedDynamics);

    /// <summary>
    /// ダイナミクス値を編集情報に追加する。
    /// </summary>
    /// <param name="editBeginTime">編集開始時間</param>
    /// <param name="dynamicsValues">編集データ</param>
    public void EditDynamics(int editBeginTime, Span<float> dynamicsValues)
    {
        Span<float> dynamics = this.GetDynamicsForEdit();
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
    /// ダイナミクスの編集値に加算する(編集値の範囲は0.0～1.0)
    /// </summary>
    /// <param name="editBeginTime"></param>
    /// <param name="dynamicsCoeDelta"></param>
    public void AddDynamicsCoe(int editBeginTime, float[] dynamicsCoeDelta)
    {
        Span<float> editingDynamics = this.GetDynamicsForEdit();
        Span<float> editingMspec = this.GetEditingMspec();
        if (dynamicsCoeDelta.Length < 1 || editingDynamics.Length < 1)
            return;

        int editBeginFrameIdx = NeutrinoUtil.MsToFrameIndex(editBeginTime);
        int editEndFrameIdx = editBeginFrameIdx + dynamicsCoeDelta.Length;

        int phraseBeginIdx = NeutrinoUtil.MsToFrameIndex(this.BeginTime);

        // 編集データ全体の開始時間(beginFrameIdx)とフレーズの開始時間(phraseBeginIdx)の差異
        int relativeFrequencyIdx = editBeginFrameIdx - phraseBeginIdx;

        // フレーズにおけるデータの編集範囲
        int startIdx = Math.Max(0, relativeFrequencyIdx);
        int endIdx = Math.Min(editingDynamics.Length, editEndFrameIdx - phraseBeginIdx);

        int framesCount = endIdx - startIdx;
        if (framesCount < 1)
            return; // 編集対象がなければ処理を抜ける

        // 編集内容を反映する
        for (int idx = 0; idx < framesCount; ++idx)
        {
            float currentDynamics = editingDynamics[startIdx + idx];
            if (float.IsNaN(currentDynamics))
                currentDynamics = editingMspec.Slice((startIdx + idx) * NeutrinoConfig.MspecDimension, NeutrinoConfig.MspecDimension).Average();

            editingDynamics[startIdx + idx] = NeutrinoUtil.LinearMspecCoeToLogValue(
                NeutrinoUtil.MspecToCoe(currentDynamics) + dynamicsCoeDelta[startIdx - relativeFrequencyIdx + idx]);
        }

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

        dynamics[index] = float.NaN;

        this.OnUpdated();
    }

    /// <summary>
    /// F0値が編集中かどうかを取得する。
    /// </summary>
    public bool IsF0Editing()
        => this.EditingF0 != null;

    /// <summary>
    /// 編集中のF0値を編集済み値に反映する。
    /// </summary>
    public void DetermineEditingF0()
    {
        if (this.EditingF0 is { } editing)
        {
            (this.EditedF0, this.EditingF0) = (editing, null);
            this.OnAudioFetureDetermined();
        }
    }

    /// <summary>
    /// 編集中のF0値を編集済み値に反映する。
    /// </summary>
    public void CancelEditingF0()
        => this.EditingF0 = null;

    /// <summary>
    /// ダイナミクス値が編集中かどうかを取得する。
    /// </summary>
    /// <returns></returns>
    public bool IsDynamicsEditing()
        => this.EditingDynamics != null;

    /// <summary>
    /// 編集中のF0値を編集済み値に反映する。
    /// </summary>
    public void DetermineEditingDynamics()
    {
        if (this.EditingDynamics is { } editing)
        {
            (this.EditedDynamics, this.EditingDynamics) = (editing, null);
            this.OnAudioFetureDetermined();
        }
    }

    /// <summary>
    /// 編集中のダイナミクス値を編集済み値に反映する。
    /// </summary>
    public void CancelEditingDynamics()
        => this.EditingDynamics = null;

    private void OnAudioFetureDetermined()
    {
        switch (this.Status)
        {
            case PhraseStatus.WaitEstimate:
            case PhraseStatus.EstimateError:
            case PhraseStatus.EstimateProcessing:
                return;
        }

        this.Status = PhraseStatus.WaitAudioRender;
    }

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
