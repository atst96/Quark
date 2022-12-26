using World.NET.Structs.Fft;

namespace World.NET.Structs.Common;

/// <summary>
/// Forward FFT in the real sequence
/// </summary>
internal ref struct ForwardRealFFT
{
    public int fft_size;
    public Span<double> waveform;
    public Span<fft_complex> spectrum;
    public fft_plan forward_fft;
}
