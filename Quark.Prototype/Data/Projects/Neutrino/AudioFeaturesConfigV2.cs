using MemoryPack;

namespace Quark.Data.Projects.Neutrino;

[MemoryPackable]
public partial class AudioFeaturesConfigV2
{
    public AudioFeaturesConfigV2(string modelId)
    {
        this.ModelId = modelId;
    }

    public string ModelId { get; }

    public string? Timing { get; set; }

    public float[]? F0 { get; set; }

    public float[]? Mspec { get; set; }
}
