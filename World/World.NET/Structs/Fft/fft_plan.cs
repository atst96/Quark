namespace World.NET.Structs.Fft;

/// <summary>
/// Struct used for FFT
/// </summary>
internal ref struct fft_plan
{
    public int n;
    public int sign;
    public uint flags;
    public Span<fft_complex> c_in;
    public Span<double> @in;
    public Span<fft_complex> c_out;
    public Span<double> @out;
    public Span<double> input;
    public Span<int> ip;
    public Span<double> w;
}