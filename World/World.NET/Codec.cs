using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using World.NET.Structs.Common;
using World.NET.Structs.Fft;
using World.NET.Utils;

namespace World.NET;

public static unsafe class Codec
{
    /// <summary>
    /// Aperiodicity is initialized by the value 1.0 - world::kMySafeGuardMinimum. 
    /// This value means the frame/frequency index is aperiodic.
    /// </summary>
    private static void InitializeAperiodicity(int f0_length, int fft_size,
        double[][] aperiodicity)
    {
        //for (int i = 0; i < f0_length; ++i)
        //    for (int j = 0; j < fft_size / 2 + 1; ++j)
        //        aperiodicity[i][j] = 1.0 - ConstantNumbers.kMySafeGuardMinimum;

        double value = 1.0 - ConstantNumbers.kMySafeGuardMinimum;
        for (int i = 0; i < f0_length; ++i)
            aperiodicity[i].AsSpan(0..(fft_size / 2 + 1)).Fill(value);
    }

    /// <summary>
    /// This function identifies whether this frame is voiced or unvoiced.
    /// </summary>
    private static int CheckVUV(double* coarse_aperiodicity,
        int number_of_aperiodicities, double* tmp_aperiodicity)
    {
        double tmp = 0.0;
        for (int i = 0; i < number_of_aperiodicities; ++i)
        {
            tmp += coarse_aperiodicity[i];
            tmp_aperiodicity[i + 1] = coarse_aperiodicity[i];
        }
        tmp /= number_of_aperiodicities;

        return tmp > -0.5 ? 1 : 0;  // -0.5 is not optimized, but okay.
    }

    /// <summary>
    /// Aperiodicity is obtained from the coded aperiodicity.
    /// </summary>
    private static void GetAperiodicity(double* coarse_frequency_axis,
        double* coarse_aperiodicity, int number_of_aperiodicities,
        double* frequency_axis, int fft_size, double* aperiodicity)
    {
        MatlabFunctions.interp1(coarse_frequency_axis, coarse_aperiodicity,
            number_of_aperiodicities + 2, frequency_axis, fft_size / 2 + 1,
            aperiodicity);
        // for (int i = 0; i <= fft_size / 2; ++i)
        for (int i = 0, l = fft_size / 2 + 1; i < l; ++i)
            aperiodicity[i] = Math.Pow(10.0, aperiodicity[i] / 20.0);
    }

    /// <summary>
    /// Frequency is converted into its mel representation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double FrequencyToMel(double frequency)
            => ConstantNumbers.kM0 * Math.Log(frequency / ConstantNumbers.kF0 + 1.0);

    /// <summary>
    /// Mel is converted into frequency.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double MelToFrequency(double mel)
        => ConstantNumbers.kF0 * (Math.Exp(mel / ConstantNumbers.kM0) - 1.0);

    /// <summary>
    /// IDCT for spectral envelope decoding
    /// </summary>
    private static void IDCTForCodec(double[] mel_cepstrum, int max_dimension,
        Span<fft_complex> weight, ref InverseComplexFFT inverse_complex_fft,
        int number_of_dimensions, Span<double> mel_spectrum)
    {
        double normalization = Math.Sqrt(inverse_complex_fft.fft_size);
        for (int i = 0; i < number_of_dimensions; ++i)
        {
            //inverse_complex_fft.input[i].v0 =
            //  mel_cepstrum[i] * weight[i].v0 * normalization;
            //inverse_complex_fft.input[i].v1 =
            //  -mel_cepstrum[i] * weight[i].v1 * normalization;

            inverse_complex_fft.input[i].Set(
                mel_cepstrum[i] * weight[i].v0 * normalization,
                -mel_cepstrum[i] * weight[i].v1 * normalization);
        }
        //for (int i = number_of_dimensions; i < max_dimension; ++i)
        //{
        //    inverse_complex_fft.input[i].v0 = 0.0;
        //    inverse_complex_fft.input[i].v1 = 0.0;
        //}
        inverse_complex_fft.input[number_of_dimensions..max_dimension].Clear();

        Fft.fft_execute(ref inverse_complex_fft.inverse_fft);

        for (int i = 0; i < max_dimension / 2; ++i)
        {
            mel_spectrum[i * 2] = inverse_complex_fft.output[i].v0;
            mel_spectrum[(i * 2) + 1] =
              inverse_complex_fft.output[max_dimension - i - 1].v0;
        }
    }

    /// <summary>
    /// Coded spectral envelope in a frame is decoded
    /// </summary>
    private static void DecodeOneFrame(double[] coded_spectral_envelope,
        double* frequency_axis, int fft_size, double* mel_axis,
        fft_complex[] weight, int max_dimension, int number_of_dimensions,
        ref InverseComplexFFT inverse_complex_fft, double* spectral_envelope)
    {
        double[] mel_spectrum = ArrayUtil.Rent<double>(max_dimension + 2);

        // IDCT
        IDCTForCodec(coded_spectral_envelope, max_dimension, weight,
            ref inverse_complex_fft, number_of_dimensions, mel_spectrum.AsSpan(1..));
        fixed (double* p_mel_spectrum = mel_spectrum)
        {
            p_mel_spectrum[0] = p_mel_spectrum[1];
            p_mel_spectrum[max_dimension + 1] = p_mel_spectrum[max_dimension];

            MatlabFunctions.interp1(mel_axis, p_mel_spectrum, max_dimension + 2, frequency_axis,
                fft_size / 2 + 1, spectral_envelope);
        }

        for (int i = 0; i < fft_size / 2 + 1; ++i)
            spectral_envelope[i] = Math.Exp(spectral_envelope[i] / max_dimension);

        ArrayUtil.Return(ref mel_spectrum);
    }

    /// <summary>
    /// GetParameters() generates the required parameters.
    /// </summary>
    private static void GetParametersForDecoding(double floor_frequency,
        double ceil_frequency, int fs, int fft_size, int number_of_dimensions,
        double[] mel_axis, double[] frequency_axis, fft_complex[] weight)
    {
        int max_dimension = fft_size / 2;
        double floor_mel = FrequencyToMel(floor_frequency);
        double ceil_mel = FrequencyToMel(ceil_frequency);

        // Generate the weighting vector for IDCT.
        double sqrt = Math.Sqrt(fft_size);
        double temp;
        for (int i = 0; i < number_of_dimensions; ++i)
        {
            //weight[i].v0 = Math.Cos(i * ConstantNumbers.kPi / fft_size) * Math.Sqrt(fft_size);
            //weight[i].v1 = Math.Sin(i * ConstantNumbers.kPi / fft_size) * Math.Sqrt(fft_size);
            temp = i * ConstantNumbers.kPi / fft_size;
            weight[i].Set(Math.Cos(temp) * sqrt, Math.Sin(temp) * sqrt);
        }
        weight[0].v0 /= Math.Sqrt(2.0);
        // Generate the mel axis for IDCT.
        for (int i = 0; i < max_dimension; ++i)
            mel_axis[i + 1] =
              MelToFrequency((ceil_mel - floor_mel) * i / max_dimension + floor_mel);
        mel_axis[0] = 0;
        mel_axis[max_dimension + 1] = fs / 2.0;

        // Generate the frequency axis
        for (int i = 0; i < fft_size / 2 + 1; ++i)
            frequency_axis[i] = (double)i * fs / fft_size;
    }

    /// <summary>
    /// GetNumberOfAperiodicities provides the number of dimensions for aperiodicity coding.It is determined by only fs.
    /// </summary>
    /// <param name="fs">Sampling frequency</param>
    /// <returns>Number of aperiodicities</returns>
    public static int GetNumberOfAperiodicities(int fs)
        => (int)(Common.MyMinDouble(ConstantNumbers.kUpperLimit, fs / 2.0 -
          ConstantNumbers.kFrequencyInterval) / ConstantNumbers.kFrequencyInterval);

    /// <summary>
    /// DecodeAperiodicity decodes the coded aperiodicity.
    /// </summary>
    /// <param name="coded_aperiodicity">Coded aperiodicity</param>
    /// <param name="f0_length">Length of F0 contour</param>
    /// <param name="fs">Sampling frequency</param>
    /// <param name="fft_size">FFT size</param>
    /// <param name="aperiodicity">Decoded aperiodicity</param>
    public static void DecodeAperiodicity(double[][] coded_aperiodicity,
        int f0_length, int fs, int fft_size, double[][] aperiodicity)
    {
        InitializeAperiodicity(f0_length, fft_size, aperiodicity);
        int number_of_aperiodicities = GetNumberOfAperiodicities(fs);
        double[] frequency_axis = ArrayUtil.Rent<double>(fft_size / 2 + 1);
        for (int i = 0; i <= fft_size / 2; ++i)
            frequency_axis[i] = (double)fs / fft_size * i;

        double[] coarse_frequency_axis = ArrayUtil.Rent<double>(number_of_aperiodicities + 2);
        for (int i = 0; i <= number_of_aperiodicities; ++i)
            coarse_frequency_axis[i] = i * ConstantNumbers.kFrequencyInterval;
        coarse_frequency_axis[number_of_aperiodicities + 1] = fs / 2.0;

        //double[] coarse_aperiodicity = ArrayUtil.Rent<double>(number_of_aperiodicities + 2);
        //coarse_aperiodicity[0] = -60.0;
        //coarse_aperiodicity[number_of_aperiodicities + 1] =
        //  -ConstantNumbers.kMySafeGuardMinimum;

        //for (int i = 0; i < f0_length; ++i)
        //{
        //    if (CheckVUV(coded_aperiodicity[i], number_of_aperiodicities,
        //      coarse_aperiodicity) == 1) continue;
        //    GetAperiodicity(coarse_frequency_axis, coarse_aperiodicity,
        //        number_of_aperiodicities, frequency_axis, fft_size, aperiodicity[i]);
        //}

        Parallel.ForEach(Partitioner.Create(0, f0_length), t =>
        {
            double[] coarse_aperiodicity = ArrayUtil.Rent<double>(number_of_aperiodicities + 2);
            fixed (double* p_coarse_aperiodicity = coarse_aperiodicity)
            fixed (double* p_coarse_frequency_axis = coarse_frequency_axis)
            fixed (double* p_frequency_axis = frequency_axis)
            {

                p_coarse_aperiodicity[0] = -60.0;
                p_coarse_aperiodicity[number_of_aperiodicities + 1] =
                  -ConstantNumbers.kMySafeGuardMinimum;

                for (int i = t.Item1; i < t.Item2; ++i)
                {
                    fixed (double* p_coded_aperiodicity = coded_aperiodicity[i])
                    fixed (double* p_aperiodicity = aperiodicity[i])
                    {
                        if (CheckVUV(p_coded_aperiodicity, number_of_aperiodicities,
                          p_coarse_aperiodicity) == 1) continue;
                        GetAperiodicity(p_coarse_frequency_axis, p_coarse_aperiodicity,
                            number_of_aperiodicities, p_frequency_axis, fft_size, p_aperiodicity);
                    }
                }
            }

            ArrayUtil.Return(ref coarse_aperiodicity);
        });

        // ArrayUtil.Return(ref coarse_aperiodicity);
        ArrayUtil.Return(ref coarse_frequency_axis);
        ArrayUtil.Return(ref frequency_axis);
    }

    public static void DecodeSpectralEnvelope(double[][] coded_spectral_envelope,
        int f0_length, int fs, int fft_size, int number_of_dimensions,
        double[][] spectrogram)
    {
        double[] mel_axis = ArrayUtil.Rent<double>(fft_size / 2 + 2);
        double[] frequency_axis = ArrayUtil.Rent<double>(fft_size / 2 + 1);
        fft_complex[] weight = ArrayUtil.Rent<fft_complex>(fft_size / 2);

        // Generation of the required parameters
        GetParametersForDecoding(ConstantNumbers.kFloorFrequency,
            Common.MyMinDouble(fs / 2.0, ConstantNumbers.kCeilFrequency),
            fs, fft_size, number_of_dimensions, mel_axis, frequency_axis, weight);

        //InverseComplexFFT inverse_complex_fft = default;
        //Common.InitializeInverseComplexFFT(fft_size / 2, ref inverse_complex_fft);

        //for (int i = 0; i < f0_length; ++i)
        //{
        //    DecodeOneFrame(coded_spectral_envelope[i], frequency_axis, fft_size,
        //        mel_axis, weight, fft_size / 2, number_of_dimensions,
        //        ref inverse_complex_fft, spectrogram[i]);
        //}

        //Common.DestroyInverseComplexFFT(ref inverse_complex_fft);

        Parallel.ForEach(Partitioner.Create(0, f0_length), t =>
        {
            InverseComplexFFT inverse_complex_fft = default;
            Common.InitializeInverseComplexFFT(fft_size / 2, ref inverse_complex_fft);

            fixed (double* p_mel_axis = mel_axis)
            fixed (double* p_frequency_axis = frequency_axis)
            {
                for (int i = t.Item1; i < t.Item2; ++i)
                {
                    fixed (double* p_spectrogram_i = spectrogram[i])
                    {
                        DecodeOneFrame(coded_spectral_envelope[i], p_frequency_axis, fft_size,
                            p_mel_axis, weight, fft_size / 2, number_of_dimensions,
                            ref inverse_complex_fft, p_spectrogram_i);
                    }
                }
            }

            Common.DestroyInverseComplexFFT(ref inverse_complex_fft);
        });

        ArrayUtil.Return(ref weight);
        ArrayUtil.Return(ref frequency_axis);
        ArrayUtil.Return(ref mel_axis);
    }
}
