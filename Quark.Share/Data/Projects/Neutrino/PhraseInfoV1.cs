using MemoryPack;
using Quark.Utils;

namespace Quark.Data.Projects.Neutrino;

[MemoryPackable(SerializeLayout.Explicit)]
public partial class PhraseInfoV1
{
    [MemoryPackOrder(0)]
    public int No { get; init; }

    [MemoryPackOrder(1)]
    public int BeginTime { get; init; }

    [MemoryPackOrder(2)]
    public int EndTime { get; init; }

    [MemoryPackOrder(3)]
    public string[][] Phonemes { get; init; }

    [MemoryPackOrder(4)]
    public double[]? F0 { get; init; }

    [MemoryPackOrder(5)]
    public double[]? Mgc { get; init; }

    [MemoryPackOrder(6)]
    public double[]? Bap { get; init; }

    [MemoryPackOrder(7)]
    public double?[]? OldEditedF0 { get; set; }

    [MemoryPackOrder(8)]
    public double?[]? OldEditedDynamics { get; set; }

    [MemoryPackOrder(9)]
    public double[]? EditedF0 { get; set; }

    [MemoryPackOrder(10)]
    public double[]? EditedDynamics { get; set; }

    /// <summary>
    /// シリアライズ後に呼ばれるメソッド
    /// </summary>
    [MemoryPackOnDeserialized]
    public void OnDeserialized()
    {
        if (this.OldEditedF0 != null)
        {
            this.EditedF0 ??= ArrayUtil.UnNullable(this.OldEditedF0, double.NaN);
            this.OldEditedF0 = null;
        }

        if (this.OldEditedDynamics != null)
        {
            this.EditedDynamics ??= ArrayUtil.UnNullable(this.OldEditedDynamics, double.NaN);
            this.OldEditedDynamics = null;
        }
    }
}
