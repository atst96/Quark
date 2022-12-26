using System.Runtime.CompilerServices;
using World.NET.Structs.Common;
using World.NET.Structs.Fft;

namespace World.NET;

internal static class Common
{
    // These four functions are simple max() and min() function
    // for "int" and "double" type.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MyMaxInt(int x, int y)
        => x > y ? x : y;


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double MyMaxDouble(double x, double y)
        => x > y ? x : y;


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MyMinInt(int x, int y)
        => x < y ? x : y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double MyMinDouble(double x, double y)
        => x < y ? x : y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double GetSafeAperiodicity(double x)
        => MyMaxDouble(0.001, MyMinDouble(0.999999999999, x));

    // These functions are used to speed up the processing.

    /// <summary>
    /// Forward FFT
    /// </summary>
    /// <param name="fft_size"></param>
    /// <param name="forward_real_fft"></param>
    public static void InitializeForwardRealFFT(int fft_size, ref ForwardRealFFT forward_real_fft)
    {
        forward_real_fft.fft_size = fft_size;
        forward_real_fft.waveform = new double[fft_size];
        forward_real_fft.spectrum = new fft_complex[fft_size];
        forward_real_fft.forward_fft = Fft.fft_plan_dft_r2c_1d(fft_size,
            forward_real_fft.waveform, forward_real_fft.spectrum, Fft.FFT_ESTIMATE);
    }

    public static void DestroyForwardRealFFT(ref ForwardRealFFT forward_real_fft)
    {
        Fft.fft_destroy_plan(ref forward_real_fft.forward_fft);
        forward_real_fft.spectrum = null;
        forward_real_fft.waveform = null;
    }

    /// <summary>
    /// Inverse FFT
    /// </summary>
    public static void InitializeInverseRealFFT(int fft_size, ref InverseRealFFT inverse_real_fft)
    {
        inverse_real_fft.fft_size = fft_size;
        inverse_real_fft.waveform = new double[fft_size];
        inverse_real_fft.spectrum = new fft_complex[fft_size];
        inverse_real_fft.inverse_fft = Fft.fft_plan_dft_c2r_1d(fft_size,
            inverse_real_fft.spectrum, inverse_real_fft.waveform, Fft.FFT_ESTIMATE);
    }

    public static void DestroyInverseRealFFT(ref InverseRealFFT inverse_real_fft)
    {
        Fft.fft_destroy_plan(ref inverse_real_fft.inverse_fft);
        inverse_real_fft.spectrum = null;
        inverse_real_fft.waveform = null;
    }

    /// <summary>
    /// Minimum phase analysis (This analysis uses FFT)
    /// </summary>
    public static void InitializeMinimumPhaseAnalysis(int fft_size,
        ref MinimumPhaseAnalysis minimum_phase)
    {
        minimum_phase.fft_size = fft_size;
        minimum_phase.log_spectrum = new double[fft_size];
        minimum_phase.minimum_phase_spectrum = new fft_complex[fft_size];
        minimum_phase.cepstrum = new fft_complex[fft_size];
        minimum_phase.inverse_fft = Fft.fft_plan_dft_r2c_1d(fft_size,
            minimum_phase.log_spectrum, minimum_phase.cepstrum, Fft.FFT_ESTIMATE);
        minimum_phase.forward_fft = Fft.fft_plan_dft_1d(fft_size,
            minimum_phase.cepstrum, minimum_phase.minimum_phase_spectrum,
            Fft.FFT_FORWARD, Fft.FFT_ESTIMATE);
    }

    public static void DestroyMinimumPhaseAnalysis(ref MinimumPhaseAnalysis minimum_phase)
    {
        Fft.fft_destroy_plan(ref minimum_phase.forward_fft);
        Fft.fft_destroy_plan(ref minimum_phase.inverse_fft);
        minimum_phase.cepstrum = null;
        minimum_phase.log_spectrum = null;
        minimum_phase.minimum_phase_spectrum = null;
    }

    public static void GetMinimumPhaseSpectrum(ref MinimumPhaseAnalysis minimum_phase)
    {
        // Mirroring
        for (int i = minimum_phase.fft_size / 2 + 1;
            i < minimum_phase.fft_size; ++i)
            minimum_phase.log_spectrum[i] =
            minimum_phase.log_spectrum[minimum_phase.fft_size - i];

        // This fft_plan carries out "forward" FFT.
        // To carriy out the Inverse FFT, the sign of imaginary part
        // is inverted after FFT.
        Fft.fft_execute(ref minimum_phase.inverse_fft);
        minimum_phase.cepstrum[0].v1 *= -1.0;
        for (int i = 1; i < minimum_phase.fft_size / 2; ++i)
        {
            minimum_phase.cepstrum[i].v0 *= 2.0;
            minimum_phase.cepstrum[i].v1 *= -2.0;
        }
        minimum_phase.cepstrum[minimum_phase.fft_size / 2].v1 *= -1.0;
        for (int i = minimum_phase.fft_size / 2 + 1;
            i < minimum_phase.fft_size; ++i)
        {
            minimum_phase.cepstrum[i].v0 = 0.0;
            minimum_phase.cepstrum[i].v1 = 0.0;
        }

        Fft.fft_execute(ref minimum_phase.forward_fft);

        // Since x is complex number, calculation of exp(x) is as following.
        // Note: This FFT library does not keep the aliasing.
        double tmp;
        for (int i = 0; i <= minimum_phase.fft_size / 2; ++i)
        {
            tmp = Math.Exp(minimum_phase.minimum_phase_spectrum[i].v0 /
              minimum_phase.fft_size);
            minimum_phase.minimum_phase_spectrum[i].v0 = tmp *
              Math.Cos(minimum_phase.minimum_phase_spectrum[i].v1 /
              minimum_phase.fft_size);
            minimum_phase.minimum_phase_spectrum[i].v1 = tmp *
              Math.Sin(minimum_phase.minimum_phase_spectrum[i].v1 /
              minimum_phase.fft_size);
        }
    }
}
