using World.NET.Structs.Fft;

namespace World.NET.Structs.Common;

internal ref struct MinimumPhaseAnalysis
{
    public int fft_size;
    public Span<double> log_spectrum;
    public Span<fft_complex> minimum_phase_spectrum;
    public Span<fft_complex> cepstrum;
    public fft_plan inverse_fft;
    public fft_plan forward_fft;
}