using System;
using System.Collections.Generic;
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

        // ---- Stereo mono-mix envelope (CombinedMin/Max) tests ----

        [Fact]
        public void Compute_Stereo_CombinedIsMonoAverage_Float()
        {
            AssertStereoCombinedIsMonoAverage(
                TestSignals.WriteMultiToneWav(Path.GetTempPath(), 44100, 0.5, new[] { 1.0, 0.5 }));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Compute_Stereo_CombinedIsMonoAverage_Pcm16(bool extensible)
        {
            AssertStereoCombinedIsMonoAverage(
                TestSignals.WriteMultiToneWavPcm16(Path.GetTempPath(), 44100, 0.5, new[] { 1.0, 0.5 },
                    extensible: extensible));
        }

        private static void AssertStereoCombinedIsMonoAverage(string wav)
        {
            try
            {
                using (var stream = AudioReaderFactory.Create(wav))
                {
                    var wf = WaveformData.Compute(stream, columns: 200);
                    Assert.Equal(2, wf.ChannelCount);
                    Assert.NotNull(wf.CombinedMin);
                    Assert.NotNull(wf.CombinedMax);

                    // L at 1.0, R at 0.5, in phase: the (L+R)/2 mono peak is 0.75 — distinctly
                    // below the ~1.0 a peak-union would show, confirming this is the average.
                    Assert.InRange(MaxOf(wf.CombinedMax), 0.73f, 0.77f);
                    Assert.InRange(MinOf(wf.CombinedMin), -0.77f, -0.73f);
                }
            }
            finally { File.Delete(wav); }
        }

        [Fact]
        public void Compute_Mono_HasNoCombinedEnvelope()
        {
            var wav = TestSignals.WriteSineWav(Path.GetTempPath(), seconds: 0.3, amplitude: 1.0, channels: 1);
            try
            {
                using (var stream = AudioReaderFactory.Create(wav))
                {
                    var wf = WaveformData.Compute(stream, columns: 100);
                    Assert.Null(wf.CombinedMin);
                    Assert.Null(wf.CombinedMax);
                }
            }
            finally { File.Delete(wav); }
        }

        [Fact]
        public void Compute_ThreeChannel_HasNoCombinedEnvelope()
        {
            var wav = TestSignals.WriteMultiToneWav(Path.GetTempPath(), 44100, 0.3, new[] { 1.0, 0.5, 0.25 });
            try
            {
                using (var stream = AudioReaderFactory.Create(wav))
                {
                    var wf = WaveformData.Compute(stream, columns: 100);
                    Assert.Equal(3, wf.ChannelCount);
                    Assert.Null(wf.CombinedMin);
                    Assert.Null(wf.CombinedMax);
                }
            }
            finally { File.Delete(wav); }
        }

        [Fact]
        public void Compute_StereoCombined_Pcm16VsFloat_MatchWithinQuantizationTolerance()
        {
            const double tolerance = 2.0 / 32768.0; // mono mixes two channels' quantization
            var floatWav = TestSignals.WriteMultiToneWav(Path.GetTempPath(), 44100, 0.5, new[] { 1.0, 0.5 });
            var pcm16Wav = TestSignals.WriteMultiToneWavPcm16(Path.GetTempPath(), 44100, 0.5, new[] { 1.0, 0.5 });
            try
            {
                WaveformData floatWf, pcm16Wf;
                using (var stream = AudioReaderFactory.Create(floatWav))
                    floatWf = WaveformData.Compute(stream, columns: 300);
                using (var stream = AudioReaderFactory.Create(pcm16Wav))
                    pcm16Wf = WaveformData.Compute(stream, columns: 300);

                Assert.Equal(floatWf.Columns, pcm16Wf.Columns);
                for (int col = 0; col < floatWf.Columns; col++)
                {
                    Assert.InRange(pcm16Wf.CombinedMax[col] - floatWf.CombinedMax[col], -tolerance, tolerance);
                    Assert.InRange(pcm16Wf.CombinedMin[col] - floatWf.CombinedMin[col], -tolerance, tolerance);
                }
            }
            finally
            {
                File.Delete(floatWav);
                File.Delete(pcm16Wav);
            }
        }

        private static float MinOf(float[] values)
        {
            float m = float.MaxValue;
            foreach (var v in values) if (v < m) m = v;
            return m;
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

        // ---- Progressive fill tests (P4) ----

        [Fact]
        public void Compute_Progressive_InvokesCallbackAboutProgressStepsTimes()
        {
            const int progressSteps = 10;
            var wav = TestSignals.WriteSineWav(Path.GetTempPath(), seconds: 1.0, amplitude: 1.0, channels: 1);
            try
            {
                int count = 0;
                using (var stream = AudioReaderFactory.Create(wav))
                    WaveformData.Compute(stream, columns: 100, onProgress: _ => count++, progressSteps: progressSteps);

                Assert.InRange(count, progressSteps - 1, progressSteps + 1);
            }
            finally { File.Delete(wav); }
        }

        [Fact]
        public void Compute_Progressive_FillsColumnsLeftToRight()
        {
            const int columns = 100;
            const int lateColumn = columns - 1;
            var wav = TestSignals.WriteSineWav(Path.GetTempPath(), seconds: 1.0, amplitude: 1.0, channels: 1);
            try
            {
                var snapshots = new List<WaveformData>();
                using (var stream = AudioReaderFactory.Create(wav))
                    WaveformData.Compute(stream, columns, onProgress: s => snapshots.Add(s), progressSteps: 10);

                Assert.True(snapshots.Count >= 2);
                var first = snapshots[0];
                var last = snapshots[snapshots.Count - 1];

                // The late column is still untouched (flat silence) in the first snapshot...
                Assert.Equal(0f, first.Max[0][lateColumn]);
                Assert.Equal(0f, first.Min[0][lateColumn]);
                // ...and an early column is already filled by the first snapshot (loud sine).
                Assert.InRange(first.Max[0][0], 0.9f, 1.01f);
                // ...but the late column is filled by the final snapshot.
                Assert.InRange(last.Max[0][lateColumn], 0.9f, 1.01f);
            }
            finally { File.Delete(wav); }
        }

        [Fact]
        public void Compute_Progressive_SnapshotsAreIndependentCopies()
        {
            const int columns = 100;
            const int lateColumn = columns - 1;
            var wav = TestSignals.WriteSineWav(Path.GetTempPath(), seconds: 1.0, amplitude: 1.0, channels: 1);
            try
            {
                var snapshots = new List<WaveformData>();
                using (var stream = AudioReaderFactory.Create(wav))
                    WaveformData.Compute(stream, columns, onProgress: s => snapshots.Add(s), progressSteps: 10);

                // Continuing (and completing) the scan must not retroactively fill the early
                // snapshot's later columns — it captured a deep copy, not a live reference.
                var first = snapshots[0];
                Assert.Equal(0f, first.Max[0][lateColumn]);
                Assert.Equal(0f, first.Min[0][lateColumn]);
            }
            finally { File.Delete(wav); }
        }

        [Fact]
        public void Compute_Progressive_FinalSnapshotEqualsReturnedResult()
        {
            const int columns = 100;
            var wav = TestSignals.WriteSineWav(Path.GetTempPath(), seconds: 1.0, amplitude: 1.0, channels: 1);
            try
            {
                var snapshots = new List<WaveformData>();
                WaveformData result;
                using (var stream = AudioReaderFactory.Create(wav))
                    result = WaveformData.Compute(stream, columns, onProgress: s => snapshots.Add(s), progressSteps: 10);

                AssertWaveformsEqual(result, snapshots[snapshots.Count - 1]);
            }
            finally { File.Delete(wav); }
        }

        [Fact]
        public void Compute_NullProgress_MatchesPlainCompute()
        {
            const int columns = 300;
            var wav = TestSignals.WriteMultiToneWav(Path.GetTempPath(), 44100, 0.5,
                new[] { 1.0, 0.0, 0.5 });
            try
            {
                WaveformData plain, nullProgress;
                using (var stream = AudioReaderFactory.Create(wav))
                    plain = WaveformData.Compute(stream, columns);
                using (var stream = AudioReaderFactory.Create(wav))
                    nullProgress = WaveformData.Compute(stream, columns, onProgress: null, progressSteps: 10);

                AssertWaveformsEqual(plain, nullProgress);
            }
            finally { File.Delete(wav); }
        }

        private static void AssertWaveformsEqual(WaveformData expected, WaveformData actual)
        {
            Assert.Equal(expected.ChannelCount, actual.ChannelCount);
            Assert.Equal(expected.Columns, actual.Columns);
            for (int c = 0; c < expected.ChannelCount; c++)
                for (int col = 0; col < expected.Columns; col++)
                {
                    Assert.Equal(expected.Max[c][col], actual.Max[c][col]);
                    Assert.Equal(expected.Min[c][col], actual.Min[c][col]);
                }
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
