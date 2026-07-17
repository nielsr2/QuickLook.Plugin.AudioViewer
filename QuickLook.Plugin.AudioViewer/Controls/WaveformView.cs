using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using QuickLook.Plugin.AudioViewer.Audio;

namespace QuickLook.Plugin.AudioViewer.Controls
{
    public sealed class WaveformView : FrameworkElement
    {
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
            set { _waveform = value; InvalidateVisual(); }
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

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            dc.DrawRectangle(BackgroundBrush, null, new Rect(0, 0, w, h));
            if (_waveform == null || _waveform.Columns == 0 || w <= 0 || h <= 0) return;

            double mid = h / 2.0;
            int cols = _waveform.Columns;
            var pen = new Pen(WaveBrush, Math.Max(1.0, w / cols));
            pen.Freeze();

            for (int i = 0; i < cols; i++)
            {
                double x = (i + 0.5) / cols * w;
                double yTop = mid - _waveform.Max[i] * mid;
                double yBot = mid - _waveform.Min[i] * mid;
                dc.DrawLine(pen, new Point(x, yTop), new Point(x, yBot));
            }

            double px = _playheadFraction * w;
            var ph = new Pen(PlayheadBrush, 1.5);
            ph.Freeze();
            dc.DrawLine(ph, new Point(px, 0), new Point(px, h));
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
