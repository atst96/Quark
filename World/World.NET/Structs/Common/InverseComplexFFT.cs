using World.NET.Structs.Fft;

namespace World.NET.Structs.Common;

/// <summary>
/// Inverse FFT in the complex sequence
/// </summary>
internal ref struct InverseComplexFFT
{
    public int fft_size;
    public Span<fft_complex> input;
    public Span<fft_complex> output;
    public fft_plan inverse_fft;
}
