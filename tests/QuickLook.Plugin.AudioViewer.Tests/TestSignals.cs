using System;
using System.IO;
using NAudio.Dmo;
using NAudio.Wave;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    public static class TestSignals
    {
        private static short ToInt16(double sample)
        {
            double scaled = Math.Round(sample * short.MaxValue);
            if (scaled > short.MaxValue) scaled = short.MaxValue;
            if (scaled < short.MinValue) scaled = short.MinValue;
            return (short)scaled;
        }

        private static WaveFormat Pcm16Format(int sampleRate, int channels, bool extensible)
        {
            return extensible
                ? (WaveFormat)new WaveFormatExtensible(sampleRate, 16, channels)
                : new WaveFormat(sampleRate, 16, channels);
        }

        /// <summary>
        /// 16-bit PCM sine wave (optionally WAVE_FORMAT_EXTENSIBLE, PCM sub-format —
        /// the layout real multichannel audio interfaces typically use).
        /// </summary>
        public static string WriteSineWavPcm16(string dir, int sampleRate = 44100, double seconds = 1.0,
            double freq = 1000, double amplitude = 1.0, int channels = 1, bool extensible = false)
        {
            var path = Path.Combine(dir, $"sine16_{Guid.NewGuid():N}.wav");
            var format = Pcm16Format(sampleRate, channels, extensible);
            using (var writer = new WaveFileWriter(path, format))
            {
                int frames = (int)(sampleRate * seconds);
                var frameBuf = new short[channels];
                for (int i = 0; i < frames; i++)
                {
                    short sample = ToInt16(amplitude * Math.Sin(2 * Math.PI * freq * i / sampleRate));
                    for (int c = 0; c < channels; c++) frameBuf[c] = sample;
                    writer.WriteSamples(frameBuf, 0, channels);
                }
            }
            return path;
        }

        /// <summary>16-bit PCM silence (optionally WAVE_FORMAT_EXTENSIBLE, PCM sub-format).</summary>
        public static string WriteSilenceWavPcm16(string dir, int sampleRate = 44100, double seconds = 1.0,
            int channels = 1, bool extensible = false)
        {
            var path = Path.Combine(dir, $"silence16_{Guid.NewGuid():N}.wav");
            var format = Pcm16Format(sampleRate, channels, extensible);
            using (var writer = new WaveFileWriter(path, format))
            {
                int frames = (int)(sampleRate * seconds);
                var frameBuf = new short[channels];
                for (int i = 0; i < frames; i++)
                    writer.WriteSamples(frameBuf, 0, channels);
            }
            return path;
        }

        /// <summary>
        /// 16-bit PCM per-channel-independent tone (optionally WAVE_FORMAT_EXTENSIBLE,
        /// PCM sub-format) — the PCM16 counterpart of <see cref="WriteMultiToneWav"/>.
        /// </summary>
        public static string WriteMultiToneWavPcm16(string dir, int sampleRate, double seconds,
            double[] channelAmplitudes, double freq = 1000, bool extensible = false)
        {
            var path = Path.Combine(dir, $"multitone16_{Guid.NewGuid():N}.wav");
            int channels = channelAmplitudes.Length;
            var format = Pcm16Format(sampleRate, channels, extensible);
            using (var writer = new WaveFileWriter(path, format))
            {
                int frames = (int)(sampleRate * seconds);
                var frameBuf = new short[channels];
                for (int i = 0; i < frames; i++)
                {
                    double phase = 2 * Math.PI * freq * i / sampleRate;
                    for (int c = 0; c < channels; c++)
                        frameBuf[c] = ToInt16(channelAmplitudes[c] * Math.Sin(phase));
                    writer.WriteSamples(frameBuf, 0, channels);
                }
            }
            return path;
        }

        /// <summary>
        /// 16-bit PCM WAV of <paramref name="totalFrames"/> frames that is silent except
        /// for a single full-scale frame (all channels) at <paramref name="impulseFrameIndex"/>.
        /// Used to assert exactly which output column a known sample position lands in.
        /// </summary>
        public static string WriteImpulseWav(string dir, int sampleRate, int totalFrames, int channels,
            int impulseFrameIndex, bool extensible = false)
        {
            var path = Path.Combine(dir, $"impulse16_{Guid.NewGuid():N}.wav");
            var format = Pcm16Format(sampleRate, channels, extensible);
            using (var writer = new WaveFileWriter(path, format))
            {
                var frameBuf = new short[channels];
                for (int i = 0; i < totalFrames; i++)
                {
                    short v = i == impulseFrameIndex ? short.MaxValue : (short)0;
                    for (int c = 0; c < channels; c++) frameBuf[c] = v;
                    writer.WriteSamples(frameBuf, 0, channels);
                }
            }
            return path;
        }

        /// <summary>
        /// Hand-rolled WAVE_FORMAT_EXTENSIBLE file with the IEEE-float sub-format —
        /// NAudio's public WaveFormatExtensible constructor only ever produces the PCM
        /// sub-format, so this fixture is written at the raw chunk level to cover the
        /// "extensible but NOT integer PCM" case (must fall back to the float decode path).
        /// </summary>
        public static string WriteExtensibleFloatSineWav(string dir, int sampleRate, double seconds,
            double freq, double amplitude, int channels)
        {
            var path = Path.Combine(dir, $"sineExtFloat_{Guid.NewGuid():N}.wav");
            int frames = (int)(sampleRate * seconds);
            const int bitsPerSample = 32;
            int blockAlign = channels * (bitsPerSample / 8);
            int byteRate = sampleRate * blockAlign;
            int dataBytes = frames * blockAlign;
            const int cbSize = 22;
            const int fmtChunkSize = 16 + 2 + cbSize; // 40
            int riffSize = 4 + (8 + fmtChunkSize) + (8 + dataBytes);

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                bw.Write(riffSize);
                bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

                bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                bw.Write(fmtChunkSize);
                bw.Write((ushort)0xFFFE); // WAVE_FORMAT_EXTENSIBLE
                bw.Write((ushort)channels);
                bw.Write(sampleRate);
                bw.Write(byteRate);
                bw.Write((ushort)blockAlign);
                bw.Write((ushort)bitsPerSample);
                bw.Write((ushort)cbSize);
                bw.Write((ushort)bitsPerSample); // valid bits per sample
                bw.Write((uint)0); // channel mask: unspecified
                bw.Write(AudioMediaSubtypes.MEDIASUBTYPE_IEEE_FLOAT.ToByteArray());

                bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                bw.Write(dataBytes);
                for (int i = 0; i < frames; i++)
                {
                    float sample = (float)(amplitude * Math.Sin(2 * Math.PI * freq * i / sampleRate));
                    for (int c = 0; c < channels; c++)
                        bw.Write(sample);
                }
            }
            return path;
        }

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

        /// <summary>
        /// A finite mono ISampleProvider over a fixed sample array. Unlike
        /// <see cref="FixedChannelSampleProvider"/> it ends, which is what lets a test
        /// place energy at one exact sample index and assert which STFT frames see it.
        /// </summary>
        public sealed class MonoArraySampleProvider : NAudio.Wave.ISampleProvider
        {
            private readonly float[] _samples;
            private int _pos;

            public MonoArraySampleProvider(float[] samples, int sampleRate = 44100)
            {
                _samples = samples;
                WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            }

            public WaveFormat WaveFormat { get; }

            public int Read(float[] buffer, int offset, int count)
            {
                int n = Math.Min(count, _samples.Length - _pos);
                if (n <= 0) return 0;
                Array.Copy(_samples, _pos, buffer, offset, n);
                _pos += n;
                return n;
            }
        }
    }
}
