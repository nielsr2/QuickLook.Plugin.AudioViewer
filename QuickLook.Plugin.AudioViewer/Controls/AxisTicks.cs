using System;
using System.Collections.Generic;

namespace QuickLook.Plugin.AudioViewer.Controls
{
    /// <summary>
    /// Pure helpers for choosing spectrogram axis ticks and formatting labels. Kept free
    /// of WPF so the tick/label logic is unit-tested; the ruler controls handle drawing
    /// (and pixel-based de-cluttering).
    /// </summary>
    internal static class AxisTicks
    {
        /// <summary>
        /// Candidate "nice" frequencies (a 1-2-5 series) that fall within [fMin, fMax].
        /// The frequency ruler positions each via the scale mapping and drops any that
        /// would overlap the previous label, so this can return a generous set.
        /// </summary>
        internal static double[] NiceFrequencies(double fMin, double fMax)
        {
            var ticks = new List<double>();
            double lo = Math.Max(fMin, 1.0);
            for (double decade = 1.0; decade <= 100000.0; decade *= 10.0)
            {
                foreach (var mult in new[] { 1.0, 2.0, 5.0 })
                {
                    double f = decade * mult;
                    if (f >= lo && f <= fMax) ticks.Add(f);
                }
            }
            return ticks.ToArray();
        }

        /// <summary>
        /// A "nice" time step (seconds) so labels are at least <paramref name="minPxPerLabel"/>
        /// apart given the pixel width of the whole duration.
        /// </summary>
        internal static double TimeStepSeconds(double durationSeconds, double widthPixels, double minPxPerLabel)
        {
            if (durationSeconds <= 0 || widthPixels <= 0) return 1.0;
            double pxPerSec = widthPixels / durationSeconds;
            double minStep = minPxPerLabel / pxPerSec;
            // The ladder previously bottomed out at 0.5 s. On a short file that forced
            // half-second ticks to be labelled with whole-second precision, so a 2 s clip
            // rendered "0:00, 0:00, 0:01, 0:01, 0:02" — every label duplicated. Sub-second
            // steps let DecimalsForStep give those ticks the precision they need.
            double[] steps = { 0.001, 0.002, 0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5,
                               1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600 };
            foreach (var s in steps)
                if (s >= minStep) return s;
            return steps[steps.Length - 1];
        }

        /// <summary>
        /// How many fractional-second digits a tick label needs so that adjacent ticks
        /// spaced <paramref name="stepSeconds"/> apart do not render identically.
        /// </summary>
        internal static int DecimalsForStep(double stepSeconds)
        {
            if (stepSeconds >= 1.0) return 0;
            if (stepSeconds >= 0.1) return 1;
            if (stepSeconds >= 0.01) return 2;
            return 3;
        }

        internal static double[] TimeTicks(double durationSeconds, double stepSeconds)
        {
            if (stepSeconds <= 0 || durationSeconds <= 0) return new double[0];
            var ticks = new List<double>();
            for (double t = 0; t <= durationSeconds + 1e-9; t += stepSeconds)
                ticks.Add(t);
            return ticks.ToArray();
        }

        internal static string FormatHz(double hz)
        {
            if (hz >= 1000.0)
            {
                double k = hz / 1000.0;
                return (Math.Abs(k - Math.Round(k)) < 0.05 ? Math.Round(k).ToString("0") : k.ToString("0.0")) + " kHz";
            }
            return Math.Round(hz).ToString("0") + " Hz";
        }

        /// <param name="decimals">
        /// Fractional-second digits (0-3). Use <see cref="DecimalsForStep"/> to derive this
        /// from the axis tick spacing so short files do not render duplicate labels.
        /// </param>
        internal static string FormatTime(double seconds, int decimals = 0)
        {
            if (seconds < 0) seconds = 0;
            if (decimals < 0) decimals = 0; else if (decimals > 3) decimals = 3;
            var t = TimeSpan.FromSeconds(seconds);

            // Build the fraction from the rounded total so 1.9995 s at 3 dp does not
            // render as "0:01.1000" — carry has to reach the seconds field.
            double scale = Math.Pow(10, decimals);
            double rounded = Math.Round(seconds * scale, MidpointRounding.AwayFromZero) / scale;
            t = TimeSpan.FromSeconds(rounded);

            string frac = decimals == 0
                ? string.Empty
                : "." + ((int)Math.Round((rounded - Math.Floor(rounded)) * scale)).ToString(new string('0', decimals));

            return t.TotalHours >= 1
                ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}{frac}"
                : $"{(int)t.TotalMinutes}:{t.Seconds:00}{frac}";
        }

        /// <summary>
        /// dBFS levels worth labelling on a waveform's amplitude axis, coarsest first.
        /// A curated ladder rather than a 1-2-5 series: these are the values engineers
        /// actually reference, and a linear-amplitude axis crowds them toward the centre
        /// line fast, so the ruler takes as many as fit and drops the rest.
        /// </summary>
        internal static readonly double[] NiceDbFs = { 0, -3, -6, -12, -20, -30, -40, -60 };

        /// <summary>
        /// Where a dBFS level sits within the half-lane, as a fraction of it, at a given
        /// magnification. Returns &gt; 1 when the level is magnified past the lane edge.
        /// </summary>
        internal static double DbToFraction(double db, double gain)
        {
            if (gain <= 0) gain = 1.0;
            return gain * Math.Pow(10.0, db / 20.0);
        }

        internal static string FormatDbFs(double db)
            => Math.Abs(db) < 0.05 ? "0 dB" : db.ToString("0") + " dB";

        /// <summary>
        /// Formats a raw linear FFT magnitude for the colorbar legend.
        ///
        /// These span many orders of magnitude and are not normalised: at the default
        /// -90 dB contrast floor the bottom of the scale is ~31 600x smaller than the top,
        /// so a fixed "0.00" renders the bottom tick as "0.00" for essentially every file,
        /// and on quiet material collapses all three ticks to the same string. Keeps three
        /// significant figures, dropping to exponent form once fixed-point would be nothing
        /// but leading zeroes.
        /// </summary>
        internal static string FormatMagnitude(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "—";
            double a = Math.Abs(v);
            if (a == 0) return "0";
            if (a >= 0.1) return v.ToString("0.000");
            if (a >= 0.001) return v.ToString("0.00000");
            return v.ToString("0.00e+0");
        }
    }
}
