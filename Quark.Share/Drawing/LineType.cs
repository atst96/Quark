namespace Quark.Drawing;

/// <summary>
/// 縦線の種別
/// </summary>
[Flags]
public enum LineType : ushort
{
    /// <summary>小節</summary>
    Measure = 0x0100,
    /// <summary>三連符</summary>
    Triplet = 0x0200,
    /// <summary>付点音符</summary>
    Dotted = 0x0300,

    /// <summary>全音符</summary>
    Whole = 1,
    /// <summary>2分音符</summary>
    Note2th = 2,
    /// <summary>4分音符</summary>
    Note4th = 4,
    /// <summary>8分音符</summary>
    Note8th = 8,
    /// <summary>16分音符</summary>
    Note16th = 16,
    /// <summary>32分音符</summary>
    Note32th = 32,
    /// <summary>64分音符</summary>
    Note64th = 64,
    /// <summary>128分音符</summary>
    Note128th = 128,

    // 三連符系
    /// <summary>2分三連符</summary>
    Note2thTriplet = Triplet | Note2th,
    /// <summary>4分三連符</summary>
    Note4thTriplet = Triplet | Note4th,
    /// <summary>8分三連符</summary>
    Note8thTriplet = Triplet | Note8th,
    /// <summary>16分三連符</summary>
    Note16thTriplet = Triplet | Note16th,
    /// <summary>32分三連符</summary>
    Note32thTriplet = Triplet | Note32th,
    /// <summary>64分三連符</summary>
    Note64thTriplet = Triplet | Note64th,

    // 付点系
    /// <summary>2分付点</summary>
    Note2thDotted = Dotted | Note2th,
    /// <summary>4分付点</summary>
    Note4thDotted = Dotted | Note4th,
    /// <summary>8分付点</summary>
    Note8thDotted = Dotted | Note8th,
    /// <summary>16分付点</summary>
    Note16thDotted = Dotted | Note16th,
    /// <summary>32分付点</summary>
    Note32thDotted = Dotted | Note32th,
    /// <summary>64分付点</summary>
    Note64thDotted = Dotted | Note64th,
}
