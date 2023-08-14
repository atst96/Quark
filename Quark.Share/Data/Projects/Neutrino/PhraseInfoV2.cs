using MemoryPack;
using Quark.Utils;

namespace Quark.Data.Projects.Neutrino;

[MemoryPackable(SerializeLayout.Explicit)]
public partial class PhraseInfoV2
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
    public float[]? F0 { get; init; }

    [MemoryPackOrder(5)]
    public float[]? Mspec { get; init; }

    [MemoryPackOrder(6)]
    public float[]? Mgc { get; init; }

    [MemoryPackOrder(7)]
    public float[]? Bap { get; init; }

    [MemoryPackOrder(8)]
    public float?[]? OldEditedF0 { get; set; }

    [MemoryPackOrder(9)]
    public float?[]? OldEditedDynamics { get; set; }

    [MemoryPackOrder(10)]
    public float[]? EditedF0 { get; set; }

    [MemoryPackOrder(11)]
    public float[]? EditedDynamics { get; set; }

    /// <summary>
    /// シリアライズ後に呼ばれるメソッド
    /// </summary>
    [MemoryPackOnDeserialized]
    public void OnSerialized()
    {
        if (this.OldEditedF0 != null)
        {
            this.EditedF0 ??= ArrayUtil.UnNullable(this.OldEditedF0, float.NaN);
            this.OldEditedF0 = null;
        }

        if (this.OldEditedDynamics != null)
        {
            this.EditedDynamics ??= ArrayUtil.UnNullable(this.OldEditedDynamics, float.NaN);
            this.OldEditedDynamics = null;
        }
    }
}
