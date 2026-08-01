using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// WaveformRasterizer is internal by design (an implementation detail of the
// waveform view); the test assembly needs access to unit-test RenderPixels
// at the pixel level.
[assembly: InternalsVisibleTo("QuickLook.Plugin.AudioViewer.Tests")]

namespace QuickLook.Plugin.AudioViewer.Audio
{
    /// <summary>
    /// Pure, bitmap-based waveform rasterizer. Mirrors the lane math used by
    /// <see cref="Controls.WaveformView"/>'s vector (DrawingGroup) rendering, but
    /// produces a plain ARGB pixel buffer instead — useful for fast, cacheable
    /// snapshots (e.g. very tall multichannel views) where per-frame vector
    /// re-rendering would be too expensive.
    /// </summary>
    internal static class WaveformRasterizer
    {
        /// <summary>
        /// Renders <paramref name="wf"/> to an ARGB32 pixel buffer, one lane of
        /// <paramref name="lanePixelHeight"/> rows per channel, stacked top to bottom.
        /// Width equals <see cref="WaveformData.Columns"/>; height equals
        /// <see cref="WaveformData.ChannelCount"/> * <paramref name="lanePixelHeight"/>.
        /// </summary>
        /// <param name="gain">
        /// Vertical magnification. Applied here rather than to <see cref="WaveformData"/>
        /// so the stored envelope stays a true record of the file's sample peaks — the
        /// amplitude ruler and the auto-fit calculation both need those untouched, and a
        /// gain change costs one re-raster rather than a re-scan.
        /// </param>
        /// <param name="clipArgb">
        /// Colour for the single row marking a column whose envelope was cut off by the
        /// lane edge. Null (the default) paints those rows in <paramref name="waveArgb"/>
        /// like any other, i.e. no clip indication.
        /// </param>
        internal static int[] RenderPixels(WaveformData wf, int lanePixelHeight, int waveArgb, int backgroundArgb,
            double gain = 1.0, int? clipArgb = null)
        {
            if (wf == null) throw new ArgumentNullException(nameof(wf));
            if (lanePixelHeight < 1) throw new ArgumentOutOfRangeException(nameof(lanePixelHeight));
            if (double.IsNaN(gain) || double.IsInfinity(gain) || gain <= 0) gain = 1.0;

            int width = wf.Columns;
            int channels = wf.ChannelCount;
            int height = channels * lanePixelHeight;

            var pixels = new int[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = backgroundArgb;

            // Fold the gain into the half-lane height once per call: the inner loop is
            // unchanged, so magnification costs nothing per pixel.
            double half = lanePixelHeight / 2.0 * gain;

            for (int c = 0; c < channels; c++)
            {
                int laneTop = c * lanePixelHeight;
                int laneBottom = laneTop + lanePixelHeight - 1;
                double mid = laneTop + lanePixelHeight / 2.0;

                var min = wf.Min[c];
                var max = wf.Max[c];

                for (int x = 0; x < width; x++)
                {
                    double yTopD = mid - max[x] * half;
                    double yBotD = mid - min[x] * half;

                    int yTop = (int)Math.Round(yTopD, MidpointRounding.AwayFromZero);
                    int yBot = (int)Math.Round(yBotD, MidpointRounding.AwayFromZero);

                    // Note before clamping: at high gain the envelope runs past the lane,
                    // and a flat-topped waveform otherwise reads as a limited signal rather
                    // than as content scrolled out of view.
                    bool clipped = yTop < laneTop || yBot > laneBottom;

                    if (yTop < laneTop) yTop = laneTop;
                    if (yTop > laneBottom) yTop = laneBottom;
                    if (yBot < laneTop) yBot = laneTop;
                    if (yBot > laneBottom) yBot = laneBottom;

                    // Min <= Max always, so yTopD <= yBotD; guard against any rounding
                    // flip and guarantee at least a 1px span.
                    if (yBot < yTop) yBot = yTop;

                    for (int y = yTop; y <= yBot; y++)
                        pixels[y * width + x] = waveArgb;

                    if (clipped && clipArgb.HasValue)
                    {
                        pixels[laneTop * width + x] = clipArgb.Value;
                        pixels[laneBottom * width + x] = clipArgb.Value;
                    }
                }
            }

            return pixels;
        }

        /// <summary>
        /// Wraps a pre-rendered ARGB32 pixel buffer into a WPF bitmap. Trivial by
        /// design — the interesting, testable logic lives in <see cref="RenderPixels"/>.
        /// Caller is responsible for freezing the result if it will cross threads.
        /// </summary>
        internal static WriteableBitmap ToBitmap(int[] pixels, int width, int height)
        {
            var bmp = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            bmp.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
            return bmp;
        }
    }
}
