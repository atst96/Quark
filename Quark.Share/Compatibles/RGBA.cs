using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Quark.Compatibles;

/// <summary>
/// RGBA struct
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public struct RGBA
{
    /// <summary>R</summary>
    [FieldOffset(0)]
    public byte r;

    /// <summary>G</summary>
    [FieldOffset(1)]
    public byte g;

    /// <summary>G</summary>
    [FieldOffset(2)]
    public byte b;

    /// <summary>A</summary>
    [FieldOffset(3)]
    public byte a;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetColor(byte r, byte g, byte b, byte a)
        => (this.r, this.g, this.b, this.a) = (r, g, b, a);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetColor(byte allColor, byte alpha)
        => (this.r, this.g, this.b, this.a) = (allColor, allColor, allColor, alpha);
}
