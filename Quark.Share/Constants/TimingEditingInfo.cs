namespace Quark.Constants;

/// <summary>
/// 編集中タイミング情報
/// </summary>
public class TimingEditingInfo
{
    /// <summary>編集対象要素</summary>
    public TimingHandle Target { get; }

    /// <summary>下限の時間</summary>
    public required long Lower { get; init; }

    /// <summary>上限の時間</summary>
    public required long? Upper { get; init; }

    /// <summary>
    /// X位置のオフセット
    /// 操作開始時の位置と当たり判定のマージンを考慮したマウス位置との差分
    /// </summary>
    public required float OffsetX { get; init; }

    /// <summary>タイミング編集中の時間</summary>
    public required int Time { get; set; }

    /// <summary>コンストラクタ</summary>
    /// <param name="target"></param>
    public TimingEditingInfo(TimingHandle target)
    {
        this.Target = target;
    }
}
