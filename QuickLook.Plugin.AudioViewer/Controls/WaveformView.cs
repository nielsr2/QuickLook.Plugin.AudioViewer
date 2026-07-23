using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

        // The lane waveforms are rasterized off-thread into a single ARGB bitmap
        // (1px per column, channels*lanePixelHeight tall) and blitted, replacing the
        // former per-column DrawLine vector cache. A playhead-only invalidation
        // (every timer tick, ~30 Hz) then just re-blits the cached bitmap plus the
        // cheap playhead/separator vectors — no waveform re-rasterization.
        private ImageSource _waveBitmap;

        // Rebuild key: the bitmap is valid only for the WaveformData reference AND the
        // integer lanePixelHeight it was rasterized at (width is always Columns and is
        // stretched horizontally, so it is deliberately NOT part of the key).
        //  - "completed" key: what the currently-held _waveBitmap was actually built for.
        //  - "requested" key: what the most recently spawned rebuild is building for,
        //    used to avoid spawning a duplicate rebuild every OnRender while one is
        //    already in flight for the same key.
        private WaveformData _bitmapWaveform;      // completed key (waveform ref)
        private int _bitmapLanePx = -1;            // completed key (lanePixelHeight)
        private WaveformData _requestedWaveform;   // last-requested key (waveform ref)
        private int _requestedLanePx = -1;         // last-requested key (lanePixelHeight)
        private int _bitmapGeneration;             // staleness guard for async completions

        public event Action<double> SeekRequested;

        public Brush WaveBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7));
        public Brush PlayheadBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0xFF, 0x52, 0x52));
        public Brush BackgroundBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));

        // Subtle 1px divider drawn between adjacent channel lanes so multichannel stacks
        // read as distinct lanes rather than one blurred band; a shade lighter than the
        // background but well below the wave color so it doesn't compete for attention.
        public Brush LaneSeparatorBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40));
        private readonly Pen _separatorPen;
        private readonly Pen _playheadPen;

        public WaveformView()
        {
            WaveBrush.Freeze();
            PlayheadBrush.Freeze();
            LaneSeparatorBrush.Freeze();
            _separatorPen = new Pen(LaneSeparatorBrush, 1.0);
            _separatorPen.Freeze();
            // Built once (not per-render): the playhead invalidates ~30 Hz, so re-allocating
            // and freezing a Pen every tick was pure churn.
            _playheadPen = new Pen(PlayheadBrush, 1.5);
            _playheadPen.Freeze();

            // The wave bitmap is intentionally 1px-per-column and stretched horizontally to
            // fill; NearestNeighbor keeps the crisp per-column look of the old DrawLine
            // rendering and avoids interpolation blur.
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        }

        public WaveformData Waveform
        {
            get => _waveform;
            set
            {
                _waveform = value;
                // Force a rebuild: drop the cached bitmap and both keys, and bump the
                // generation so any in-flight rebuild for the old waveform is discarded.
                _waveBitmap = null;
                _bitmapWaveform = null;
                _bitmapLanePx = -1;
                _requestedWaveform = null;
                _requestedLanePx = -1;
                _bitmapGeneration++;
                InvalidateMeasure();
                InvalidateVisual();
            }
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

        private static int ToArgb(Brush brush)
        {
            var c = ((SolidColorBrush)brush).Color;
            return (c.A << 24) | (c.R << 16) | (c.G << 8) | c.B;
        }

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

            int channels = _waveform.ChannelCount;
            // Mono/stereo stretch to fill; >=3ch pinned at LaneHeight (DIP).
            double laneHeightDIP = Math.Max(LaneHeight, h / channels);
            double dpiY = VisualTreeHelper.GetDpi(this).DpiScaleY;
            int lanePixelHeight = Math.Max(1, (int)Math.Round(laneHeightDIP * dpiY));

            // Rebuild off-thread if stale: the bitmap must match the current waveform ref
            // AND lanePixelHeight. Guard on the last-requested key so we don't spawn a
            // duplicate rebuild every OnRender while one is already in flight for this key.
            if (!ReferenceEquals(_requestedWaveform, _waveform) || _requestedLanePx != lanePixelHeight)
            {
                _requestedWaveform = _waveform;
                _requestedLanePx = lanePixelHeight;

                int gen = ++_bitmapGeneration;
                var wf = _waveform;
                int lph = lanePixelHeight;
                int waveArgb = ToArgb(WaveBrush);
                int bgArgb = ToArgb(BackgroundBrush);

                Task.Run(() =>
                {
                    var px = WaveformRasterizer.RenderPixels(wf, lph, waveArgb, bgArgb);
                    var bmp = WaveformRasterizer.ToBitmap(px, wf.Columns, wf.ChannelCount * lph);
                    bmp.Freeze();
                    Dispatcher.BeginInvoke((Action)(() =>
                    {
                        if (gen != _bitmapGeneration) return; // a newer rebuild superseded this one
                        _waveBitmap = bmp;
                        _bitmapWaveform = wf;   // record the key this bitmap was built for
                        _bitmapLanePx = lph;
                        InvalidateVisual();
                    }));
                });
            }

            if (_waveBitmap != null)
                // Stretches the Columns x (channels*lanePixelHeight) bitmap to fill the control.
                dc.DrawImage(_waveBitmap, new Rect(0, 0, w, h));
            else
                dc.DrawRectangle(BackgroundBrush, null, new Rect(0, 0, w, h));

            // Lane separators drawn as crisp vectors on top (kept OUT of the bitmap so they
            // stay 1px regardless of the horizontal stretch), between adjacent lanes only.
            if (channels > 1)
            {
                for (int ch = 1; ch < channels; ch++)
                {
                    double y = ch * laneHeightDIP;
                    dc.DrawLine(_separatorPen, new Point(0, y), new Point(w, y));
                }
            }

            double px2 = _playheadFraction * w;
            dc.DrawLine(_playheadPen, new Point(px2, 0), new Point(px2, h));
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
