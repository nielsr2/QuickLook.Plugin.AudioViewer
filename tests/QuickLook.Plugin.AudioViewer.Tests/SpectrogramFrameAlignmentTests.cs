using System.Collections.Generic;
using QuickLook.Plugin.AudioViewer.Audio;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    /// <summary>
    /// Pins the sample-exact STFT frame alignment of <see cref="SpectrogramGenerator.ComputeMagnitudes"/>.
    ///
    /// The window does not start at sample 0. Frames are only counted once the window is
    /// full, and the sample that *completes* the window is itself counted, so with
    /// fftSize=8 and hop=4 the frames cover:
    ///
    ///     frame f  ->  samples [4f+3 .. 4f+10]
    ///
    /// The existing spectrogram tests assert a peak bin within +/-1.5 bins, which a
    /// whole-frame shift would sail straight through. These tests exist so that any
    /// rewrite of the sliding-window mechanics (e.g. replacing the per-sample
    /// Array.Copy shift with a circular index) cannot silently move every frame.
    /// If these fail after such a change, the cached/rendered time axis is now wrong.
    /// </summary>
    public class SpectrogramFrameAlignmentTests
    {
        private const int FftSize = 8;
        private const int Hop = 4;

        /// <summary>Indices of the frames that contain any non-zero energy.</summary>
        private static List<int> FramesWithEnergy(int impulseIndex, int totalSamples = 20)
        {
            var samples = new float[totalSamples];
            if (impulseIndex >= 0) samples[impulseIndex] = 1.0f;

            var frames = SpectrogramGenerator.ComputeMagnitudes(
                new TestSignals.MonoArraySampleProvider(samples),
                FftSize, Hop, maxFrames: 100, out _);

            var hits = new List<int>();
            for (int f = 0; f < frames.Length; f++)
                foreach (var m in frames[f])
                    if (m != 0f) { hits.Add(f); break; }
            return hits;
        }

        [Fact]
        public void ThreeFramesAreEmittedForTwentySamples()
        {
            // Frames land on the samples that push sinceLastFrame to hop: 10, 14, 18.
            var frames = SpectrogramGenerator.ComputeMagnitudes(
                new TestSignals.MonoArraySampleProvider(new float[20]),
                FftSize, Hop, maxFrames: 100, out bool capped);

            Assert.Equal(3, frames.Length);
            Assert.False(capped);
        }

        [Fact]
        public void FirstFrameStartsAtHopMinusOne_NotAtSampleZero()
        {
            // Sample 3 is the first sample inside frame 0's window [3..10].
            Assert.Equal(new[] { 0 }, FramesWithEnergy(3));

            // Sample 2 is one before it, so nothing sees it at all. This is the
            // assertion that catches an off-by-one in the window start.
            Assert.Empty(FramesWithEnergy(2));
        }

        [Fact]
        public void FrameWindowsOverlapByFftMinusHop()
        {
            // Sample 10 closes frame 0's window and also sits inside frame 1's [7..14].
            Assert.Equal(new[] { 0, 1 }, FramesWithEnergy(10));

            // Sample 11 is past frame 0's window but inside frames 1 and 2 ([11..18]).
            Assert.Equal(new[] { 1, 2 }, FramesWithEnergy(11));
        }

        [Fact]
        public void SilenceProducesExactlyZeroMagnitudes()
        {
            Assert.Empty(FramesWithEnergy(-1));
        }
    }
}
