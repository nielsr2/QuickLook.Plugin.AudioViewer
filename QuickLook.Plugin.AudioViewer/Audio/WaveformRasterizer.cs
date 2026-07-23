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
        internal static int[] RenderPixels(WaveformData wf, int lanePixelHeight, int waveArgb, int backgroundArgb)
        {
            if (wf == null) throw new ArgumentNullException(nameof(wf));
            if (lanePixelHeight < 1) throw new ArgumentOutOfRangeException(nameof(lanePixelHeight));

            int width = wf.Columns;
            int channels = wf.ChannelCount;
            int height = channels * lanePixelHeight;

            var pixels = new int[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = backgroundArgb;

            double half = lanePixelHeight / 2.0;

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

                    if (yTop < laneTop) yTop = laneTop;
                    if (yTop > laneBottom) yTop = laneBottom;
                    if (yBot < laneTop) yBot = laneTop;
                    if (yBot > laneBottom) yBot = laneBottom;

                    // Min <= Max always, so yTopD <= yBotD; guard against any rounding
                    // flip and guarantee at least a 1px span.
                    if (yBot < yTop) yBot = yTop;

                    for (int y = yTop; y <= yBot; y++)
                        pixels[y * width + x] = waveArgb;
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
