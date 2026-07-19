using QuickLook.Common.Plugin;
using QuickLook.Plugin.AudioViewer.Theming;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    public class ThemePaletteTests
    {
        [Fact]
        public void ResolveIsDark_Light_IsFalse()
            => Assert.False(ThemePalette.ResolveIsDark(Themes.Light));

        [Fact]
        public void ResolveIsDark_Dark_IsTrue()
            => Assert.True(ThemePalette.ResolveIsDark(Themes.Dark));

        [Fact]
        public void ResolveIsDark_None_FallsBackWithoutThrowing()
        {
            // None defers to the OS theme; result is environment-dependent, but it must
            // resolve to a bool rather than throw.
            var _ = ThemePalette.ResolveIsDark(Themes.None);
        }

        [Fact]
        public void WaveformColors_DifferBetweenThemes()
        {
            Assert.NotEqual(ThemePalette.WaveformWave(true), ThemePalette.WaveformWave(false));
            Assert.NotEqual(ThemePalette.WaveformBackground(true), ThemePalette.WaveformBackground(false));
        }
    }
}
