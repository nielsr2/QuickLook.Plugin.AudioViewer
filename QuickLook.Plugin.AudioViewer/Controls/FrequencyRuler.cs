using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using QuickLook.Plugin.AudioViewer.Audio;
using QuickLook.Plugin.AudioViewer.Settings;

namespace QuickLook.Plugin.AudioViewer.Controls
{
    /// <summary>
    /// Frequency axis drawn to the left of the spectrogram. Ticks are positioned with the
    /// exact same scale mapping (<see cref="FrequencyScaleMapper"/>) and range the image
    /// uses, so labels line up with the colored rows under Linear/Log/Mel and any max-freq.
    /// <see cref="TopFraction"/> is the vertical fraction where the spectrogram begins
    /// (0 in single-view, 0.2 when stacked below the waveform).
    /// </summary>
    public sealed class FrequencyRuler : FrameworkElement
    {
        private FrequencyScale _scale = FrequencyScale.Linear;
        private double _fMin, _fMax, _topFraction;
        private Brush _text = Brushes.Gray;
        private Brush _line = Brushes.Gray;
        private readonly Typeface _face = new Typeface("Segoe UI");

        public void SetTheme(Color text, Color line)
        {
            _text = Frozen(text);
            _line = Frozen(line);
            InvalidateVisual();
        }

        public void Configure(FrequencyScale scale, double fMin, double fMax, double topFraction)
        {
            _scale = scale;
            _fMin = fMin;
            _fMax = fMax;
            _topFraction = topFraction;
            InvalidateVisual();
        }

        // OnRender is not re-invoked for a size change alone, and every tick position here
        // is derived from ActualHeight. See the note on TimeRuler.OnRenderSizeChanged.
        protected override void OnRenderSizeChanged(SizeChangedInfo info)
        {
            base.OnRenderSizeChanged(info);
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w <= 0 || h <= 0 || _fMax <= _fMin) return;

            double top = _topFraction * h;
            double band = h - top;           // vertical extent of the spectrogram
            var pen = new Pen(_line, 1); pen.Freeze();
            double dpi = PixelsPerDip();
            double lastY = double.NegativeInfinity;

            foreach (var f in AxisTicks.NiceFrequencies(_fMin, _fMax))
            {
                double u = FrequencyScaleMapper.FrequencyToFraction(f, _scale, _fMin, _fMax);
                double y = h - u * band;                 // u=0 -> bottom, u=1 -> top of the band
                if (y < top - 0.5 || y > h + 0.5) continue;
                if (Math.Abs(y - lastY) < 13) continue;  // de-clutter overlapping labels
                lastY = y;

                dc.DrawLine(pen, new Point(w - 4, y), new Point(w, y));
                var ft = Text(AxisTicks.FormatHz(f), dpi);
                dc.DrawText(ft, new Point(w - 6 - ft.Width, y - ft.Height / 2));
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
