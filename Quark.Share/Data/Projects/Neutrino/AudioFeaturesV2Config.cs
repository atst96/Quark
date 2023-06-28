using MemoryPack;
using Quark.Models.Neutrino;

namespace Quark.Data.Projects.Neutrino;

[MemoryPackable]
public partial class AudioFeaturesV2Config
{
    public AudioFeaturesV2Config(string modelId)
    {
        this.ModelId = modelId;
    }

    public string ModelId { get; }

    public required TimingInfo[]? Timing { get; set; }

    public required PhraseInfo[] RawPhraseInfo { get; set; }

    public required PhraseInfoV2[] Phrases { get; set; }
}
