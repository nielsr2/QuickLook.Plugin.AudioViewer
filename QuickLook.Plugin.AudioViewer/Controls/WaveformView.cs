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

        public event Action<double> SeekRequested;

        public Brush WaveBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7));
        public Brush PlayheadBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0xFF, 0x52, 0x52));
        public Brush BackgroundBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));

        public WaveformView()
        {
            WaveBrush.Freeze();
            PlayheadBrush.Freeze();
        }

        public WaveformData Waveform
        {
            get => _waveform;
            set { _waveform = value; InvalidateMeasure(); InvalidateVisual(); }
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
            dc.DrawRectangle(BackgroundBrush, null, new Rect(0, 0, w, h));
            if (_waveform == null || _waveform.Columns == 0 || w <= 0) return;

            int channels = _waveform.ChannelCount;
            int cols = _waveform.Columns;
            var pen = new Pen(WaveBrush, Math.Max(1.0, w / cols));
            pen.Freeze();

            for (int ch = 0; ch < channels; ch++)
            {
                double mid = ch * LaneHeight + LaneHeight / 2.0;
                var min = _waveform.Min[ch];
                var max = _waveform.Max[ch];
                for (int i = 0; i < cols; i++)
                {
                    double x = (i + 0.5) / cols * w;
                    double yTop = mid - max[i] * (LaneHeight / 2.0);
                    double yBot = mid - min[i] * (LaneHeight / 2.0);
                    dc.DrawLine(pen, new Point(x, yTop), new Point(x, yBot));
                }
            }

            double px = _playheadFraction * w;
            var ph = new Pen(PlayheadBrush, 1.5);
            ph.Freeze();
            dc.DrawLine(ph, new Point(px, 0), new Point(px, channels * LaneHeight));
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
