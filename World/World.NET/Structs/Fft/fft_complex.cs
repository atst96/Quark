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
}