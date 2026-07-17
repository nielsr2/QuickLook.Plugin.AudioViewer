using System;
using System.IO;
using NAudio.Wave;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    public static class TestSignals
    {
        public static string WriteSineWav(string dir, int sampleRate = 44100, double seconds = 1.0,
            double freq = 1000, double amplitude = 1.0, int channels = 1)
        {
            var path = Path.Combine(dir, $"sine_{Guid.NewGuid():N}.wav");
            var format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
            using (var writer = new WaveFileWriter(path, format))
            {
                int frames = (int)(sampleRate * seconds);
                for (int i = 0; i < frames; i++)
                {
                    float sample = (float)(amplitude * Math.Sin(2 * Math.PI * freq * i / sampleRate));
                    for (int c = 0; c < channels; c++)
                        writer.WriteSample(sample);
                }
            }
            return path;
        }

        public static string WriteSilenceWav(string dir, int sampleRate = 44100, double seconds = 1.0, int channels = 1)
        {
            var path = Path.Combine(dir, $"silence_{Guid.NewGuid():N}.wav");
            var format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
            using (var writer = new WaveFileWriter(path, format))
            {
                int frames = (int)(sampleRate * seconds);
                for (int i = 0; i < frames * channels; i++)
                    writer.WriteSample(0f);
            }
            return path;
        }
    }
}
