using System.IO;
using NAudio.Wave;
using QuickLook.Plugin.AudioViewer.Audio;
using QuickLook.Plugin.AudioViewer.Settings;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    public class SpectrogramGeneratorTests
    {
        [Fact]
        public void ComputeMagnitudes_Sine1kHz_EnergyInExpectedBin()
        {
            int sampleRate = 44100, fftSize = 1024;
            double freq = 1000;
            var wav = TestSignals.WriteSineWav(Path.GetTempPath(), sampleRate, seconds: 1.0, freq: freq, channels: 1);
            try
            {
                using (var stream = AudioReaderFactory.Create(wav))
                {
                    var mags = SpectrogramGenerator.ComputeMagnitudes(
                        stream.ToSampleProvider(), fftSize: fftSize, hop: fftSize / 2, maxFrames: 100, out _);

                    Assert.NotEmpty(mags);
                    // Argmax bin of a middle frame should map to ~1kHz.
                    var frame = mags[mags.Length / 2];
                    int argmax = 0;
                    for (int b = 1; b < frame.Length; b++)
                        if (frame[b] > frame[argmax]) argmax = b;

                    double binHz = (double)sampleRate / fftSize;   // ~43.07 Hz per bin
                    double peakHz = argmax * binHz;
                    Assert.InRange(peakHz, freq - binHz * 1.5, freq + binHz * 1.5);
                }
            }
            finally { File.Delete(wav); }
        }

        [Theory]
        [InlineData(FrequencyScale.Linear)]
        [InlineData(FrequencyScale.Mel)]
        [InlineData(FrequencyScale.Log)]
        public void RowBins_PlacePeakBinAtExpectedFrequency(FrequencyScale scale)
        {
            int sampleRate = 44100, fftSize = 1024, rowCount = 256;
            double freq = 1000;
            var wav = TestSignals.WriteSineWav(Path.GetTempPath(), sampleRate, seconds: 1.0, freq: freq, channels: 1);
            try
            {
                using (var stream = AudioReaderFactory.Create(wav))
                {
                    var mags = SpectrogramGenerator.ComputeMagnitudes(
                        stream.ToSampleProvider(), fftSize, fftSize / 2, 100, out _);
                    var frame = mags[mags.Length / 2];
                    int peakBin = 0;
                    for (int b = 1; b < frame.Length; b++)
                        if (frame[b] > frame[peakBin]) peakBin = b;

                    var rows = FrequencyScaleMapper.BuildRowBins(rowCount, scale, fftSize, sampleRate, 0);

                    // Some row containing the peak bin should sit near 1 kHz under this scale.
                    bool found = false;
                    double fMin = FrequencyScaleMapper.EffectiveFMin(scale, fftSize, sampleRate);
                    for (int r = 0; r < rowCount; r++)
                    {
                        if (peakBin >= rows[r].Lo && peakBin <= rows[r].Hi)
                        {
                            double center = FrequencyScaleMapper.RowCenterFrequency(r, rowCount, scale, fMin, sampleRate / 2.0);
                            if (center >= freq - 150 && center <= freq + 150) { found = true; break; }
                        }
                    }
                    Assert.True(found, $"no {scale} row containing peak bin {peakBin} maps near {freq} Hz");
                }
            }
            finally { File.Delete(wav); }
        }
    }
}
