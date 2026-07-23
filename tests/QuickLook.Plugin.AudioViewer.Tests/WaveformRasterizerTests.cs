using System.IO;
using QuickLook.Plugin.AudioViewer.Audio;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    public class WaveformRasterizerTests
    {
        private const int WaveArgb = unchecked((int)0xFF4FC3F7);
        private const int BackgroundArgb = unchecked((int)0xFF1E1E1E);

        private static WaveformData ComputeFrom(string wav, int columns)
        {
            using (var stream = AudioReaderFactory.Create(wav))
                return WaveformData.Compute(stream, columns);
        }

        [Fact]
        public void RenderPixels_DimensionsMatchColumnsAndChannels()
        {
            var wav = TestSignals.WriteSilenceWav(Path.GetTempPath(), seconds: 0.5, channels: 2);
            try
            {
                var wf = ComputeFrom(wav, columns: 64);
                var pixels = WaveformRasterizer.RenderPixels(wf, lanePixelHeight: 20, WaveArgb, BackgroundArgb);

                Assert.Equal(64 * (2 * 20), pixels.Length);
            }
            finally { File.Delete(wav); }
        }

        [Fact]
        public void RenderPixels_Silence_OnlyMidlineRowIsWaveColored()
        {
            var wav = TestSignals.WriteSilenceWav(Path.GetTempPath(), seconds: 0.5, channels: 1);
            try
            {
                var wf = ComputeFrom(wav, columns: 32);
                const int laneHeight = 20;
                var pixels = WaveformRasterizer.RenderPixels(wf, laneHeight, WaveArgb, BackgroundArgb);

                int width = wf.Columns;
                int midRow = laneHeight / 2; // 10

                for (int y = 0; y < laneHeight; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int argb = pixels[y * width + x];
                        if (y == midRow)
                            Assert.Equal(WaveArgb, argb);
                        else
                            Assert.Equal(BackgroundArgb, argb);
                    }
                }
            }
            finally { File.Delete(wav); }
        }

        [Fact]
        public void RenderPixels_FullScaleSine_FillsWholeLaneColumn()
        {
            var wav = TestSignals.WriteSineWav(Path.GetTempPath(), sampleRate: 44100, seconds: 1.0,
                freq: 1000, amplitude: 1.0, channels: 1);
            try
            {
                var wf = ComputeFrom(wav, columns: 50);
                const int laneHeight = 20;
                var pixels = WaveformRasterizer.RenderPixels(wf, laneHeight, WaveArgb, BackgroundArgb);

                int width = wf.Columns;
                int x = width / 2; // a well-covered interior column

                // Full-scale tone: top and bottom rows of the lane are wave-colored,
                // i.e. the column spans (very nearly) the whole lane height.
                Assert.Equal(WaveArgb, pixels[0 * width + x]);
                Assert.Equal(WaveArgb, pixels[(laneHeight - 1) * width + x]);
            }
            finally { File.Delete(wav); }
        }

        [Fact]
        public void RenderPixels_HalfScaleSine_SpansRoughlyHalfTheLane()
        {
            var wav = TestSignals.WriteSineWav(Path.GetTempPath(), sampleRate: 44100, seconds: 1.0,
                freq: 1000, amplitude: 0.5, channels: 1);
            try
            {
                var wf = ComputeFrom(wav, columns: 50);
                const int laneHeight = 20;
                var pixels = WaveformRasterizer.RenderPixels(wf, laneHeight, WaveArgb, BackgroundArgb);

                int width = wf.Columns;
                int x = width / 2;

                int coloredRows = 0;
                for (int y = 0; y < laneHeight; y++)
                    if (pixels[y * width + x] == WaveArgb) coloredRows++;

                // ~half of the 20-row lane (with tolerance for discrete sampling/rounding).
                Assert.InRange(coloredRows, 8, 12);
            }
            finally { File.Delete(wav); }
        }

        [Fact]
        public void RenderPixels_BeyondUnityAmplitude_ClampsWithinLane_NoBleedIntoNeighbor()
        {
            // Channel 0: amplitude 1.5 (out-of-range peaks). Channel 1: silence.
            var wav = TestSignals.WriteMultiToneWav(Path.GetTempPath(), 44100, 1.0,
                new[] { 1.5, 0.0 });
            try
            {
                var wf = ComputeFrom(wav, columns: 50);
                Assert.Equal(2, wf.ChannelCount);

                const int laneHeight = 20;
                var pixels = WaveformRasterizer.RenderPixels(wf, laneHeight, WaveArgb, BackgroundArgb);

                int width = wf.Columns;
                int x = width / 2;

                // Channel 0's out-of-range peaks clamp to the full lane (rows 0..19),
                // never spilling past row 19 into channel 1's lane.
                Assert.Equal(WaveArgb, pixels[0 * width + x]);
                Assert.Equal(WaveArgb, pixels[(laneHeight - 1) * width + x]);

                // Channel 1 (silence) lane starts at row 20; only its own midline
                // (row 30) is wave-colored, and row 20 (its top, adjacent to channel
                // 0's clamped bottom) is background — proving no bleed-over.
                int lane1Top = laneHeight;
                int lane1Mid = lane1Top + laneHeight / 2;
                for (int y = lane1Top; y < lane1Top + laneHeight; y++)
                {
                    int argb = pixels[y * width + x];
                    if (y == lane1Mid)
                        Assert.Equal(WaveArgb, argb);
                    else
                        Assert.Equal(BackgroundArgb, argb);
                }
            }
            finally { File.Delete(wav); }
        }

        [Fact]
        public void RenderPixels_MultiChannel_EachChannelAppearsOnlyAtItsRowOffset()
        {
            // Channel 0: full-scale. Channel 1: silence. Channel 2: full-scale.
            var wav = TestSignals.WriteMultiToneWav(Path.GetTempPath(), 44100, 1.0,
                new[] { 1.0, 0.0, 1.0 });
            try
            {
                var wf = ComputeFrom(wav, columns: 50);
                Assert.Equal(3, wf.ChannelCount);

                const int laneHeight = 20;
                var pixels = WaveformRasterizer.RenderPixels(wf, laneHeight, WaveArgb, BackgroundArgb);

                int width = wf.Columns;
                int x = width / 2;
                int height = 3 * laneHeight;
                Assert.Equal(width * height, pixels.Length);

                // Lane 0 (rows 0..19): full-scale tone fills top and bottom rows.
                Assert.Equal(WaveArgb, pixels[0 * width + x]);
                Assert.Equal(WaveArgb, pixels[19 * width + x]);

                // Lane 1 (rows 20..39): silent channel — only its midline (row 30).
                for (int y = 20; y < 40; y++)
                {
                    int argb = pixels[y * width + x];
                    if (y == 30)
                        Assert.Equal(WaveArgb, argb);
                    else
                        Assert.Equal(BackgroundArgb, argb);
                }

                // Lane 2 (rows 40..59): full-scale tone fills top and bottom rows.
                Assert.Equal(WaveArgb, pixels[40 * width + x]);
                Assert.Equal(WaveArgb, pixels[59 * width + x]);
            }
            finally { File.Delete(wav); }
        }

        [Fact]
        public void RenderPixels_UsesExactWaveAndBackgroundArgbConstants()
        {
            var wav = TestSignals.WriteSilenceWav(Path.GetTempPath(), seconds: 0.2, channels: 1);
            try
            {
                var wf = ComputeFrom(wav, columns: 10);
                var pixels = WaveformRasterizer.RenderPixels(wf, lanePixelHeight: 8, WaveArgb, BackgroundArgb);

                bool sawWave = false, sawBackground = false;
                foreach (var argb in pixels)
                {
                    Assert.True(argb == WaveArgb || argb == BackgroundArgb);
                    if (argb == WaveArgb) sawWave = true;
                    if (argb == BackgroundArgb) sawBackground = true;
                }
                Assert.True(sawWave);
                Assert.True(sawBackground);
            }
            finally { File.Delete(wav); }
        }
    }
}
