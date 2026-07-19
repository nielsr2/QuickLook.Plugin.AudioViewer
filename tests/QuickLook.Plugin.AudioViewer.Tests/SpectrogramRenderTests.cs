using System.IO;
using NAudio.Wave;
using QuickLook.Plugin.AudioViewer.Audio;
using QuickLook.Plugin.AudioViewer.Settings;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    /// <summary>
    /// Covers the pure pixel path extracted from <c>SpectrogramGenerator.Render</c>
    /// (previously "verified manually"), including the too-short-file empty-frames case.
    /// </summary>
    public class SpectrogramRenderTests
    {
        private static float[][] SineFrames(out int sampleRate, int fftSize = 1024)
        {
            var wav = TestSignals.WriteSineWav(Path.GetTempPath(), 44100, 1.0, 1000, 1.0, 1);
            try
            {
                using (var stream = AudioReaderFactory.Create(wav))
                {
                    sampleRate = stream.WaveFormat.SampleRate;
                    return SpectrogramGenerator.ComputeMagnitudes(
                        stream.ToSampleProvider(), fftSize, fftSize / 2, 200, out _);
                }
            }
            finally { File.Delete(wav); }
        }

        [Fact]
        public void RenderToPixels_EmptyFrames_IsUniformColormapBlack()
        {
            var opts = new SpectrogramRenderOptions
            {
                PixelWidth = 24,
                PixelHeight = 16,
                Colormap = SpectrogramColormap.Magma,
                AmplitudeScale = AmplitudeScale.Decibel
            };
            var px = SpectrogramGenerator.RenderToPixels(new float[0][], opts, out int pw, out int ph);

            Assert.Equal(24, pw);
            Assert.Equal(16, ph);
            Assert.Equal(pw * ph * 4, px.Length);

            // Empty frames => normalized value 0 everywhere => the colormap's t=0 color.
            Colormaps.Sample(SpectrogramColormap.Magma, 0.0, out byte r, out byte g, out byte b);
            for (int i = 0; i < pw * ph; i++)
            {
                Assert.Equal(b, px[i * 4 + 0]); // BGRA order
                Assert.Equal(g, px[i * 4 + 1]);
                Assert.Equal(r, px[i * 4 + 2]);
                Assert.Equal(255, px[i * 4 + 3]);
            }
        }

        [Fact]
        public void RenderToPixels_Sine_IsNonUniformAndOpaque()
        {
            var frames = SineFrames(out int sr);
            var opts = new SpectrogramRenderOptions
            {
                SampleRate = sr,
                FftSize = 1024,
                PixelWidth = 100,
                PixelHeight = 128,
                FrequencyScale = FrequencyScale.Linear,
                Colormap = SpectrogramColormap.Magma
            };
            var px = SpectrogramGenerator.RenderToPixels(frames, opts, out int pw, out int ph);

            byte first = px[2];
            bool varied = false;
            for (int i = 0; i < pw * ph; i++)
            {
                Assert.Equal(255, px[i * 4 + 3]); // fully opaque
                if (px[i * 4 + 2] != first) varied = true;
            }
            Assert.True(varied, "a 1 kHz tone should not render as a flat color");
        }

        [Theory]
        [InlineData(SpectrogramColormap.Magma, AmplitudeScale.Decibel, -120)]
        [InlineData(SpectrogramColormap.Viridis, AmplitudeScale.Decibel, -40)]
        [InlineData(SpectrogramColormap.Grayscale, AmplitudeScale.Linear, -90)]
        public void RenderToPixels_VariousOptions_ProduceFullOpaqueBuffer(
            SpectrogramColormap map, AmplitudeScale amp, int dbFloor)
        {
            var frames = SineFrames(out int sr);
            var opts = new SpectrogramRenderOptions
            {
                SampleRate = sr,
                FftSize = 1024,
                PixelWidth = 80,
                PixelHeight = 96,
                Colormap = map,
                AmplitudeScale = amp,
                DbFloor = dbFloor
            };
            var px = SpectrogramGenerator.RenderToPixels(frames, opts, out int pw, out int ph);

            Assert.Equal(pw * ph * 4, px.Length);
            for (int i = 0; i < pw * ph; i++)
                Assert.Equal(255, px[i * 4 + 3]);
        }

        [Fact]
        public void ComputeMagnitudes_FileShorterThanFftWindow_ReturnsEmpty()
        {
            // ~0.004 s at 44.1 kHz ≈ 176 samples, far below an fftSize of 1024.
            var wav = TestSignals.WriteSineWav(Path.GetTempPath(), 44100, 0.004, 1000, 1.0, 1);
            try
            {
                using (var stream = AudioReaderFactory.Create(wav))
                {
                    var mags = SpectrogramGenerator.ComputeMagnitudes(
                        stream.ToSampleProvider(), 1024, 512, 100, out _);
                    Assert.Empty(mags);
                }
            }
            finally { File.Delete(wav); }
        }
    }
}
