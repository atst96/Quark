using Quark.Components;
using Quark.Models.Neutrino;

namespace Quark.Projects.Tracks;

public interface INeutrinoPhrase
{
    public int No { get; }

    public int BeginTime { get; }

    public int EndTime { get; }

    public string[][] Phonemes { get; }

    public PhraseStatus Status { get; }

    public void SetStatus(PhraseStatus status);

    /// <summary>
    /// F0値が編集中かどうかを取得する。
    /// </summary>
    public bool IsF0Editing();

    /// <summary>
    /// 編集中のF0値を編集済み値に反映する。
    /// </summary>
    /// <returns></returns>
    public void DetermineEditingF0();

    /// <summary>
    /// 編集中のF0値を編集済み値に反映する。
    /// </summary>
    public void CancelEditingF0();

    /// <summary>
    /// ダイナミクス値が編集中かどうかを取得する。
    /// </summary>
    /// <returns></returns>
    public bool IsDynamicsEditing();

    /// <summary>
    /// 編集中のダイナミクス値を編集済み値に反映する。
    /// </summary>
    public void DetermineEditingDynamics();

    /// <summary>
    /// 編集中のダイナミクス値を編集済み値に反映する。
    /// </summary>
    public void CancelEditingDynamics();

    /// <summary>
    /// 音響情報が編集中かどうかを取得する。
    /// </summary>
    /// <returns></returns>
    public bool IsAudioFeatureEditing();

    /// <summary>
    /// 編集中の音響情報を編集済みに反映する。
    /// </summary>
    public void DetermineEditingAudioFeatures()
    {
        this.DetermineEditingF0();
        this.DetermineEditingDynamics();
    }

    /// <summary>推論モード</summary>
    public EstimateMode? EstimateMode { get; }

    public DateTime LastUpdated { get; }
}
