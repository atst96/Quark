namespace Quark.Controls.Editing;

/// <summary>
/// 編集情報のインタフェース
/// </summary>
public interface IEditInfo
{
    /// <summary>編集モード</summary>
    public EditInfoType Type { get; }
}
