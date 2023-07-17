using Quark.Utils;

namespace Quark.Constants;

/// <summary>
/// 編集中タイミング情報
/// </summary>
public class TimingEditingInfo
{
    /// <summary>編集対象要素</summary>
    public TimingHandle Target { get; }

    /// <summary>下限の時間</summary>
    public required long LowerTime100Ns { get; init; }

    /// <summary>上限の時間</summary>
    public required long? UpperTime100Ns { get; init; }

    /// <summary>
    /// X位置のオフセット
    /// 操作開始時の位置と当たり判定のマージンを考慮したマウス位置との差分
    /// </summary>
    public required float OffsetX { get; init; }

    /// <summary>編集前の位置(時間)</summary>
    public long InitialTime100Ns { get; }

    /// <summary>タイミング編集中の位置(時間)</summary>
    public int CurrentTimeMs { get; set; }

    /// <summary>コンストラクタ</summary>
    /// <param name="target"></param>
    public TimingEditingInfo(TimingHandle target, long initialTime100Ns)
    {
        this.Target = target;
        this.InitialTime100Ns = initialTime100Ns;
        this.CurrentTimeMs = NeutrinoUtil.TimingTimeToMs(initialTime100Ns);
    }
}
