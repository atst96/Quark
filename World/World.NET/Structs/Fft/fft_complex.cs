using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace World.NET.Structs.Fft;

/// <summary>
/// Complex number for FFT
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct fft_complex
{
    public double v0;
    public double v1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(double v0, double v1)
    {
        this.v0 = v0;
        this.v1 = v1;
    }
}