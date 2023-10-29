using MemoryPack;
using Quark.Data.Settings;
using Quark.Models.Neutrino;

namespace Quark.Data.Projects.Neutrino;

[MemoryPackable]
public partial class AudioFeaturesV2Config
{
    public AudioFeaturesV2Config(string modelId)
    {
        this.ModelId = modelId;
    }

    [MemoryPackOrder(0)]
    public string ModelId { get; }

    [MemoryPackOrder(1)]
    public required TimingInfo[]? Timing { get; set; }

    [MemoryPackOrder(2)]
    public required PhraseInfo[] RawPhraseInfo { get; set; }

    [MemoryPackOrder(3)]
    public required PhraseInfoV2[] Phrases { get; set; }

    [MemoryPackOrder(4)]
    private uint _versionId;

    [MemoryPackOnDeserializing]
    private void OnDeserializing()
    {
        // MEMO: 項目の増減時にインクリメントする
        this._versionId = 0;
    }
}
