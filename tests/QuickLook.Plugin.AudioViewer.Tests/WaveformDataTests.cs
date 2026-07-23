using System;
using System.IO;
using QuickLook.Plugin.AudioViewer.Audio;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    public class WaveformDataTests
    {
        [Fact]
        public void Compute_FullScaleSine_PeaksNearUnity()
        {
            var wav = TestSignals.WriteSineWav(Path.GetTempPath(), seconds: 1.0, amplitude: 1.0, channels: 1);
            try
            {
                using (var stream = AudioReaderFactory.Create(wav))
                {
                    var wf = WaveformData.Compute(stream, columns: 500);
                    Assert.Equal(1, wf.ChannelCount);
                    Assert.Equal(500, wf.Columns);
                    float globalMax = float.MinValue, globalMin = float.MaxValue;
                    for (int i = 0; i < wf.Columns; i++)
                    {
                        if (wf.Max[0][i] > globalMax) globalMax = wf.Max[0][i];
                        if (wf.Min[0][i] < globalMin) globalMin = wf.Min[0][i];
                    }
                    Assert.InRange(globalMax, 0.9f, 1.01f);
                    Assert.InRange(globalMin, -1.01f, -0.9f);
                }
            }
            finally { File.Delete(wav); }
        }

        [Fact]
        public void Compute_Silence_PeaksNearZero()
        {
            var wav = TestSignals.WriteSilenceWav(Path.GetTempPath(), seconds: 1.0, channels: 1);
            try
            {
                using (var stream = AudioReaderFactory.Create(wav))
                {
                    var wf = WaveformData.Compute(stream, columns: 200);
                    for (int i = 0; i < wf.Columns; i++)
                    {
                        Assert.InRange(wf.Max[0][i], -0.001f, 0.001f);
                        Assert.InRange(wf.Min[0][i], -0.001f, 0.001f);
                    }
                }
            }
            finally { File.Delete(wav); }
        }

        [Fact]
        public void Compute_MultiChannel_TracksPerChannelIndependently()
        {
            var wav = TestSignals.WriteMultiToneWav(Path.GetTempPath(), 44100, 1.0,
                new[] { 1.0, 0.0, 0.5 });
            try
            {
                using (var stream = AudioReaderFactory.Create(wav))
                {
                    var wf = WaveformData.Compute(stream, columns: 200);
                    Assert.Equal(3, wf.ChannelCount);

                    Assert.InRange(MaxOf(wf.Max[0]), 0.9f, 1.01f);   // full-scale channel
                    Assert.InRange(MaxOf(wf.Max[1]), -0.001f, 0.001f); // silent channel
                    Assert.InRange(MaxOf(wf.Max[2]), 0.4f, 0.6f);    // half-scale channel
                }
            }
            finally { File.Delete(wav); }
        }

        private static float MaxOf(float[] values)
        {
            float m = float.MinValue;
            foreach (var v in values) if (v > m) m = v;
            return m;
        }

        // ---- PCM16 fast-path tests (P2) ----

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Compute_FullScaleSine_Pcm16_PeaksNearUnity(bool extensible)
        {
            var wav = TestSignals.WriteSineWavPcm16(Path.GetTempPath(), seconds: 1.0, amplitude: 1.0,
                channels: 1, extensible: extensible);
            try
            {
                using (var stream = AudioReaderFactory.Create(wav))
                {
                    var wf = WaveformData.Compute(stream, columns: 500);
                    Assert.Equal(1, wf.ChannelCount);
                    Assert.Equal(500, wf.Columns);
                    float globalMax = float.MinValue, globalMin = float.MaxValue;
                    for (int i = 0; i < wf.Columns; i++)
                    {
                        if (wf.Max[0][i] > globalMax) globalMax = wf.Max[0][i];
                        if (wf.Min[0][i] < globalMin) globalMin = wf.Min[0][i];
                    }
                    Assert.InRange(globalMax, 0.9f, 1.01f);
                    Assert.InRange(globalMin, -1.01f, -0.9f);
                }
            }
            finally { File.Delete(wav); }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Compute_Silence_Pcm16_PeaksNearZero(bool extensible)
        {
            var wav = TestSignals.WriteSilenceWavPcm16(Path.GetTempPath(), seconds: 1.0, channels: 1,
                extensible: extensible);
            try
            {
                using (var stream = AudioReaderFactory.Create(wav))
                {
                    var wf = WaveformData.Compute(stream, columns: 200);
                    for (int i = 0; i < wf.Columns; i++)
                    {
                        Assert.InRange(wf.Max[0][i], -0.001f, 0.001f);
                        Assert.InRange(wf.Min[0][i], -0.001f, 0.001f);
                    }
                }
            }
            finally { File.Delete(wav); }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Compute_MultiChannel_Pcm16_TracksPerChannelIndependently(bool extensible)
        {
            var wav = TestSignals.WriteMultiToneWavPcm16(Path.GetTempPath(), 44100, 1.0,
                new[] { 1.0, 0.0, 0.5 }, extensible: extensible);
            try
            {
                using (var stream = AudioReaderFactory.Create(wav))
                {
                    var wf = WaveformData.Compute(stream, columns: 200);
                    Assert.Equal(3, wf.ChannelCount);

                    Assert.InRange(MaxOf(wf.Max[0]), 0.9f, 1.01f);   // full-scale channel
                    Assert.InRange(MaxOf(wf.Max[1]), -0.001f, 0.001f); // silent channel
                    Assert.InRange(MaxOf(wf.Max[2]), 0.4f, 0.6f);    // half-scale channel
                }
            }
            finally { File.Delete(wav); }
        }

        [Fact]
        public void Compute_Pcm16VsFloat_MatchWithinQuantizationTolerance()
        {
            const double tolerance = 1.5 / 32768.0;
            var amplitudes = new[] { 1.0, 0.0, 0.5, 0.25 };
            var floatWav = TestSignals.WriteMultiToneWav(Path.GetTempPath(), 44100, 0.5, amplitudes);
            var pcm16Wav = TestSignals.WriteMultiToneWavPcm16(Path.GetTempPath(), 44100, 0.5, amplitudes);
            try
            {
                WaveformData floatWf, pcm16Wf;
                using (var stream = AudioReaderFactory.Create(floatWav))
                    floatWf = WaveformData.Compute(stream, columns: 300);
                using (var stream = AudioReaderFactory.Create(pcm16Wav))
                    pcm16Wf = WaveformData.Compute(stream, columns: 300);

                Assert.Equal(floatWf.ChannelCount, pcm16Wf.ChannelCount);
                Assert.Equal(floatWf.Columns, pcm16Wf.Columns);

                for (int c = 0; c < floatWf.ChannelCount; c++)
                {
                    for (int col = 0; col < floatWf.Columns; col++)
                    {
                        Assert.InRange(pcm16Wf.Max[c][col] - floatWf.Max[c][col], -tolerance, tolerance);
                        Assert.InRange(pcm16Wf.Min[c][col] - floatWf.Min[c][col], -tolerance, tolerance);
                    }
                }
            }
            finally
            {
                File.Delete(floatWav);
                File.Delete(pcm16Wav);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Compute_Impulse_LandsInExpectedColumn(bool extensible)
        {
            const int sampleRate = 44100;
            const int totalFrames = 44100;
            const int columns = 100;
            const int impulseFrameIndex = 44099; // last frame -> last column, exercises boundary too
            long framesPerColumn = totalFrames / columns; // 441
            int expectedColumn = (int)Math.Min(columns - 1, impulseFrameIndex / framesPerColumn);

            var wav = TestSignals.WriteImpulseWav(Path.GetTempPath(), sampleRate, totalFrames, channels: 2,
                impulseFrameIndex: impulseFrameIndex, extensible: extensible);
            try
            {
                using (var stream = AudioReaderFactory.Create(wav))
                {
                    var wf = WaveformData.Compute(stream, columns: columns);
                    for (int c = 0; c < wf.ChannelCount; c++)
                    {
                        for (int col = 0; col < wf.Columns; col++)
                        {
                            if (col == expectedColumn)
                                Assert.InRange(wf.Max[c][col], 0.99f, 1.01f);
                            else
                                Assert.InRange(wf.Max[c][col], -0.001f, 0.001f);
                        }
                    }
                }
            }
            finally { File.Delete(wav); }
        }

        [Fact]
        public void Compute_TrailingRemainderFrames_LandInLastColumn()
        {
            // 1000 frames over 7 columns doesn't divide evenly (framesPerColumn = 142,
            // 7*142 = 994, leaving 6 trailing frames that must be absorbed by the last column).
            const int sampleRate = 44100;
            const int totalFrames = 1000;
            const int columns = 7;
            const int impulseFrameIndex = 999; // in the trailing remainder, beyond the 6th column's span

            var wav = TestSignals.WriteImpulseWav(Path.GetTempPath(), sampleRate, totalFrames, channels: 1,
                impulseFrameIndex: impulseFrameIndex);
            try
            {
                using (var stream = AudioReaderFactory.Create(wav))
                {
                    var wf = WaveformData.Compute(stream, columns: columns);
                    Assert.Equal(columns, wf.Columns);
                    for (int col = 0; col < wf.Columns - 1; col++)
                        Assert.InRange(wf.Max[0][col], -0.001f, 0.001f);

                    Assert.InRange(wf.Max[0][wf.Columns - 1], 0.99f, 1.01f);
                }
            }
            finally { File.Delete(wav); }
        }

        [Fact]
        public void Compute_ExtensibleFloat_DoesNotTakeIntPath()
        {
            // Extensible IEEE-float must never be routed through ComputePcm16 — if it were,
            // raw float32 bit patterns reinterpreted as little-endian int16 would either
            // silently produce wildly-out-of-[-1,1]-range "peaks" or throw an unrelated
            // buffer/format error. NAudio's WaveFileReader parses ANY on-disk
            // WAVE_FORMAT_EXTENSIBLE fmt chunk into a WaveFormatExtraData (not a
            // WaveFormatExtensible), which its own ToSampleProvider() cannot consume
            // regardless of sub-format — a pre-existing NAudio limitation, unchanged by
            // this refactor (confirmed against the pre-refactor code too). So the correct,
            // in-scope assertion here is that dispatch correctly falls through to the
            // (pre-existing) float path and fails the *same* documented NAudio way,
            // rather than silently corrupting data via the int16 fast path.
            var wav = TestSignals.WriteExtensibleFloatSineWav(Path.GetTempPath(), 44100, 1.0, 1000, 1.0, channels: 2);
            try
            {
                using (var stream = AudioReaderFactory.Create(wav))
                {
                    var ex = Assert.Throws<ArgumentException>(() => WaveformData.Compute(stream, columns: 500));
                    Assert.Contains("Unsupported source encoding", ex.Message);
                }
            }
            finally { File.Delete(wav); }
        }
    }
}
