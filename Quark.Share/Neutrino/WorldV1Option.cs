namespace Quark.Neutrino;

/// <summary>
/// NEUTRINO(v1)のWORLD合成オプション
/// </summary>
public class WorldV1Option
{
    /// <summary>基本周波数($1)</summary>
    public required double[] F0 { get; init; }

    /// <summary>スペクトラム包絡($2)</summary>
    public required double[] Mgc { get; init; }

    /// <summary>非同期成分($3)</summary>
    public required double[] Bap { get; init; }

    /// <summary>ピッチシフト(-f f)</summary>
    public float? PitchShift { get; init; }

    /// <summary>フォルマントシフト(-m f)</summary>
    public float? FormantShift { get; init; }

    /// <summary>Number of Parallel(-n i)</summary>
    public int? NumberOfParallel { get; init; }

    /// <summary>Hi-speed synthesis(-s)</summary>
    public bool IsHiSpeedSynthesis { get; init; }

    /// <summary>realtime synthesis(-r)</summary>
    public bool IsRealtimeSynthesis { get; init; }

    /// <summary>smooth pitch (beta version)(-p f)</summary>
    public float? SmoothPitch { get; init; }

    /// <summary>smooth formant (beta version)(-c f)</summary>
    public float? SmoothFormant { get; init; }

    /// <summary>enhance breathiness (beta version)(-b f)</summary>
    public float? EnhanceBreathiness { get; init; }

    /// <summary>詳細情報出力</summary>
    public bool IsViewInformation { get; init; }
}
