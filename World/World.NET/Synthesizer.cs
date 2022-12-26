using Microsoft.VisualBasic;
using World.NET.Structs.Common;
using World.NET.Utils;

namespace World.NET;

public static class Synthesizer
{
    static void GetNoiseSpectrum(int noise_size, int fft_size,
        ref ForwardRealFFT forward_real_fft)
    {
        double average = 0.0;
        for (int i = 0; i < noise_size; ++i)
        {
            forward_real_fft.waveform[i] = MatlabFunctions.randn();
            average += forward_real_fft.waveform[i];
        }

        average /= noise_size;
        for (int i = 0; i < noise_size; ++i)
            forward_real_fft.waveform[i] -= average;
        for (int i = noise_size; i < fft_size; ++i)
            forward_real_fft.waveform[i] = 0.0;
        Fft.fft_execute(ref forward_real_fft.forward_fft);
    }

    /// <summary>
    /// GetAperiodicResponse() calculates an aperiodic response.
    /// </summary>
    static void GetAperiodicResponse(int noise_size, int fft_size,
        Span<double> spectrum, Span<double> aperiodic_ratio, double current_vuv,
        ref ForwardRealFFT forward_real_fft,
        ref InverseRealFFT inverse_real_fft,
        ref MinimumPhaseAnalysis minimum_phase, Span<double> aperiodic_response)
    {
        GetNoiseSpectrum(noise_size, fft_size, ref forward_real_fft);

        if (current_vuv != 0.0)
            for (int i = 0; i <= minimum_phase.fft_size / 2; ++i)
                minimum_phase.log_spectrum[i] =
                  Math.Log(spectrum[i] * aperiodic_ratio[i]) / 2.0;
        else
            for (int i = 0; i <= minimum_phase.fft_size / 2; ++i)
                minimum_phase.log_spectrum[i] = Math.Log(spectrum[i]) / 2.0;
        Common.GetMinimumPhaseSpectrum(ref minimum_phase);

        for (int i = 0; i <= fft_size / 2; ++i)
        {
            inverse_real_fft.spectrum[i].v0 =
              minimum_phase.minimum_phase_spectrum[i].v0 *
              forward_real_fft.spectrum[i].v0 -
              minimum_phase.minimum_phase_spectrum[i].v1 *
              forward_real_fft.spectrum[i].v1;
            inverse_real_fft.spectrum[i].v1 =
              minimum_phase.minimum_phase_spectrum[i].v0 *
              forward_real_fft.spectrum[i].v1 +
              minimum_phase.minimum_phase_spectrum[i].v1 *
              forward_real_fft.spectrum[i].v0;
        }
        Fft.fft_execute(ref inverse_real_fft.inverse_fft);
        MatlabFunctions.fftshift(inverse_real_fft.waveform, fft_size, aperiodic_response);
    }

    /// <summary>
    /// RemoveDCComponent()
    /// </summary>
    private static void RemoveDCComponent(Span<double> periodic_response, int fft_size,
        Span<double> dc_remover, Span<double> new_periodic_response)
    {
        double dc_component = 0.0;
        for (int i = fft_size / 2; i < fft_size; ++i)
            dc_component += periodic_response[i];
        for (int i = 0; i < fft_size / 2; ++i)
            new_periodic_response[i] = -dc_component * dc_remover[i];
        for (int i = fft_size / 2; i < fft_size; ++i)
            new_periodic_response[i] -= dc_component * dc_remover[i];
    }

    /// <summary>
    /// GetSpectrumWithFractionalTimeShift() calculates a periodic spectrum with the fractional time shift under 1/fs.
    /// </summary>
    /// <param name="fft_size"></param>
    /// <param name="coefficient"></param>
    /// <param name="inverse_real_fft"></param>
    static void GetSpectrumWithFractionalTimeShift(int fft_size,
            double coefficient, ref InverseRealFFT inverse_real_fft)
    {
        double re, im, re2, im2;
        for (int i = 0; i <= fft_size / 2; ++i)
        {
            re = inverse_real_fft.spectrum[i].v0;
            im = inverse_real_fft.spectrum[i].v1;
            re2 = Math.Cos(coefficient * i);
            im2 = Math.Sqrt(1.0 - re2 * re2);  // sin(pshift)

            inverse_real_fft.spectrum[i].v0 = re * re2 + im * im2;
            inverse_real_fft.spectrum[i].v1 = im * re2 - re * im2;
        }
    }

    /// <summary>
    /// GetPeriodicResponse() calculates a periodic response.
    /// </summary>
    static void GetPeriodicResponse(int fft_size, Span<double> spectrum,
        Span<double> aperiodic_ratio, double current_vuv,
        ref InverseRealFFT inverse_real_fft,
        ref MinimumPhaseAnalysis minimum_phase, Span<double> dc_remover,
        double fractional_time_shift, int fs, Span<double> periodic_response)
    {
        if (current_vuv <= 0.5 || aperiodic_ratio[0] > 0.999)
        {
            for (int i = 0; i < fft_size; ++i) periodic_response[i] = 0.0;
            return;
        }

        for (int i = 0; i <= minimum_phase.fft_size / 2; ++i)
            minimum_phase.log_spectrum[i] =
              Math.Log(spectrum[i] * (1.0 - aperiodic_ratio[i]) +
              ConstantNumbers.kMySafeGuardMinimum) / 2.0;
        Common.GetMinimumPhaseSpectrum(ref minimum_phase);

        for (int i = 0; i <= fft_size / 2; ++i)
        {
            inverse_real_fft.spectrum[i].v0 =
              minimum_phase.minimum_phase_spectrum[i].v0;
            inverse_real_fft.spectrum[i].v1 =
              minimum_phase.minimum_phase_spectrum[i].v1;
        }

        // apply fractional time delay of fractional_time_shift seconds
        // using linear phase shift
        double coefficient =
          2.0 * ConstantNumbers.kPi * fractional_time_shift * fs / fft_size;
        GetSpectrumWithFractionalTimeShift(fft_size, coefficient, ref inverse_real_fft);

        Fft.fft_execute(ref inverse_real_fft.inverse_fft);
        MatlabFunctions.fftshift(inverse_real_fft.waveform, fft_size, periodic_response);
        RemoveDCComponent(periodic_response, fft_size, dc_remover,
            periodic_response);
    }

    private static void GetSpectralEnvelope(double current_time, double frame_period,
        int f0_length, double[][] spectrogram, int fft_size,
        Span<double> spectral_envelope)
    {
        int current_frame_floor = Common.MyMinInt(f0_length - 1,
          (int)Math.Floor(current_time / frame_period));
        int current_frame_ceil = Common.MyMinInt(f0_length - 1,
          (int)Math.Ceiling(current_time / frame_period));
        double interpolation = current_time / frame_period - current_frame_floor;

        if (current_frame_floor == current_frame_ceil)
            for (int i = 0; i <= fft_size / 2; ++i)
                spectral_envelope[i] = Math.Abs(spectrogram[current_frame_floor][i]);
        else
            for (int i = 0; i <= fft_size / 2; ++i)
                spectral_envelope[i] =
                  (1.0 - interpolation) * Math.Abs(spectrogram[current_frame_floor][i]) +
                  interpolation * Math.Abs(spectrogram[current_frame_ceil][i]);
    }

    static void GetAperiodicRatio(double current_time, double frame_period,
        int f0_length, double[][] aperiodicity, int fft_size,
        Span<double> aperiodic_spectrum)
    {
        int current_frame_floor = Common.MyMinInt(f0_length - 1,
          (int)(Math.Floor(current_time / frame_period)));
        int current_frame_ceil = Common.MyMinInt(f0_length - 1,
          (int)(Math.Ceiling(current_time / frame_period)));
        double interpolation = current_time / frame_period - current_frame_floor;

        if (current_frame_floor == current_frame_ceil)
            for (int i = 0; i <= fft_size / 2; ++i)
                aperiodic_spectrum[i] =
                  Math.Pow(Common.GetSafeAperiodicity(aperiodicity[current_frame_floor][i]), 2.0);
        else
            for (int i = 0; i <= fft_size / 2; ++i)
                aperiodic_spectrum[i] = Math.Pow((1.0 - interpolation) *
                    Common.GetSafeAperiodicity(aperiodicity[current_frame_floor][i]) +
                    interpolation *
                    Common.GetSafeAperiodicity(aperiodicity[current_frame_ceil][i]), 2.0);
    }

    /// <summary>
    /// GetOneFrameSegment() calculates a periodic and aperiodic response at a time.
    /// </summary>
    static void GetOneFrameSegment(
        double current_vuv, int noise_size,
        double[][] spectrogram, int fft_size,
        double[][] aperiodicity, int f0_length, double frame_period,
        double current_time, double fractional_time_shift, int fs,
        ref ForwardRealFFT forward_real_fft,
        ref InverseRealFFT inverse_real_fft,
        ref MinimumPhaseAnalysis minimum_phase, Span<double> dc_remover,
        Span<double> response)
    {
        double[] aperiodic_response = new double[fft_size];
        double[] periodic_response = new double[fft_size];

        double[] spectral_envelope = new double[fft_size];
        double[] aperiodic_ratio = new double[fft_size];
        GetSpectralEnvelope(current_time, frame_period, f0_length, spectrogram,
            fft_size, spectral_envelope);
        GetAperiodicRatio(current_time, frame_period, f0_length, aperiodicity,
            fft_size, aperiodic_ratio);

        // Synthesis of the periodic response
        GetPeriodicResponse(fft_size, spectral_envelope, aperiodic_ratio,
            current_vuv, ref inverse_real_fft, ref minimum_phase, dc_remover,
            fractional_time_shift, fs, periodic_response);

        // Synthesis of the aperiodic response
        GetAperiodicResponse(noise_size, fft_size, spectral_envelope,
            aperiodic_ratio, current_vuv, ref forward_real_fft,
            ref inverse_real_fft, ref minimum_phase, aperiodic_response);

        double sqrt_noise_size = Math.Sqrt(noise_size);
        for (int i = 0; i < fft_size; ++i)
            response[i] =
              (periodic_response[i] * sqrt_noise_size + aperiodic_response[i]) /
              fft_size;

        //ArrayUtil.Return(ref spectral_envelope);
        //ArrayUtil.Return(ref aperiodic_ratio);
        //ArrayUtil.Return(ref periodic_response);
        //ArrayUtil.Return(ref aperiodic_response);
    }

    static void GetTemporalParametersForTimeBase(Span<double> f0, int f0_length,
            int fs, int y_length, double frame_period, double lowest_f0,
            Span<double> time_axis, Span<double> coarse_time_axis, Span<double> coarse_f0,
            Span<double> coarse_vuv)
    {
        for (int i = 0; i < y_length; ++i)
            time_axis[i] = i / (double)fs;
        // the array 'coarse_time_axis' is supposed to have 'f0_length + 1' positions
        for (int i = 0; i < f0_length; ++i)
        {
            coarse_time_axis[i] = i * frame_period;
            coarse_f0[i] = f0[i] < lowest_f0 ? 0.0 : f0[i];
            coarse_vuv[i] = coarse_f0[i] == 0.0 ? 0.0 : 1.0;
        }
        coarse_time_axis[f0_length] = f0_length * frame_period;
        coarse_f0[f0_length] = coarse_f0[f0_length - 1] * 2 -
          coarse_f0[f0_length - 2];
        coarse_vuv[f0_length] = coarse_vuv[f0_length - 1] * 2 -
          coarse_vuv[f0_length - 2];
    }

    static int GetPulseLocationsForTimeBase(Span<double> interpolated_f0,
        Span<double> time_axis, int y_length, int fs, Span<double> pulse_locations,
        Span<int> pulse_locations_index, Span<double> pulse_locations_time_shift)
    {
        double[] total_phase = new double[y_length];
        double[] wrap_phase = new double[y_length];
        double[] wrap_phase_abs = new double[y_length - 1];
        total_phase[0] = 2.0 * ConstantNumbers.kPi * interpolated_f0[0] / fs;
        wrap_phase[0] = total_phase[0] % (2.0 * ConstantNumbers.kPi);
        for (int i = 1; i < y_length; ++i)
        {
            total_phase[i] = total_phase[i - 1] +
              2.0 * ConstantNumbers.kPi * interpolated_f0[i] / fs;
            wrap_phase[i] = total_phase[i] % (2.0 * ConstantNumbers.kPi);
            wrap_phase_abs[i - 1] = Math.Abs(wrap_phase[i] - wrap_phase[i - 1]);
        }

        int number_of_pulses = 0;
        for (int i = 0; i < y_length - 1; ++i)
        {
            if (wrap_phase_abs[i] > ConstantNumbers.kPi)
            {
                pulse_locations[number_of_pulses] = time_axis[i];
                pulse_locations_index[number_of_pulses] = i;

                // calculate the time shift in seconds between exact fractional pulse
                // position and the integer pulse position (sample i)
                // as we don't have access to the exact pulse position, we infer it
                // from the point between sample i and sample i + 1 where the
                // accummulated phase cross a multiple of 2pi
                // this point is found by solving y1 + x * (y2 - y1) = 0 for x, where y1
                // and y2 are the phases corresponding to sample i and i + 1, offset so
                // they cross zero; x >= 0
                double y1 = wrap_phase[i] - 2.0 * ConstantNumbers.kPi;
                double y2 = wrap_phase[i + 1];
                double x = -y1 / (y2 - y1);
                pulse_locations_time_shift[number_of_pulses] = x / fs;

                ++number_of_pulses;
            }
        }

        //ArrayUtil.Return(ref wrap_phase_abs);
        //ArrayUtil.Return(ref wrap_phase);
        //ArrayUtil.Return(ref total_phase);

        return number_of_pulses;
    }

    private static int GetTimeBase(Span<double> f0, int f0_length, int fs,
        double frame_period, int y_length, double lowest_f0,
        Span<double> pulse_locations, Span<int> pulse_locations_index,
        Span<double> pulse_locations_time_shift, Span<double> interpolated_vuv)
    {
        double[] time_axis = new double[y_length];
        double[] coarse_time_axis = new double[f0_length + 1];
        double[] coarse_f0 = new double[f0_length + 1];
        double[] coarse_vuv = new double[f0_length + 1];
        GetTemporalParametersForTimeBase(f0, f0_length, fs, y_length, frame_period,
            lowest_f0, time_axis, coarse_time_axis, coarse_f0, coarse_vuv);
        double[] interpolated_f0 = new double[y_length];
        MatlabFunctions.interp1(coarse_time_axis, coarse_f0, f0_length + 1,
            time_axis, y_length, interpolated_f0);
        MatlabFunctions.interp1(coarse_time_axis, coarse_vuv, f0_length + 1,
            time_axis, y_length, interpolated_vuv);

        for (int i = 0; i < y_length; ++i)
        {
            interpolated_vuv[i] = interpolated_vuv[i] > 0.5 ? 1.0 : 0.0;
            interpolated_f0[i] =
              interpolated_vuv[i] == 0.0 ? ConstantNumbers.kDefaultF0 : interpolated_f0[i];
        }

        int number_of_pulses = GetPulseLocationsForTimeBase(interpolated_f0,
            time_axis, y_length, fs, pulse_locations, pulse_locations_index,
            pulse_locations_time_shift);

        // ArrayUtil.Return(ref coarse_vuv);
        // ArrayUtil.Return(ref coarse_f0);
        // ArrayUtil.Return(ref coarse_time_axis);
        // ArrayUtil.Return(ref time_axis);
        // ArrayUtil.Return(ref interpolated_f0);

        return number_of_pulses;
    }

    private static void GetDCRemover(int fft_size, Span<double> dc_remover)
    {
        double dc_component = 0.0;
        for (int i = 0; i < fft_size / 2; ++i)
        {
            dc_remover[i] = 0.5 -
              0.5 * Math.Cos(2.0 * ConstantNumbers.kPi * (i + 1.0) / (1.0 + fft_size));
            dc_remover[fft_size - i - 1] = dc_remover[i];
            dc_component += dc_remover[i] * 2.0;
        }
        for (int i = 0; i < fft_size / 2; ++i)
        {
            dc_remover[i] /= dc_component;
            dc_remover[fft_size - i - 1] = dc_remover[i];
        }
    }

    /// <summary>
    /// Synthesis() synthesize the voice based on f0, spectrogram and aperiodicity (not excitation signal).
    /// </summary>
    /// <param name="f0">f0 contour</param>
    /// <param name="f0_length">Length of f0</param>
    /// <param name="spectrogram">Spectrogram estimated by CheapTrick</param>
    /// <param name="aperiodicity">Aperiodicity spectrogram based on D4C</param>
    /// <param name="fft_size">FFT size</param>
    /// <param name="frame_period"></param>
    /// <param name="fs">Sampling frequency</param>
    /// <param name="y_length">Length of the output signal (Memory of y has been allocated in advance)</param>
    /// <param name="y">Calculated speech</param>
    public static void Synthesis(Span<double> f0, int f0_length,
        double[][] spectrogram, double[][] aperiodicity,
        int fft_size, double frame_period, int fs, int y_length, Span<double> y)
    {
        MatlabFunctions.randn_reseed();

        double[] impulse_response = new double[fft_size];

        for (int i = 0; i < y_length; ++i) y[i] = 0.0;

        MinimumPhaseAnalysis minimum_phase = default;
        Common.InitializeMinimumPhaseAnalysis(fft_size, ref minimum_phase);
        InverseRealFFT inverse_real_fft = default;
        Common.InitializeInverseRealFFT(fft_size, ref inverse_real_fft);
        ForwardRealFFT forward_real_fft = default;
        Common.InitializeForwardRealFFT(fft_size, ref forward_real_fft);

        double[] pulse_locations = new double[y_length];
        int[] pulse_locations_index = new int[y_length];
        double[] pulse_locations_time_shift = new double[y_length];
        double[] interpolated_vuv = new double[y_length];
        int number_of_pulses = GetTimeBase(f0, f0_length, fs, frame_period / 1000.0,
            y_length, fs / fft_size + 1.0, pulse_locations, pulse_locations_index,
            pulse_locations_time_shift, interpolated_vuv);

        double[] dc_remover = new double[fft_size];
        GetDCRemover(fft_size, dc_remover);

        frame_period /= 1000.0;
        int noise_size;
        int index, offset, lower_limit, upper_limit;
        for (int i = 0; i < number_of_pulses; ++i)
        {
            noise_size = pulse_locations_index[Common.MyMinInt(number_of_pulses - 1, i + 1)] -
              pulse_locations_index[i];

            GetOneFrameSegment(interpolated_vuv[pulse_locations_index[i]], noise_size,
                spectrogram, fft_size, aperiodicity, f0_length, frame_period,
                pulse_locations[i], pulse_locations_time_shift[i], fs,
                ref forward_real_fft, ref inverse_real_fft, ref minimum_phase, dc_remover,
                impulse_response);
            offset = pulse_locations_index[i] - fft_size / 2 + 1;
            lower_limit = Common.MyMaxInt(0, -offset);
            upper_limit = Common.MyMinInt(fft_size, y_length - offset);
            for (int j = lower_limit; j < upper_limit; ++j)
            {
                index = j + offset;
                y[index] += impulse_response[j];
            }
        }

        //delete[] dc_remover;
        //delete[] pulse_locations;
        //delete[] pulse_locations_index;
        //delete[] pulse_locations_time_shift;
        //delete[] interpolated_vuv;

        Common.DestroyMinimumPhaseAnalysis(ref minimum_phase);
        Common.DestroyInverseRealFFT(ref inverse_real_fft);
        Common.DestroyForwardRealFFT(ref forward_real_fft);

        //delete[] impulse_response;
    }
}
