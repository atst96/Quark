namespace Quark.Controls;

/// <summary>
/// マウス制御モード
/// </summary>
public enum MouseControlMode
{
    /// <summary>操作なし</summary>
    None,

    /// <summary>シーク操作</summary>
    Seek,

    /// <summary>音符配置</summary>
    PutNote,

    /// <summary>タイミング編集</summary>
    EditTiming,

    /// <summary>ピッチ編集</summary>
    EditPitch,

    /// <summary>ダイナミクス編集</summary>
    EditDynamics,
}
