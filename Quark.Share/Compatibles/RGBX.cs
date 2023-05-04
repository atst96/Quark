using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Quark.Compatibles;

/// <summary>
/// RGB struct
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 4)]
public struct RGBX
{
    /// <summary>R</summary>
    [FieldOffset(0)]
    public byte r;

    /// <summary>G</summary>
    [FieldOffset(1)]
    public byte g;

    /// <summary>B</summary>
    [FieldOffset(2)]
    public byte b;

    /// <summary>other bytes</summary>
    [FieldOffset(3)]
    public byte _;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetColor(byte r, byte g, byte b)
        => (this.r, this.g, this.b) = (r, g, b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetColor(byte allColor)
        => (this.r, this.g, this.b) = (allColor, allColor, allColor);
}
