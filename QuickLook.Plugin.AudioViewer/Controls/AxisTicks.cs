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
            double[] steps = { 0.5, 1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600 };
            foreach (var s in steps)
                if (s >= minStep) return s;
            return steps[steps.Length - 1];
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

        internal static string FormatTime(double seconds)
        {
            if (seconds < 0) seconds = 0;
            var t = TimeSpan.FromSeconds(seconds);
            return t.TotalHours >= 1
                ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
                : $"{(int)t.TotalMinutes}:{t.Seconds:00}";
        }
    }
}
