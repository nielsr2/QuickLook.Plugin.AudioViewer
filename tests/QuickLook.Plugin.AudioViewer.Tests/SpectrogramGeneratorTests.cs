using System.IO;
using NAudio.Wave;
using QuickLook.Plugin.AudioViewer.Audio;
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
    }
}
