using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace QuickLook.Plugin.AudioViewer.Controls
{
    /// <summary>
    /// Amplitude axis drawn to the left of the waveform, labelled in dBFS.
    ///
    /// dBFS rather than a normalised 0-1 figure because the axis exists to make vertical
    /// magnification legible: at 8x the lane edge is -18 dB, and "the loudest thing here is
    /// -42 dBFS" is directly actionable in a way that "0.0079" is not. It also matches the
    /// rest of the plugin, where the colorbar already labels in dB.
    ///
    /// The axis is linear in amplitude, so dB ticks bunch hard toward the centre line —
    /// -6 dB sits at half the half-lane, -20 at a tenth, and zero amplitude is -inf. The
    /// ladder is therefore taken coarsest-first and de-cluttered the same way
    /// <see cref="FrequencyRuler"/> handles its own non-linear crowding.
    /// </summary>
    public sealed class AmplitudeRuler : FrameworkElement
    {
        private double _gain = 1.0;
        private int _lanes = 1;
        private double _bottomFraction = 1.0;
        private double _scrollOffset;
        private double _laneHeightDip;
        private Brush _text = Brushes.Gray;
        private Brush _line = Brushes.Gray;
        private readonly Typeface _face = new Typeface("Segoe UI");

        public void SetTheme(Color text, Color line)
        {
            _text = Frozen(text);
            _line = Frozen(line);
            InvalidateVisual();
        }

        /// <param name="bottomFraction">
        /// Vertical fraction where the waveform band ends (1 when it fills the pane, 0.2
        /// when stacked above the spectrogram) — the mirror of FrequencyRuler.TopFraction.
        /// </param>
        /// <param name="laneHeightDip">
        /// Height of one channel lane. Needed because a many-channel stack overflows and
        /// scrolls, so lanes are not simply the band divided by the channel count.
        /// </param>
        /// <param name="scrollOffset">WaveScroll's vertical offset, so ticks track the lanes.</param>
        public void Configure(double gain, int lanes, double bottomFraction, double laneHeightDip, double scrollOffset)
        {
            _gain = gain > 0 ? gain : 1.0;
            _lanes = Math.Max(1, lanes);
            _bottomFraction = bottomFraction;
            _laneHeightDip = laneHeightDip;
            _scrollOffset = scrollOffset;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            double band = _bottomFraction * h;
            if (band <= 4) return;

            double laneH = _laneHeightDip > 0 ? _laneHeightDip : band / _lanes;
            var pen = new Pen(_line, 1); pen.Freeze();
            double dpi = PixelsPerDip();

            // Numeric labels only when a single lane is shown. On a stack of 28 DIP lanes
            // the full ladder repeated per lane is unreadable, so those get zero lines only.
            bool label = _lanes == 1;

            for (int lane = 0; lane < _lanes; lane++)
            {
                double laneTop = lane * laneH - _scrollOffset;
                double mid = laneTop + laneH / 2.0;
                if (mid < -laneH || mid > band + laneH) continue;   // scrolled out of view

                // The zero line is always drawn: it is what makes a lane readable at a glance.
                if (mid >= 0 && mid <= band)
                    dc.DrawLine(pen, new Point(w - 4, mid), new Point(w, mid));

                if (!label) continue;

                double half = laneH / 2.0;
                double lastY = double.NegativeInfinity;

                foreach (var db in AxisTicks.NiceDbFs)
                {
                    double frac = AxisTicks.DbToFraction(db, _gain);
                    if (frac > 1.0) continue;                 // magnified past the lane edge

                    // Mirrored above and below the centre line.
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        double y = mid - sign * frac * half;
                        if (y < 0 || y > band) continue;
                        if (Math.Abs(y - lastY) < 13) continue;
                        lastY = y;

                        dc.DrawLine(pen, new Point(w - 4, y), new Point(w, y));
                        var ft = Text(AxisTicks.FormatDbFs(db), dpi);
                        dc.DrawText(ft, new Point(Math.Max(0, w - 6 - ft.Width), y - ft.Height / 2));
                    }
                }
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
