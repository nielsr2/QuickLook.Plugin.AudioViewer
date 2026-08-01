using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace QuickLook.Plugin.AudioViewer.Controls
{
    /// <summary>
    /// Time axis drawn below the spectrogram. The element is exactly as wide as the visible
    /// pane and draws only the currently visible slice of the timeline, offsetting each tick
    /// by the shared scroll position.
    /// </summary>
    /// <remarks>
    /// It used to be as wide as the whole timeline (up to 12000px) and be slid under a
    /// clipping parent with a RenderTransform. That silently truncated the axis: when an
    /// element's unclipped desired size overflows its arrange slot, WPF sets NeedsClipBounds
    /// and installs a VisualClip the size of that slot — expressed in the element's LOCAL
    /// coordinates. A RenderTransform maps local to parent, so the clip travelled with the
    /// translation instead of staying put. Anything past local x ≈ viewport width could
    /// never be shown, and scrolling ate the ruler from the left rather than revealing more.
    /// The tell was that labels always stopped at the same pixel offset regardless of file
    /// length or tick step.
    ///
    /// Drawing only the visible range avoids that entirely, and costs one repaint of ~10-20
    /// labels per scroll event instead of retaining a 12000px-wide visual.
    /// </remarks>
    public sealed class TimeRuler : FrameworkElement
    {
        private double _duration;
        private double _minPxPerLabel = 64;
        private double _timelineWidth;   // full scrollable content width
        private double _scrollOffset;
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

        /// <summary>
        /// The timeline's full laid-out width and the current horizontal scroll offset.
        /// Both come from the shared ScrollViewer, so the ruler cannot disagree with the
        /// content it labels — the previous Width binding could be read before layout had
        /// settled.
        /// </summary>
        public void SetGeometry(double timelineWidth, double scrollOffset)
        {
            _timelineWidth = timelineWidth;
            _scrollOffset = scrollOffset;
            InvalidateVisual();
        }

        // The visible time range is derived from ActualWidth, so a resize must redraw.
        protected override void OnRenderSizeChanged(SizeChangedInfo info)
        {
            base.OnRenderSizeChanged(info);
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth;
            if (w <= 0 || _duration <= 0 || _timelineWidth <= 0) return;

            double pxPerSec = _timelineWidth / _duration;
            if (pxPerSec <= 0) return;

            // The step comes from the FULL timeline width, not the viewport, so it stays put
            // while scrolling instead of re-quantising under the user.
            double step = AxisTicks.TimeStepSeconds(_duration, _timelineWidth, _minPxPerLabel);
            int decimals = AxisTicks.DecimalsForStep(step);
            var pen = new Pen(_line, 1); pen.Freeze();
            double dpi = PixelsPerDip();

            // Widest label the file can produce, used to decide how many ticks to skip
            // between labels. See AxisTicks.VisibleTicks for why the stride is fixed.
            double widest = Text(AxisTicks.FormatTime(_duration, decimals), dpi).Width;
            AxisTicks.VisibleTicks(_duration, _timelineWidth, w, _scrollOffset, step, widest,
                out long first, out long lastIndex, out int stride);

            for (long i = first; i <= lastIndex; i++)
            {
                double t = i * step;
                double x = t * pxPerSec - _scrollOffset;

                dc.DrawLine(pen, new Point(x, 0), new Point(x, 4));
                if (i % stride != 0) continue;

                var ft = Text(AxisTicks.FormatTime(t, decimals), dpi);
                double tx = x + 2;

                // Keep the file's final label on screen when its tick lands on the very edge;
                // every other label stays with its own tick and clips like the content does.
                if (i == lastIndex && tx + ft.Width > w) tx = w - ft.Width;

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
