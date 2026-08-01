using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace QuickLook.Plugin.AudioViewer.Controls
{
    /// <summary>
    /// Time axis drawn below the spectrogram. Its width matches the full timeline width
    /// (so ticks align with the spectrogram columns) and it is translated horizontally to
    /// follow the shared scroll offset.
    /// </summary>
    public sealed class TimeRuler : FrameworkElement
    {
        private double _duration;
        private double _minPxPerLabel = 64;
        private Brush _text = Brushes.Gray;
        private Brush _line = Brushes.Gray;
        private readonly Typeface _face = new Typeface("Segoe UI");

        public void SetTheme(Color text, Color line)
        {
            _text = Frozen(text);
            _line = Frozen(line);
            InvalidateVisual();
        }

        /// <param name="minPxPerLabel">
        /// Minimum spacing between labels, which is what picks the step off the ladder.
        /// Larger means fewer, coarser marks.
        /// </param>
        public void Configure(double durationSeconds, double minPxPerLabel = 64)
        {
            _duration = durationSeconds;
            _minPxPerLabel = minPxPerLabel > 8 ? minPxPerLabel : 64;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w <= 0 || _duration <= 0) return;

            double step = AxisTicks.TimeStepSeconds(_duration, w, _minPxPerLabel);
            int decimals = AxisTicks.DecimalsForStep(step);
            var pen = new Pen(_line, 1); pen.Freeze();
            double dpi = PixelsPerDip();

            // Ticks are always drawn; labels are skipped when the previous one would still
            // be under them. FrequencyRuler does the same on its own axis.
            //
            // De-cluttering compares the UNCLAMPED positions. Comparing the clamped ones is
            // what broke the axis: every label past (w - textWidth) was pulled back to the
            // same x, so the first of them set lastLabelEnd near the ruler's right edge and
            // every later label then tested as "overlapping" and was dropped. On a long file
            // the axis simply stopped part way through.
            double lastLabelEnd = double.NegativeInfinity;
            var ticks = AxisTicks.TimeTicks(_duration, step);

            for (int i = 0; i < ticks.Length; i++)
            {
                double t = ticks[i];
                double x = t / _duration * w;
                dc.DrawLine(pen, new Point(x, 0), new Point(x, 4));

                if (x < lastLabelEnd + 4) continue;

                var ft = Text(AxisTicks.FormatTime(t, decimals), dpi);
                lastLabelEnd = x + ft.Width;

                // Only the final label is nudged left to stay on the ruler; pulling interior
                // labels back would misplace them relative to their own tick.
                double tx = i == ticks.Length - 1 ? Math.Min(w - ft.Width, x + 2) : x + 2;
                dc.DrawText(ft, new Point(Math.Max(0, tx), 4));
            }
        }

        private FormattedText Text(string s, double dpi) => new FormattedText(
            s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _face, 10, _text, dpi);

        private double PixelsPerDip()
        {
            try { return VisualTreeHelper.GetDpi(this).PixelsPerDip; }
            catch { return 1.0; }
        }

        private static Brush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }
    }
}
