using MemoryPack;
using Quark.Models.Neutrino;

namespace Quark.Data.Projects.Neutrino;

[MemoryPackable]
public partial class AudioFeaturesV1Config
{
    public AudioFeaturesV1Config(string modelId)
    {
        this.ModelId = modelId;
    }

    public string ModelId { get; }

    public TimingInfo[]? Timings { get; set; }

    public PhraseInfo[]? RawPhrases { get; set; }

    public PhraseInfoV1[]? Phrases { get; set; }
}
