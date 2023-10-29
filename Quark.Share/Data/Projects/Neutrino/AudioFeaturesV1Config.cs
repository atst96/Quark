using MemoryPack;
using Quark.Data.Settings;
using Quark.Models.Neutrino;

namespace Quark.Data.Projects.Neutrino;

[MemoryPackable]
public partial class AudioFeaturesV1Config
{
    public AudioFeaturesV1Config(string modelId)
    {
        this.ModelId = modelId;
    }

    [MemoryPackOrder(0)]
    public string ModelId { get; }

    [MemoryPackOrder(1)]
    public required TimingInfo[] Timings { get; set; }

    [MemoryPackOrder(2)]
    public required PhraseInfo[] RawPhrases { get; set; }

    [MemoryPackOrder(3)]
    public required PhraseInfoV1[] Phrases { get; set; }

    [MemoryPackOrder(4)]
    private uint _versionId;

    public PhraseInfo[] RawPhrases { get; set; }

    [MemoryPackOnDeserializing]
    private void OnDeserializing()
    {
        // MEMO: 項目の増減時にインクリメントする
    }
}
