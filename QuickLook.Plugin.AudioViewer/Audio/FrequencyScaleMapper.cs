using System;
using QuickLook.Plugin.AudioViewer.Settings;

namespace QuickLook.Plugin.AudioViewer.Audio
{
    /// <summary>Inclusive range of FFT bins aggregated into one output pixel row.</summary>
    public struct BinRange
    {
        public int Lo;
        public int Hi;

        public BinRange(int lo, int hi)
        {
            Lo = lo;
            Hi = hi;
        }
    }

    /// <summary>
    /// Pure frequency-axis mapping for the spectrogram: converts an output row
    /// (0 = lowest frequency, rowCount-1 = highest) into the source FFT bins it
    /// represents, warped by the selected <see cref="FrequencyScale"/>. Kept free of
    /// WPF so it can be unit-tested.
    /// </summary>
    public static class FrequencyScaleMapper
    {
        public static double HzToMel(double hz) => 2595.0 * Math.Log10(1.0 + hz / 700.0);

        public static double MelToHz(double mel) => 700.0 * (Math.Pow(10.0, mel / 2595.0) - 1.0);

        /// <summary>Map a fraction u in [0,1] to a frequency in [fMin,fMax] under the given scale.</summary>
        public static double FractionToFrequency(double u, FrequencyScale scale, double fMin, double fMax)
        {
            if (u < 0) u = 0; else if (u > 1) u = 1;
            switch (scale)
            {
                case FrequencyScale.Log:
                    double lo = Math.Max(fMin, 1e-6); // log needs a positive lower bound
                    return lo * Math.Pow(fMax / lo, u);
                case FrequencyScale.Mel:
                    double m0 = HzToMel(fMin), m1 = HzToMel(fMax);
                    return MelToHz(m0 + u * (m1 - m0));
                default: // Linear
                    return fMin + u * (fMax - fMin);
            }
        }

        /// <summary>Center frequency of a given row (uses the row's cell center).</summary>
        public static double RowCenterFrequency(int row, int rowCount, FrequencyScale scale, double fMin, double fMax)
            => FractionToFrequency(rowCount <= 1 ? 0.0 : (row + 0.5) / rowCount, scale, fMin, fMax);

        /// <summary>Lowest frequency shown; 0 for linear/mel, one-bin/20 Hz floor for log.</summary>
        public static double EffectiveFMin(FrequencyScale scale, int fftSize, int sampleRate)
        {
            if (scale == FrequencyScale.Log)
                return Math.Max(20.0, (double)sampleRate / fftSize);
            return 0.0;
        }

        /// <summary>
        /// For a spectrogram of <paramref name="rowCount"/> pixel rows, returns the inclusive
        /// FFT-bin range each row aggregates. fMaxHz &lt;= 0 means full range (Nyquist).
        /// </summary>
        public static BinRange[] BuildRowBins(int rowCount, FrequencyScale scale, int fftSize, int sampleRate, double fMaxHz)
        {
            if (rowCount < 1) rowCount = 1;
            int bins = fftSize / 2;
            int maxBin = bins - 1;
            double nyquist = sampleRate / 2.0;
            double fMax = fMaxHz > 0 ? Math.Min(fMaxHz, nyquist) : nyquist;
            double fMin = EffectiveFMin(scale, fftSize, sampleRate);
            if (fMax <= fMin) fMax = nyquist;

            double hzPerBin = (double)sampleRate / fftSize;
            var result = new BinRange[rowCount];
            for (int r = 0; r < rowCount; r++)
            {
                double fLo = FractionToFrequency((double)r / rowCount, scale, fMin, fMax);
                double fHi = FractionToFrequency((double)(r + 1) / rowCount, scale, fMin, fMax);
                int bLo = (int)Math.Floor(fLo / hzPerBin);
                int bHi = (int)Math.Floor(fHi / hzPerBin);
                if (bLo < 0) bLo = 0; else if (bLo > maxBin) bLo = maxBin;
                if (bHi < 0) bHi = 0; else if (bHi > maxBin) bHi = maxBin;
                if (bHi < bLo) bHi = bLo; // every row spans at least one bin
                result[r] = new BinRange(bLo, bHi);
            }
            return result;
        }
    }
}
