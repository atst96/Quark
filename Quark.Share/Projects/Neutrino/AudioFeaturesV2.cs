namespace Quark.Projects.Neutrino;

public class AudioFeaturesV2
{
    public string ModelId { get; }

    public string? Timing { get; set; }

    public float[]? F0 { get; set; }

    public float[]? Mspec { get; set; }

    public AudioFeaturesV2(string modelId)
    {
        this.ModelId = modelId;
    }

    public bool HasTiming() => this.Timing is not null;

    public bool HasFeatures() => !(this.F0 is null || this.Mspec is null);
}
