using MemoryPack;
using Quark.Components;
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

    [MemoryPackOrder(5)]
    public required EstimateMode EstimateMode { get; set; }

    [MemoryPackOnDeserialized]
    private void Migrate()
    {
        if (this._versionId < 1)
            this.EstimateMode = EstimateMode.Quality;
    }

    [MemoryPackOnDeserializing]
    private void OnDeserializing()
    {
        // MEMO: 項目の増減時にインクリメントする
        this._versionId = 1;
    }
}
