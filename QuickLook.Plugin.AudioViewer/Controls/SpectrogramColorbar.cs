using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using QuickLook.Plugin.AudioViewer.Audio;
using QuickLook.Plugin.AudioViewer.Settings;

namespace QuickLook.Plugin.AudioViewer.Controls
{
    /// <summary>
    /// Vertical colorbar legend to the right of the spectrogram: a gradient of the active
    /// colormap with top/mid/bottom labels drawn from the render's actual normalization
    /// levels (dB in decibel mode, relative magnitude in linear mode).
    /// </summary>
    public sealed class SpectrogramColorbar : FrameworkElement
    {
        private const double BarWidth = 12;

        private SpectrogramColormap _colormap = SpectrogramColormap.Magma;
        private SpectrogramLevels _levels;
        private double _topFraction;
        private bool _reverse;
        private double _gamma = 1.0;
        private bool _configured;
        private Brush _text = Brushes.Gray;
        private readonly Typeface _face = new Typeface("Segoe UI");

        public void SetTheme(Color text)
        {
            _text = Frozen(text);
            InvalidateVisual();
        }

        public void Configure(SpectrogramColormap map, SpectrogramLevels levels, double topFraction,
            bool reverse = false, double gamma = 1.0)
        {
            _colormap = map;
            _levels = levels;
            _topFraction = topFraction;
            _reverse = reverse;
            _gamma = gamma > 0 ? gamma : 1.0;
            _configured = true;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w <= 0 || h <= 0 || !_configured) return;

            double top = _topFraction * h;
            double band = h - top;
            if (band <= 1) return;

            // Gradient bar (top = brightest = ceiling).
            var grad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            const int stops = 24;
            for (int i = 0; i <= stops; i++)
            {
                double frac = 1.0 - (double)i / stops;
                // Match the image: apply the same gamma+reverse transform to the
                // normalized fraction before sampling, so the legend colors are truthful.
                // The dB/magnitude labels stay at their linear positions (LabelFor).
                double ct = Math.Pow(frac, _gamma);
                if (_reverse) ct = 1 - ct;
                Colormaps.Sample(_colormap, ct, out byte r, out byte g, out byte b);
                grad.GradientStops.Add(new GradientStop(Color.FromRgb(r, g, b), (double)i / stops));
            }
            grad.Freeze();
            dc.DrawRectangle(grad, null, new Rect(0, top, BarWidth, band));

            double dpi = PixelsPerDip();
            DrawLabel(dc, LabelFor(1.0), top, VAlign.Top, dpi);
            DrawLabel(dc, LabelFor(0.5), top + band / 2, VAlign.Center, dpi);
            DrawLabel(dc, LabelFor(0.0), h, VAlign.Bottom, dpi);
        }

        private string LabelFor(double frac)
        {
            double v = _levels.Black + frac * (_levels.Ceiling - _levels.Black);
            return _levels.Decibel ? $"{Math.Round(v)} dB" : AxisTicks.FormatMagnitude(v);
        }

        private enum VAlign { Top, Center, Bottom }

        private void DrawLabel(DrawingContext dc, string s, double y, VAlign valign, double dpi)
        {
            var ft = new FormattedText(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                _face, 10, _text, dpi);
            double ty = valign == VAlign.Top ? y
                : valign == VAlign.Bottom ? y - ft.Height
                : y - ft.Height / 2;
            dc.DrawText(ft, new Point(BarWidth + 4, ty));
        }

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
