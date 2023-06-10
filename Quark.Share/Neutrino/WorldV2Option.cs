namespace Quark.Neutrino;

public class WorldV2Option
{
    public required float[] F0 { get; init; }

    public required float[] Mgc { get; init; }

    public required float[] Bap { get; init; }

    public float? PitchShift { get; init; }

    public float? FormantShift { get; init; }

    public int? NumberOfParallel { get; init; }

    public bool IsRealtimeSynthesis { get; init; }

    public float? SmoothPitch { get; init; }

    public float? SmoothFormant { get; init; }

    public float? EnhanceBreathiness { get; init; }

    public bool IsViewInformation { get; init; }
}
