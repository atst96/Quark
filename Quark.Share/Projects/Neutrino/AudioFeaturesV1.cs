using Quark.Models.Neutrino;

namespace Quark.Projects.Neutrino;

public class AudioFeaturesV1
{
    public string ModelId { get; }

    public TimingInfo[] Timings { get; set; }

    public PhraseInfo[] RawPhrases { get; set; }

    public PhraseInfo2[] Phrases { get; set; }

    public AudioFeaturesV1(string modelId)
    {
        this.ModelId = modelId;
        this.Timings = Array.Empty<TimingInfo>();
        this.RawPhrases = Array.Empty<PhraseInfo>();
        this.Phrases = Array.Empty<PhraseInfo2>();
    }

    public bool HasTiming() => this.Timings.Any();
}
