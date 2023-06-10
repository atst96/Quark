using Quark.Models.Neutrino;

namespace Quark.Neutrino;

public class NeutrinoV2Option
{
    public required byte[] FullLabel { get; init; }

    public byte[]? TimingLabel { get; init; }

    public byte[]? EstimatedPhrases { get; set; }

    public required ModelInfo ModelInfo { get; init; }

    public int? NumberOfParallel { get; init; }

    public int? NumberOfParallelInSession { get; set; }

    public NeutrinoV2InferenceMode? InferenceMode { get; init; }

    public int? StyleShift { get; init; }

    public int? RandomSeed { get; set; }

    public bool IsSkipTimingPrediction { get; init; }

    public bool IsSkipAcousticFeaturesPrediction { get; init; }

    public bool WorldFeaturesPrediction { get; init; }

    public int? SinglePhrasePrediction { get; init; }

    public int? UseSingleGpu { get; init; }

    public bool UseMultipleGpus { get; init; }

    public bool IsTracePhraseInformation { get; init; }

    public bool IsViewInformation { get; init; }
}
