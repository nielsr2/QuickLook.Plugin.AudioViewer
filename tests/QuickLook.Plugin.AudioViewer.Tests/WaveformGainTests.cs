using QuickLook.Plugin.AudioViewer.Audio;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    public class WaveformGainTests
    {
        /// <summary>One channel whose columns all reach +/-<paramref name="peak"/>.</summary>
        private static WaveformData Flat(float peak, int columns = 100)
        {
            var min = new float[columns];
            var max = new float[columns];
            for (int i = 0; i < columns; i++) { min[i] = -peak; max[i] = peak; }
            return WaveformData.FromChannels(new[] { min }, new[] { max }, null, null);
        }

        [Fact]
        public void QuietFile_IsMagnifiedTowardTheHeadroom()
        {
            // Peaks at 0.05; 0.9 headroom wants 18x.
            double g = WaveformGain.FitToPercentile(Flat(0.05f));
            Assert.InRange(g, 17.0, 19.0);
        }

        [Fact]
        public void LoudFile_IsLeftAlone()
        {
            // Already near full scale: fitting would attenuate, which a "zoom" should not do.
            Assert.Equal(1.0, WaveformGain.FitToPercentile(Flat(0.95f)));
        }

        [Fact]
        public void Silence_DoesNotAmplifyNoiseToFullHeight()
        {
            Assert.Equal(1.0, WaveformGain.FitToPercentile(Flat(0f)));
        }

        /// <summary>
        /// The case that defeats peak normalisation, and the reason this is a percentile:
        /// a quiet recording containing one near-full-scale transient.
        /// </summary>
        [Fact]
        public void IsolatedTransient_DoesNotDefeatTheFit()
        {
            const int columns = 1000;
            var min = new float[columns];
            var max = new float[columns];
            for (int i = 0; i < columns; i++) { min[i] = -0.05f; max[i] = 0.05f; }
            min[500] = -0.99f; max[500] = 0.99f;   // one door slam

            var wf = WaveformData.FromChannels(new[] { min }, new[] { max }, null, null);

            double percentileFit = WaveformGain.FitToPercentile(wf);
            Assert.InRange(percentileFit, 17.0, 19.0);

            // A true-peak fit would see 0.99 and conclude no magnification is needed,
            // leaving the waveform exactly as flat as the user complained about.
            double peakFit = WaveformGain.FitToPercentile(wf, percentile: 1.0);
            Assert.Equal(1.0, peakFit);
        }

        [Fact]
        public void FitIsCappedAtMaxGain()
        {
            Assert.Equal(WaveformGain.MaxGain, WaveformGain.FitToPercentile(Flat(1e-5f)));
        }

        [Fact]
        public void MultichannelFitsAgainstTheLoudestChannel()
        {
            const int columns = 100;
            var quietMin = new float[columns]; var quietMax = new float[columns];
            var loudMin = new float[columns]; var loudMax = new float[columns];
            for (int i = 0; i < columns; i++)
            {
                quietMin[i] = -0.01f; quietMax[i] = 0.01f;
                loudMin[i] = -0.5f; loudMax[i] = 0.5f;
            }
            var wf = WaveformData.FromChannels(
                new[] { quietMin, loudMin }, new[] { quietMax, loudMax }, null, null);

            // A single shared gain, so the lanes stay comparable to each other. Fitting the
            // quiet lane individually would destroy exactly that comparison.
            double g = WaveformGain.FitToPercentile(wf);
            Assert.InRange(g, 1.5, 2.0);
        }

        [Fact]
        public void Step_WalksTheLadderAndStopsAtTheEnds()
        {
            Assert.Equal(2, WaveformGain.Step(1, +1));
            Assert.Equal(8, WaveformGain.Step(4, +1));
            Assert.Equal(4, WaveformGain.Step(8, -1));
            Assert.Equal(1, WaveformGain.Step(1, -1));
            Assert.Equal(64, WaveformGain.Step(64, +1));

            // From an auto-fit value that is not on the ladder, stepping still moves sensibly.
            Assert.Equal(32, WaveformGain.Step(18.0, +1));
            Assert.Equal(16, WaveformGain.Step(18.0, -1));
        }

        [Fact]
        public void FormatGain_IsCompact()
        {
            Assert.Equal("×8", WaveformGain.FormatGain(8.0));
            Assert.Equal("×2.5", WaveformGain.FormatGain(2.5));
            Assert.Equal("×1", WaveformGain.FormatGain(1.0));
        }
    }
}
