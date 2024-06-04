namespace Quark.Renderers;

/// <summary>
/// 描画対象の線の状態
/// </summary>
public enum LineRenderType
{
    /// <summary>未編集</summary>
    NotEdit,

    /// <summary>編集済み</summary>
    Edited,

    /// <summary>編集中</summary>
    Editing,
}
