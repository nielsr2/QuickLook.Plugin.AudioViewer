using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using QuickLook.Plugin.AudioViewer.Audio;

namespace QuickLook.Plugin.AudioViewer.Controls
{
    /// <summary>Renders one waveform lane per channel, stacked top to bottom.</summary>
    public sealed class WaveformView : FrameworkElement
    {
        /// <summary>Fixed height per channel lane, in device-independent pixels.</summary>
        public const double LaneHeight = 28.0;

        private WaveformData _waveform;
        private double _playheadFraction;
        private bool _dragging;

        // Cache of the (expensive) per-lane line segments, so a playhead-only invalidation
        // (which fires every timer tick, ~30 Hz) doesn't rebuild all channels' waveforms —
        // only rebuilt when the waveform instance or the rendered size changes.
        private DrawingGroup _laneCache;
        private WaveformData _laneCacheWaveform;
        private double _laneCacheWidth;
        private double _laneCacheHeight;

        public event Action<double> SeekRequested;

        public Brush WaveBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7));
        public Brush PlayheadBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0xFF, 0x52, 0x52));
        public Brush BackgroundBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));

        // Subtle 1px divider drawn between adjacent channel lanes so multichannel stacks
        // read as distinct lanes rather than one blurred band; a shade lighter than the
        // background but well below the wave color so it doesn't compete for attention.
        public Brush LaneSeparatorBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40));
        private readonly Pen _separatorPen;

        public WaveformView()
        {
            WaveBrush.Freeze();
            PlayheadBrush.Freeze();
            LaneSeparatorBrush.Freeze();
            _separatorPen = new Pen(LaneSeparatorBrush, 1.0);
            _separatorPen.Freeze();
        }

        public WaveformData Waveform
        {
            get => _waveform;
            set { _waveform = value; _laneCache = null; InvalidateMeasure(); InvalidateVisual(); }
        }

        public double PlayheadFraction
        {
            get => _playheadFraction;
            set { _playheadFraction = Clamp01(value); InvalidateVisual(); }
        }

        public static double FractionFromX(double x, double width)
        {
            if (width <= 0) return 0;
            return Clamp01(x / width);
        }

        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;

        // Reports one LaneHeight per channel as the desired height, so a wrapping
        // vertical ScrollViewer knows when to show a scrollbar (many-channel files)
        // and when not to (mono/stereo files fit without scrolling).
        protected override Size MeasureOverride(Size availableSize)
        {
            int channels = Math.Max(1, _waveform?.ChannelCount ?? 1);
            double width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
            return new Size(width, channels * LaneHeight);
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;

            if (_waveform == null || _waveform.Columns == 0 || w <= 0)
            {
                dc.DrawRectangle(BackgroundBrush, null, new Rect(0, 0, w, h));
                return;
            }

            if (_laneCache == null || !ReferenceEquals(_laneCacheWaveform, _waveform)
                || _laneCacheWidth != w || _laneCacheHeight != h)
            {
                _laneCache = BuildLaneDrawing(w, h);
                _laneCacheWaveform = _waveform;
                _laneCacheWidth = w;
                _laneCacheHeight = h;
            }

            dc.DrawDrawing(_laneCache);

            double px = _playheadFraction * w;
            var ph = new Pen(PlayheadBrush, 1.5);
            ph.Freeze();
            dc.DrawLine(ph, new Point(px, 0), new Point(px, h));
        }

        /// <summary>Builds the (expensive, per-column) background + lane waveforms into a
        /// cacheable <see cref="DrawingGroup"/>, so repeated renders (e.g. the playhead
        /// moving every timer tick) only redraw the cheap playhead line on top of it.</summary>
        private DrawingGroup BuildLaneDrawing(double w, double h)
        {
            var group = new DrawingGroup();
            using (var dc = group.Open())
            {
                dc.DrawRectangle(BackgroundBrush, null, new Rect(0, 0, w, h));

                int channels = _waveform.ChannelCount;
                int cols = _waveform.Columns;
                double laneHeight = Math.Max(LaneHeight, h / channels);
                var pen = new Pen(WaveBrush, Math.Max(1.0, w / cols));
                pen.Freeze();

                for (int ch = 0; ch < channels; ch++)
                {
                    double mid = ch * laneHeight + laneHeight / 2.0;
                    var min = _waveform.Min[ch];
                    var max = _waveform.Max[ch];
                    for (int i = 0; i < cols; i++)
                    {
                        double x = (i + 0.5) / cols * w;
                        double yTop = mid - max[i] * (laneHeight / 2.0);
                        double yBot = mid - min[i] * (laneHeight / 2.0);
                        dc.DrawLine(pen, new Point(x, yTop), new Point(x, yBot));
                    }
                }

                // Boundary lines between lanes only (channels - 1 of them) — never above
                // the first lane or below the last.
                if (channels > 1)
                {
                    for (int ch = 1; ch < channels; ch++)
                    {
                        double y = ch * laneHeight;
                        dc.DrawLine(_separatorPen, new Point(0, y), new Point(w, y));
                    }
                }
            }
            if (group.CanFreeze) group.Freeze();
            return group;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            _dragging = true;
            CaptureMouse();
            RaiseSeek(e.GetPosition(this).X);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging) RaiseSeek(e.GetPosition(this).X);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            _dragging = false;
            ReleaseMouseCapture();
        }

        private void RaiseSeek(double x) => SeekRequested?.Invoke(FractionFromX(x, ActualWidth));
    }
}
