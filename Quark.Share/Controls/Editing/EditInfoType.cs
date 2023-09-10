namespace Quark.Controls.Editing;

/// <summary>
/// エディタの編集中モード
/// </summary>
public enum EditInfoType
{
    /// <summary>タイミング情報編集</summary>
    Timing,

    /// <summary>ピッチ編集</summary>
    Pitch,

    /// <summary>ダイナミクス編集</summary>
    Dynamics,

    /// <summary>範囲選択</summary>
    RangeSelect,

    /// <summary>ピッチの一括編集</summary>
    SeekPitch,

    /// <summary>ダイナミクスの一括編集</summary>
    SeekDynamics,
}
