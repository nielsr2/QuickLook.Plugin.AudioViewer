using System;
using System.Linq;
using QuickLook.Plugin.AudioViewer.Settings;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    public class ScaleBadgeTests
    {
        [Theory]
        [InlineData(FrequencyScale.Linear, "LIN")]
        [InlineData(FrequencyScale.Log, "LOG")]
        [InlineData(FrequencyScale.Mel, "MEL")]
        public void FrequencyBadges(FrequencyScale scale, string expected)
        {
            Assert.Equal(expected, ScaleBadge.Short(scale));
        }

        [Theory]
        [InlineData(AmplitudeScale.Decibel, "dB")]
        [InlineData(AmplitudeScale.Linear, "LIN")]
        [InlineData(AmplitudeScale.DecibelSquared, "dB²")]
        public void AmplitudeBadges(AmplitudeScale scale, string expected)
        {
            Assert.Equal(expected, ScaleBadge.Short(scale));
        }

        // An appended enum member must get its own badge, not fall through to a blank corner.
        [Fact]
        public void EveryScaleHasADistinctBadge()
        {
            var freq = Enum.GetValues(typeof(FrequencyScale)).Cast<FrequencyScale>()
                           .Select(ScaleBadge.Short).ToList();
            Assert.All(freq, s => Assert.False(string.IsNullOrEmpty(s)));
            Assert.Equal(freq.Count, freq.Distinct().Count());

            var amp = Enum.GetValues(typeof(AmplitudeScale)).Cast<AmplitudeScale>()
                          .Select(ScaleBadge.Short).ToList();
            Assert.All(amp, s => Assert.False(string.IsNullOrEmpty(s)));
            Assert.Equal(amp.Count, amp.Distinct().Count());
        }

        [Fact]
        public void NextWrapsAround()
        {
            Assert.Equal(1, ScaleBadge.Next(0, 3));
            Assert.Equal(2, ScaleBadge.Next(1, 3));
            Assert.Equal(0, ScaleBadge.Next(2, 3));
            Assert.Equal(0, ScaleBadge.Next(-1, 3)); // nothing selected yet: start at the first
        }
    }
}
