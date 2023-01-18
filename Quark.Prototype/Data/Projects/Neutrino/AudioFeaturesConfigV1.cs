using MemoryPack;

namespace Quark.Data.Projects.Neutrino;

[MemoryPackable]
public partial class AudioFeaturesConfigV1
{
    public AudioFeaturesConfigV1(string modelId)
    {
        this.ModelId = modelId;
    }

    public string ModelId { get; }

    public string? Timing { get; set; }

    public double[]? F0 { get; set; }

    public double[]? Mgc { get; set; }

    public double[]? Bap { get; set; }
}
