using System.Numerics;
using System.Runtime.InteropServices;
using World.NET.Structs.Common;
using World.NET.Structs.Fft;
using World.NET.Utils;

namespace World.NET;

public static unsafe class Synthesizer
{
    private static void GetNoiseSpectrum(int noise_size, int fft_size,
        ref ForwardRealFFT forward_real_fft)
    {
        double average = 0.0;
        fixed (double* p_waveform = forward_real_fft.waveform)
        {
            for (int i = 0; i < noise_size; ++i)
            {
                //forward_real_fft.waveform[i] = MatlabFunctions.randn();
                //average += forward_real_fft.waveform[i];
                average += (p_waveform[i] = MatlabFunctions.randn());
            }
        }

        average /= noise_size;
        //for (int i = 0; i < noise_size; ++i)
        //    forward_real_fft.waveform[i] -= average;
        VectorUtil.SubstractToSelf(forward_real_fft.waveform[0..noise_size], average);
        //for (int i = noise_size; i < fft_size; ++i)
        //    forward_real_fft.waveform[i] = 0.0;
        forward_real_fft.waveform[noise_size..fft_size].Clear();
        Fft.fft_execute(ref forward_real_fft.forward_fft);
    }

    /// <summary>
    /// GetAperiodicResponse() calculates an aperiodic response.
    /// </summary>
    static unsafe void GetAperiodicResponse(int noise_size, int fft_size,
        Span<double> spectrum, Span<double> aperiodic_ratio, double current_vuv,
        ref ForwardRealFFT forward_real_fft,
        ref InverseRealFFT inverse_real_fft,
        ref MinimumPhaseAnalysis minimum_phase, double[] aperiodic_response)
    {
        GetNoiseSpectrum(noise_size, fft_size, ref forward_real_fft);

        //if (current_vuv != 0.0)
        //    for (int i = 0; i <= minimum_phase.fft_size / 2; ++i)
        //        minimum_phase.log_spectrum[i] =
        //          Math.Log(spectrum[i] * aperiodic_ratio[i]) / 2.0;
        //else
        //    for (int i = 0; i <= minimum_phase.fft_size / 2; ++i)
        //        minimum_phase.log_spectrum[i] = Math.Log(spectrum[i]) / 2.0;
        unsafe
        {
            fixed (double* p_log_spectrum = minimum_phase.log_spectrum)
            fixed (double* p_spectrum = spectrum)
            fixed (double* p_aperiodic_ratio = aperiodic_ratio)
            {
                if (current_vuv != 0.0)
                    for (int i = 0; i <= minimum_phase.fft_size / 2; ++i)
                        p_log_spectrum[i] =
                          Math.Log(p_spectrum[i] * p_aperiodic_ratio[i]) / 2.0;
                else
                    for (int i = 0; i <= minimum_phase.fft_size / 2; ++i)
                        p_log_spectrum[i] = Math.Log(p_spectrum[i]) / 2.0;
            }
        }
        Common.GetMinimumPhaseSpectrum(ref minimum_phase);

        // for (int i = 0; i <= fft_size / 2; ++i)

        fixed (fft_complex* p_minimum_phase_spectrum = minimum_phase.minimum_phase_spectrum)
        fixed (fft_complex* p_spectrum = forward_real_fft.spectrum)
        {
            for (int i = 0, l = fft_size / 2 + 1; i < l; ++i)
            {
                //inverse_real_fft.spectrum[i].v0 =
                //  minimum_phase.minimum_phase_spectrum[i].v0 *
                //  forward_real_fft.spectrum[i].v0 -
                //  minimum_phase.minimum_phase_spectrum[i].v1 *
                //  forward_real_fft.spectrum[i].v1;
                //inverse_real_fft.spectrum[i].v1 =
                //  minimum_phase.minimum_phase_spectrum[i].v0 *
                //  forward_real_fft.spectrum[i].v1 +
                //  minimum_phase.minimum_phase_spectrum[i].v1 *
                //  forward_real_fft.spectrum[i].v0;

                ref var min_spectrum = ref p_minimum_phase_spectrum[i];
                ref var fft_spectrum = ref p_spectrum[i];

                inverse_real_fft.spectrum[i].Set(
                    min_spectrum.v0 * fft_spectrum.v0 - min_spectrum.v1 * fft_spectrum.v1,
                    min_spectrum.v0 * fft_spectrum.v1 + min_spectrum.v1 * fft_spectrum.v0);
            }
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
        // double dc_component = 0.0;
        //for (int i = fft_size / 2; i < fft_size; ++i)
        //    dc_component += periodic_response[i];
        double dc_component = VectorUtil.Sum(periodic_response[(fft_size / 2)..fft_size]);

        //for (int i = 0; i < fft_size / 2; ++i)
        //    new_periodic_response[i] = -dc_component * dc_remover[i];
        {
            int size = Vector<double>.Count;
            int length = fft_size / 2;
            var vec_dc_component = new Vector<double>(-dc_component);
            var vec_dc_remover = dc_remover[0..length].ToVector();

            for (int i = 0; i < vec_dc_remover.Length; ++i)
            {
                (vec_dc_component * vec_dc_remover[i]).CopyTo(new_periodic_response[(i * size)..]);
            }

            for (int i = vec_dc_remover.Length * size; i < length; ++i)
            {
                new_periodic_response[i] = -dc_component * dc_remover[i];
            }
        }
        //for (int i = fft_size / 2; i < fft_size; ++i)
        //    new_periodic_response[i] -= dc_component * dc_remover[i];
        {
            int offset = fft_size / 2;
            int size = Vector<double>.Count;
            int length = fft_size / 2;
            var vec_dc_component = new Vector<double>(dc_component);
            var vec_dc_remover = dc_remover[offset..fft_size].ToVector();
            var vec_new_periodic_response = new_periodic_response[offset..fft_size].ToVector();

            for (int i = 0; i < vec_dc_remover.Length; ++i)
            {
                (vec_new_periodic_response[i] - (vec_dc_component * vec_dc_remover[i]))
                    .CopyTo(new_periodic_response[(offset + i * size)..]);
            }

            for (int i = offset + vec_dc_remover.Length * size; i < fft_size; ++i)
            {
                new_periodic_response[i] -= dc_component * dc_remover[i];
            }
        }
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
        double re, im, re2, im2, i = 0;
        //for (int i = 0; i <= fft_size / 2; ++i)
        //{
        //    re = inverse_real_fft.spectrum[i].v0;
        //    im = inverse_real_fft.spectrum[i].v1;
        //    re2 = Math.Cos(coefficient * i);
        //    im2 = Math.Sqrt(1.0 - re2 * re2);  // sin(pshift)

        //    inverse_real_fft.spectrum[i].v0 = re * re2 + im * im2;
        //    inverse_real_fft.spectrum[i].v1 = im * re2 - re * im2;
        //}

        foreach (ref var spectrum in inverse_real_fft.spectrum[0..(fft_size / 2 + 1)])
        {
            (re, im) = (spectrum.v0, spectrum.v1);
            re2 = Math.Cos(coefficient * i++);
            im2 = Math.Sqrt(1.0 - re2 * re2);  // sin(pshift)
            spectrum.Set(
                re * re2 + im * im2,
                im * re2 - re * im2);
        }
    }

    /// <summary>
    /// GetPeriodicResponse() calculates a periodic response.
    /// </summary>
    static void GetPeriodicResponse(int fft_size, Span<double> spectrum,
        Span<double> aperiodic_ratio, double current_vuv,
        ref InverseRealFFT inverse_real_fft,
        ref MinimumPhaseAnalysis minimum_phase, Span<double> dc_remover,
        double fractional_time_shift, int fs, double[] periodic_response)
    {
        if (current_vuv <= 0.5 || aperiodic_ratio[0] > 0.999)
        {
            // for (int i = 0; i < fft_size; ++i) periodic_response[i] = 0.0;
            periodic_response.AsSpan(0..fft_size).Clear();
            return;
        }

        //for (int i = 0; i <= minimum_phase.fft_size / 2; ++i)
        //for (int i = 0, l = minimum_phase.fft_size / 2; i <= l; ++i)
        //    minimum_phase.log_spectrum[i] =
        //        Math.Log(spectrum[i] * (1.0 - aperiodic_ratio[i]) +
        //        ConstantNumbers.kMySafeGuardMinimum) / 2.0;
        unsafe
        {
            fixed (double* p_log_spectrum = minimum_phase.log_spectrum)
            fixed (double* p_spectrum = spectrum)
            fixed (double* p_aperiodic_ratio = aperiodic_ratio)
            {
                for (int i = 0, l = minimum_phase.fft_size / 2 + 1; i < l; ++i)
                    p_log_spectrum[i] =
                        Math.Log(p_spectrum[i] * (1.0 - p_aperiodic_ratio[i]) +
                        ConstantNumbers.kMySafeGuardMinimum) / 2.0;
            }
        }

        Common.GetMinimumPhaseSpectrum(ref minimum_phase);

        //for (int i = 0; i <= fft_size / 2; ++i)
        //{
        //    inverse_real_fft.spectrum[i].v0 =
        //      minimum_phase.minimum_phase_spectrum[i].v0;
        //    inverse_real_fft.spectrum[i].v1 =
        //      minimum_phase.minimum_phase_spectrum[i].v1;
        //}
        minimum_phase.minimum_phase_spectrum[0..(fft_size / 2 + 1)]
            .CopyTo(inverse_real_fft.spectrum);

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
        double[] spectral_envelope)
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
        double[] aperiodic_spectrum)
    {
        int current_frame_floor = Common.MyMinInt(f0_length - 1,
          (int)Math.Floor(current_time / frame_period));
        int current_frame_ceil = Common.MyMinInt(f0_length - 1,
          (int)Math.Ceiling(current_time / frame_period));
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

    private static void GetSpectralEnvelopeAndAperiodicRatio(double current_time, double frame_period,
        int f0_length, double[][] spectrogram, double[][] aperiodicity, int fft_size,
        double[] spectral_envelope, double[] aperiodic_spectrum)
    {
        int current_frame_floor = Common.MyMinInt(f0_length - 1,
          (int)Math.Floor(current_time / frame_period));
        int current_frame_ceil = Common.MyMinInt(f0_length - 1,
          (int)Math.Ceiling(current_time / frame_period));
        double interpolation = current_time / frame_period - current_frame_floor;

        int length = (fft_size / 2) + 1;

        int parallel_length = length / Vector<double>.Count;

        Span<Vector<double>> vec_spec_floor = MemoryMarshal.Cast<double, Vector<double>>(
            spectrogram[current_frame_floor].AsSpan(0..length));
        Span<Vector<double>> vec_aper_floor = MemoryMarshal.Cast<double, Vector<double>>(
            aperiodicity[current_frame_floor].AsSpan(0..length));

        if (current_frame_floor == current_frame_ceil)
        {
            int i;
            for (i = 0; i < parallel_length; ++i)
            {
                int destIdx = i * Vector<double>.Count;
                Vector.Abs(vec_spec_floor[i]).CopyTo(spectral_envelope, destIdx);
                vec_aper_floor[i].GetSafeAperiodicity().Pow2().CopyTo(aperiodic_spectrum, destIdx);
            }

            for (i *= Vector<double>.Count; i < length; ++i)
            {
                spectral_envelope[i] = Math.Abs(spectrogram[current_frame_floor][i]);
                aperiodic_spectrum[i] = Math.Pow(Common.GetSafeAperiodicity(aperiodicity[current_frame_floor][i]), 2.0);
            }
        }
        else
        {
            int i;

            Vector<double> vec_interp0 = new(interpolation);
            Vector<double> vec_interp1 = new(1.0 - interpolation);
            double interp1 = 1.0 - interpolation;
            Span<Vector<double>> vec_spec_ceil = MemoryMarshal.Cast<double, Vector<double>>(
                spectrogram[current_frame_ceil].AsSpan(0..length));
            Span<Vector<double>> vec_aper_ceil = MemoryMarshal.Cast<double, Vector<double>>(
                aperiodicity[current_frame_ceil].AsSpan(0..length));

            for (i = 0; i < parallel_length; ++i)
            {
                int destIdx = i * Vector<double>.Count;

                (vec_interp1 * Vector.Abs(vec_spec_floor[i]) + vec_interp0 * Vector.Abs(vec_spec_ceil[i]))
                    .CopyTo(spectral_envelope, destIdx);
                (vec_interp1 * vec_aper_floor[i].GetSafeAperiodicity() + vec_interp0 * (vec_aper_ceil[i].GetSafeAperiodicity())).Pow2()
                    .CopyTo(aperiodic_spectrum, destIdx);
            }

            for (i *= Vector<double>.Count; i < length; ++i)
            {
                spectral_envelope[i] =
                  interp1 * Math.Abs(spectrogram[current_frame_floor][i]) +
                  interpolation * Math.Abs(spectrogram[current_frame_ceil][i]);
                aperiodic_spectrum[i] = Math.Pow(
                    interp1 * Common.GetSafeAperiodicity(aperiodicity[current_frame_floor][i]) +
                    interpolation * Common.GetSafeAperiodicity(aperiodicity[current_frame_ceil][i]), 2.0);
            }
        }
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
        double[] response)
    {
        //double[] aperiodic_response = new double[fft_size];
        //double[] periodic_response = new double[fft_size];

        //double[] spectral_envelope = new double[fft_size];
        //double[] aperiodic_ratio = new double[fft_size];

        double[] aperiodic_response = ArrayUtil.Rent<double>(fft_size);
        double[] periodic_response = ArrayUtil.Rent<double>(fft_size);

        double[] spectral_envelope = ArrayUtil.Rent<double>(fft_size);
        double[] aperiodic_ratio = ArrayUtil.Rent<double>(fft_size);

        //GetSpectralEnvelope(current_time, frame_period, f0_length, spectrogram,
        //    fft_size, spectral_envelope);
        //GetAperiodicRatio(current_time, frame_period, f0_length, aperiodicity,
        //    fft_size, aperiodic_ratio);
        GetSpectralEnvelopeAndAperiodicRatio(current_time, frame_period, f0_length, spectrogram, aperiodicity,
            fft_size, spectral_envelope, aperiodic_ratio);

        // Synthesis of the periodic response
        GetPeriodicResponse(fft_size, spectral_envelope, aperiodic_ratio,
            current_vuv, ref inverse_real_fft, ref minimum_phase, dc_remover,
            fractional_time_shift, fs, periodic_response);

        // Synthesis of the aperiodic response
        GetAperiodicResponse(noise_size, fft_size, spectral_envelope,
            aperiodic_ratio, current_vuv, ref forward_real_fft,
            ref inverse_real_fft, ref minimum_phase, aperiodic_response);

        double sqrt_noise_size = Math.Sqrt(noise_size);
        //for (int i = 0; i < fft_size; ++i)
        //    response[i] =
        //      (periodic_response[i] * sqrt_noise_size + aperiodic_response[i]) /
        //      fft_size;
        {
            int vec_size = Vector<double>.Count;
            int vec_length = fft_size / Vector<double>.Count;

            var vec_sqrt_noise_size = new Vector<double>(sqrt_noise_size);
            var vec_fft_size = new Vector<double>(fft_size);
            var vec_periodic_response = periodic_response.AsSpan(0..fft_size).Cast<double, Vector<double>>();
            var vec_aperiodic_response = aperiodic_response.AsSpan(0..fft_size).Cast<double, Vector<double>>();

            int i;
            for (i = 0; i < vec_length; ++i)
            {
                ((vec_periodic_response[i] * vec_sqrt_noise_size + vec_aperiodic_response[i]) / vec_fft_size)
                    .CopyTo(response, i * vec_size);
            }

            for (i *= vec_size; i < fft_size; ++i)
            {
                response[i] = (periodic_response[i] * sqrt_noise_size + aperiodic_response[i]) / fft_size;
            }
        }

        // delete[] spectral_envelope;
        // delete[] aperiodic_ratio;
        // delete[] periodic_response;
        // delete[] aperiodic_response;

        ArrayUtil.Return(ref spectral_envelope);
        ArrayUtil.Return(ref aperiodic_ratio);
        ArrayUtil.Return(ref periodic_response);
        ArrayUtil.Return(ref aperiodic_response);
    }

    private static void GetTemporalParametersForTimeBase(double* f0, int f0_length,
            int fs, int y_length, double frame_period, double lowest_f0,
            double* time_axis, double* coarse_time_axis, double* coarse_f0,
            double* coarse_vuv)
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

    static int GetPulseLocationsForTimeBase(double* interpolated_f0,
        double* time_axis, int y_length, int fs, double* pulse_locations,
        int* pulse_locations_index, double* pulse_locations_time_shift)
    {
        //double[] total_phase = new double[y_length];
        //double[] wrap_phase = new double[y_length];
        //double[] wrap_phase_abs = new double[y_length - 1];

        int number_of_pulses = 0;
        double[] total_phase = ArrayUtil.Rent<double>(y_length);
        double[] wrap_phase = ArrayUtil.Rent<double>(y_length);
        double[] wrap_phase_abs = ArrayUtil.Rent<double>(y_length - 1);
        fixed (double* p_total_phase = total_phase)
        fixed (double* p_wrap_phase = wrap_phase)
        fixed (double* p_wrap_phase_abs = wrap_phase_abs)
        {

            p_total_phase[0] = 2.0 * ConstantNumbers.kPi * interpolated_f0[0] / fs;
            p_wrap_phase[0] = p_total_phase[0] % (2.0 * ConstantNumbers.kPi);
            for (int i = 1; i < y_length; ++i)
            {
                p_total_phase[i] = p_total_phase[i - 1] +
                  2.0 * ConstantNumbers.kPi * interpolated_f0[i] / fs;
                p_wrap_phase[i] = p_total_phase[i] % (2.0 * ConstantNumbers.kPi);
                p_wrap_phase_abs[i - 1] = Math.Abs(p_wrap_phase[i] - p_wrap_phase[i - 1]);
                test(i - 1, ref number_of_pulses, p_wrap_phase_abs, p_wrap_phase);
            }
            test(y_length - 1, ref number_of_pulses, p_wrap_phase_abs, p_wrap_phase);

            void test(int i, ref int number_of_pulses, double* p_wrap_phase_abs, double* p_wrap_phase)
            {
                if (p_wrap_phase_abs[i] > ConstantNumbers.kPi)
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
                    double y1 = p_wrap_phase[i] - 2.0 * ConstantNumbers.kPi;
                    double y2 = p_wrap_phase[i + 1];
                    double x = -y1 / (y2 - y1);
                    pulse_locations_time_shift[number_of_pulses] = x / fs;

                    ++number_of_pulses;
                }
            }
        }

        //int number_of_pulses = 0;
        //for (int i = 0; i < y_length - 1; ++i)
        //{
        //    if (wrap_phase_abs[i] > ConstantNumbers.kPi)
        //    {
        //        pulse_locations[number_of_pulses] = time_axis[i];
        //        pulse_locations_index[number_of_pulses] = i;

        //        // calculate the time shift in seconds between exact fractional pulse
        //        // position and the integer pulse position (sample i)
        //        // as we don't have access to the exact pulse position, we infer it
        //        // from the point between sample i and sample i + 1 where the
        //        // accummulated phase cross a multiple of 2pi
        //        // this point is found by solving y1 + x * (y2 - y1) = 0 for x, where y1
        //        // and y2 are the phases corresponding to sample i and i + 1, offset so
        //        // they cross zero; x >= 0
        //        double y1 = wrap_phase[i] - 2.0 * ConstantNumbers.kPi;
        //        double y2 = wrap_phase[i + 1];
        //        double x = -y1 / (y2 - y1);
        //        pulse_locations_time_shift[number_of_pulses] = x / fs;

        //        ++number_of_pulses;
        //    }
        //}

        //deelete[] wrap_phase_abs;
        //deelete[] wrap_phase;
        //deelete[] total_phase;

        ArrayUtil.Return(ref wrap_phase_abs);
        ArrayUtil.Return(ref wrap_phase);
        ArrayUtil.Return(ref total_phase);

        return number_of_pulses;
    }

    private static int GetTimeBase(double* f0, int f0_length, int fs,
        double frame_period, int y_length, double lowest_f0,
        double* pulse_locations, int* pulse_locations_index,
        double* pulse_locations_time_shift, double* interpolated_vuv)
    {
        //double[] time_axis = new double[y_length];
        //double[] coarse_time_axis = new double[f0_length + 1];
        //double[] coarse_f0 = new double[f0_length + 1];
        //double[] coarse_vuv = new double[f0_length + 1];

        var time_axis = ArrayUtil.Rent<double>(y_length);
        var coarse_time_axis = ArrayUtil.Rent<double>(f0_length + 1);
        var coarse_f0 = ArrayUtil.Rent<double>(f0_length + 1);
        var coarse_vuv = ArrayUtil.Rent<double>(f0_length + 1);

        double[] interpolated_f0 = ArrayUtil.Rent<double>(y_length);

        int number_of_pulses;
        fixed (double* p_time_axis = time_axis)
        fixed (double* p_coarse_time_axis = coarse_time_axis)
        fixed (double* p_coarse_f0 = coarse_f0)
        fixed (double* p_coarse_vuv = coarse_vuv)
        {
            GetTemporalParametersForTimeBase(f0, f0_length, fs, y_length, frame_period,
            lowest_f0, p_time_axis, p_coarse_time_axis, p_coarse_f0, p_coarse_vuv);
            //double[] interpolated_f0 = ArrayUtil.Rent<double>(y_length);
            fixed (double* p_interpolated_f0 = interpolated_f0)
            {
                MatlabFunctions.interp1(p_coarse_time_axis, p_coarse_f0, f0_length + 1,
                    p_time_axis, y_length, p_interpolated_f0);
                MatlabFunctions.interp1(p_coarse_time_axis, p_coarse_vuv, f0_length + 1,
                    p_time_axis, y_length, interpolated_vuv);

                for (int i = 0; i < y_length; ++i)
                {
                    //interpolated_vuv[i] = interpolated_vuv[i] > 0.5 ? 1.0 : 0.0;
                    //interpolated_f0[i] =
                    //  interpolated_vuv[i] == 0.0 ? ConstantNumbers.kDefaultF0 : interpolated_f0[i];

                    if (interpolated_vuv[i] > 0.5)
                    {
                        interpolated_vuv[i] = 1.0;
                    }
                    else
                    {
                        interpolated_vuv[i] = 0.0;
                        interpolated_f0[i] = ConstantNumbers.kDefaultF0;
                    }
                }

                number_of_pulses = GetPulseLocationsForTimeBase(p_interpolated_f0,
                    p_time_axis, y_length, fs, pulse_locations, pulse_locations_index,
                    pulse_locations_time_shift);
            }
        }

        //delete[] coarse_vuv;
        //delete[] coarse_f0;
        //delete[] coarse_time_axis;
        //delete[] time_axis;
        //delete[] interpolated_f0;

        ArrayUtil.Return(ref coarse_vuv);
        ArrayUtil.Return(ref coarse_f0);
        ArrayUtil.Return(ref coarse_time_axis);
        ArrayUtil.Return(ref time_axis);
        ArrayUtil.Return(ref interpolated_f0);

        return number_of_pulses;
    }

    private static void GetDCRemover(int fft_size, Span<double> dc_remover)
    {
        double dc_component = 0.0;
        for (int i = 0, l = fft_size / 2; i < l; ++i)
        {
            dc_remover[i] = 0.5 -
              0.5 * Math.Cos(2.0 * ConstantNumbers.kPi * (i + 1.0) / (1.0 + fft_size));
            // dc_remover[fft_size - i - 1] = dc_remover[i];
            dc_component += dc_remover[i] * 2.0;
        }
        for (int i = 0, l = fft_size / 2; i < l; ++i)
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

        // for (int i = 0; i < y_length; ++i) y[i] = 0.0;
        y[0..y_length].Clear();

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


        fixed (double* p_f0 = f0)
        fixed (double* p_pulse_locations = pulse_locations)
        fixed (int* p_pulse_locations_index = pulse_locations_index)
        fixed (double* p_pulse_locations_time_shift = pulse_locations_time_shift)
        fixed (double* p_interpolated_vuv = interpolated_vuv)
        {
            int number_of_pulses = GetTimeBase(p_f0, f0_length, fs, frame_period / 1000.0,
                y_length, fs / fft_size + 1.0, p_pulse_locations, p_pulse_locations_index,
                p_pulse_locations_time_shift, p_interpolated_vuv);

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
                //for (int j = lower_limit; j < upper_limit; ++j)
                //{
                //    index = j + offset;
                //    y[index] += impulse_response[j];
                //}
                VectorUtil.AddToSelf(y[(lower_limit + offset)..(upper_limit + offset)], impulse_response.AsSpan(lower_limit..upper_limit));
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
