using QuickLook.Plugin.AudioViewer.Settings;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    /// <summary>
    /// The settings popup is user-resizable and remembers its size. These pin the
    /// clamping so a remembered size can never put the pane off-screen again.
    /// </summary>
    public class SettingsPaneMetricsTests
    {
        // A roomy 1440p-ish work area, and one short enough to force a shrink:
        // the preferred 520px only fits while (work area - WorkAreaMargin) >= 520,
        // i.e. up to a 660px work area.
        private const double BigScreen = 1380;
        private const double SmallScreen = 600;

        [Fact]
        public void Width_InRange_IsUnchanged()
        {
            Assert.Equal(320, SettingsPaneMetrics.ClampWidth(320));
        }

        [Fact]
        public void Width_BelowMinimum_ClampsUp()
        {
            Assert.Equal(SettingsPaneMetrics.MinWidth, SettingsPaneMetrics.ClampWidth(10));
            Assert.Equal(SettingsPaneMetrics.MinWidth, SettingsPaneMetrics.ClampWidth(0));
            Assert.Equal(SettingsPaneMetrics.MinWidth, SettingsPaneMetrics.ClampWidth(-500));
        }

        [Fact]
        public void Width_AboveMaximum_ClampsDown()
        {
            Assert.Equal(SettingsPaneMetrics.MaxWidth, SettingsPaneMetrics.ClampWidth(5000));
        }

        [Fact]
        public void Height_InRange_IsUnchanged()
        {
            Assert.Equal(500, SettingsPaneMetrics.ClampHeight(500, BigScreen));
        }

        [Fact]
        public void Height_NeverExceedsTheWorkArea()
        {
            // The bug this guards: a remembered height taller than the screen left the
            // bottom group unreachable.
            double h = SettingsPaneMetrics.ClampHeight(99999, SmallScreen);
            Assert.True(h <= SmallScreen, $"height {h} must fit within work area {SmallScreen}");
        }

        [Fact]
        public void Height_BelowMinimum_ClampsUp()
        {
            Assert.Equal(SettingsPaneMetrics.MinHeight, SettingsPaneMetrics.ClampHeight(10, BigScreen));
        }

        [Fact]
        public void Height_OnAbsurdlyShortScreen_StillReturnsTheMinimum()
        {
            // Degenerate work area must not produce a zero/negative height.
            double h = SettingsPaneMetrics.ClampHeight(400, 50);
            Assert.Equal(SettingsPaneMetrics.MinHeight, h);
        }

        [Fact]
        public void DefaultHeight_OnALargeScreen_IsThePreferredHeight()
        {
            Assert.Equal(SettingsPaneMetrics.DefaultHeight,
                         SettingsPaneMetrics.DefaultHeightFor(BigScreen));
        }

        [Fact]
        public void DefaultHeight_OnAShortScreen_ShrinksToFit()
        {
            double h = SettingsPaneMetrics.DefaultHeightFor(SmallScreen);
            Assert.True(h < SettingsPaneMetrics.DefaultHeight, "should shrink below the preferred height");
            Assert.True(h <= SmallScreen, "must still fit the work area");
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void NonFiniteSizes_FallBackToSomethingUsable(double bad)
        {
            // WPF hands out NaN for an unset Width; a drag must not propagate it.
            double w = SettingsPaneMetrics.ClampWidth(bad);
            double h = SettingsPaneMetrics.ClampHeight(bad, BigScreen);
            Assert.InRange(w, SettingsPaneMetrics.MinWidth, SettingsPaneMetrics.MaxWidth);
            Assert.InRange(h, SettingsPaneMetrics.MinHeight, BigScreen);
        }
    }
}
