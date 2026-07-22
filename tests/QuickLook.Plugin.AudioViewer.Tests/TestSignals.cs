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

        /// <summary>
        /// Writes an N-channel WAV where each channel plays the same tone at a
        /// different (possibly zero) amplitude — for asserting that per-channel
        /// waveform data tracks channels independently.
        /// </summary>
        public static string WriteMultiToneWav(string dir, int sampleRate, double seconds,
            double[] channelAmplitudes, double freq = 1000)
        {
            var path = Path.Combine(dir, $"multitone_{Guid.NewGuid():N}.wav");
            int channels = channelAmplitudes.Length;
            var format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
            using (var writer = new WaveFileWriter(path, format))
            {
                int frames = (int)(sampleRate * seconds);
                for (int i = 0; i < frames; i++)
                {
                    double phase = 2 * Math.PI * freq * i / sampleRate;
                    for (int c = 0; c < channels; c++)
                        writer.WriteSample((float)(channelAmplitudes[c] * Math.Sin(phase)));
                }
            }
            return path;
        }

        /// <summary>
        /// A minimal ISampleProvider that repeats one fixed per-channel frame forever.
        /// frame.Length is the channel count. Used to unit-test channel-mixing logic
        /// with known values, without any real audio device or file I/O.
        /// </summary>
        public sealed class FixedChannelSampleProvider : NAudio.Wave.ISampleProvider
        {
            private readonly float[] _frame;

            public FixedChannelSampleProvider(int sampleRate, float[] frame)
            {
                _frame = frame;
                WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, frame.Length);
            }

            public WaveFormat WaveFormat { get; }

            public int Read(float[] buffer, int offset, int count)
            {
                int channels = _frame.Length;
                int framesRequested = count / channels;
                int written = 0;
                for (int f = 0; f < framesRequested; f++)
                {
                    for (int c = 0; c < channels; c++)
                        buffer[offset + written++] = _frame[c];
                }
                return written;
            }
        }
    }
}
