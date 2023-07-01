using Quark.Models.Neutrino;

namespace Quark.Neutrino;

public class NSFV2Option
{
    public required float[] F0 { get; init; }

    public required float[] Melspec { get; init; }

    public required ModelInfo Model { get; init; }

    public required NSFV2Model ModelType { get; init; }

    public int? SamplingRate { get; init; }

    public double? PitchShift { get; init; }

    public int? NumberOfParallel { get; init; }

    public int? NumberOfParallelInSession { get; init; }

    public TimingInfo[]? MultiPhrasePrediction { get; init; }

    public int? UseSingleGpu { get; init; }

    public bool UseMultiGpus { get; init; }

    public bool IsViewInformation { get; init; }
}
