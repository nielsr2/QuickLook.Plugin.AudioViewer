using System;

namespace QuickLook.Plugin.AudioViewer.Audio
{
    /// <summary>
    /// Chooses a vertical magnification for a waveform. Pure and WPF-free, mirroring
    /// <see cref="StereoWaveform"/>: the scan stays the source of truth and the gain is
    /// applied at display time, so changing it costs a re-raster and never a re-scan.
    /// </summary>
    internal static class WaveformGain
    {
        /// <summary>Ceiling on magnification. +36 dB is already far past useful.</summary>
        internal const double MaxGain = 64.0;

        /// <summary>Discrete steps offered in the UI: 6 dB apart, matching the dBFS ruler.</summary>
        internal static readonly double[] Steps = { 1, 2, 4, 8, 16, 32, 64 };

        /// <summary>
        /// Magnification that brings the <paramref name="percentile"/> column peak up to
        /// <paramref name="headroom"/> of the lane.
        /// </summary>
        /// <remarks>
        /// Deliberately a percentile rather than the true peak. A quiet field recording
        /// with one door slam or mic bump has a near-full-scale maximum, so peak
        /// normalisation returns ~1.0 and leaves the waveform as flat as it started —
        /// exactly the case this feature exists for. Ignoring the top 1% of columns fits
        /// the body of the material instead, and the clip indicator marks the outliers
        /// that end up cut off.
        ///
        /// Never returns less than 1: this magnifies quiet files and does not shrink loud
        /// ones, which would be a surprising thing for a "zoom" to do on its own.
        /// </remarks>
        internal static double FitToPercentile(WaveformData wf, double percentile = 0.99,
            double headroom = 0.9, double maxGain = MaxGain)
        {
            if (wf == null) return 1.0;
            int channels = wf.ChannelCount, columns = wf.Columns;
            if (channels == 0 || columns == 0) return 1.0;

            if (percentile <= 0 || percentile > 1) percentile = 0.99;
            if (headroom <= 0 || headroom > 1) headroom = 0.9;

            // One magnitude per column per channel: how far that column reaches from zero.
            var peaks = new double[channels * columns];
            int n = 0;
            for (int c = 0; c < channels; c++)
            {
                var min = wf.Min[c];
                var max = wf.Max[c];
                for (int x = 0; x < columns; x++)
                {
                    double a = Math.Abs(max[x]);
                    double b = Math.Abs(min[x]);
                    peaks[n++] = a > b ? a : b;
                }
            }

            Array.Sort(peaks);
            int idx = (int)Math.Floor(percentile * (peaks.Length - 1));
            if (idx < 0) idx = 0; else if (idx > peaks.Length - 1) idx = peaks.Length - 1;
            double reference = peaks[idx];

            // Digital silence, or so close that any gain would just amplify noise into a
            // meaningless wall. Leave it alone rather than showing a full-height mess.
            if (reference < 1e-6) return 1.0;

            double gain = headroom / reference;
            if (gain < 1.0) gain = 1.0;
            if (gain > maxGain) gain = maxGain;
            return gain;
        }

        /// <summary>The next step up or down the <see cref="Steps"/> ladder from <paramref name="gain"/>.</summary>
        internal static double Step(double gain, int direction)
        {
            if (direction == 0) return gain;
            if (direction > 0)
            {
                foreach (var s in Steps)
                    if (s > gain + 1e-9) return s;
                return Steps[Steps.Length - 1];
            }
            for (int i = Steps.Length - 1; i >= 0; i--)
                if (Steps[i] < gain - 1e-9) return Steps[i];
            return Steps[0];
        }

        /// <summary>
        /// The gain as a label, e.g. "x8" or "x2.5". Shown whenever magnification is
        /// active so a stretched waveform is never mistaken for the file's real level.
        /// </summary>
        internal static string FormatGain(double gain)
            => Math.Abs(gain - Math.Round(gain)) < 0.05
                ? "×" + Math.Round(gain).ToString("0")
                : "×" + gain.ToString("0.0");
    }
}
