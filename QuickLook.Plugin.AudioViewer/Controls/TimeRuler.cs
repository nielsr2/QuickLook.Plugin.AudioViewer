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
        private Brush _text = Brushes.Gray;
        private Brush _line = Brushes.Gray;
        private readonly Typeface _face = new Typeface("Segoe UI");

        public void SetTheme(Color text, Color line)
        {
            _text = Frozen(text);
            _line = Frozen(line);
            InvalidateVisual();
        }

        public void Configure(double durationSeconds)
        {
            _duration = durationSeconds;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w <= 0 || _duration <= 0) return;

            double step = AxisTicks.TimeStepSeconds(_duration, w, 64);
            int decimals = AxisTicks.DecimalsForStep(step);
            var pen = new Pen(_line, 1); pen.Freeze();
            double dpi = PixelsPerDip();

            // Ticks are always drawn; labels are skipped when the previous one would still
            // be under them. FrequencyRuler does the same on its own axis.
            double lastLabelEnd = double.NegativeInfinity;

            foreach (var t in AxisTicks.TimeTicks(_duration, step))
            {
                double x = t / _duration * w;
                dc.DrawLine(pen, new Point(x, 0), new Point(x, 4));

                var ft = Text(AxisTicks.FormatTime(t, decimals), dpi);
                double tx = Math.Min(w - ft.Width, x + 2);
                tx = Math.Max(0, tx);
                if (tx < lastLabelEnd + 4) continue;

                dc.DrawText(ft, new Point(tx, 4));
                lastLabelEnd = tx + ft.Width;
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
