using World.NET.Structs.Fft;

namespace World.NET.Structs.Common;

/// <summary>
/// Inverse FFT in the real sequence
/// </summary>
internal ref struct InverseRealFFT
{
    public int fft_size;
    public Span<double> waveform;
    public Span<fft_complex> spectrum;
    public fft_plan inverse_fft;
}

