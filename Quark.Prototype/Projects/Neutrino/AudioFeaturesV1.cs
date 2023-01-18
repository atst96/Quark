namespace Quark.Projects.Neutrino;

public class AudioFeaturesV1
{
    public string ModelId { get; }

    public string? Timing { get; set; }

    public double[]? F0 { get; set; }

    public double[]? Mgc { get; set; }

    public double[]? Bap { get; set; }

    public AudioFeaturesV1(string modelId)
    {
        this.ModelId = modelId;
    }

    public bool HasTiming() => this.Timing is not null;

    public bool HasFeatures() => !(this.F0 is null || this.Mgc is null || this.Bap is null);
}
