using QuickLook.Common.Plugin;
using QuickLook.Plugin.AudioViewer.Settings;
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

        [Theory]
        [InlineData(Themes.Dark)]
        [InlineData(Themes.Light)]
        [InlineData(Themes.None)]
        public void ExplicitThemeMode_OverridesWhateverTheHostReports(Themes host)
        {
            Assert.False(ThemePalette.ResolveIsDark(ThemeMode.Light, host));
            Assert.True(ThemePalette.ResolveIsDark(ThemeMode.Dark, host));
        }

        [Fact]
        public void FollowHost_DefersToTheHostTheme()
        {
            Assert.False(ThemePalette.ResolveIsDark(ThemeMode.FollowHost, Themes.Light));
            Assert.True(ThemePalette.ResolveIsDark(ThemeMode.FollowHost, Themes.Dark));
        }

        [Fact]
        public void ResolveReverse_CoversEveryModeAndPalette()
        {
            Assert.False(ThemePalette.ResolveReverse(ReverseColormapMode.Never, true));
            Assert.False(ThemePalette.ResolveReverse(ReverseColormapMode.Never, false));

            Assert.True(ThemePalette.ResolveReverse(ReverseColormapMode.Always, true));
            Assert.True(ThemePalette.ResolveReverse(ReverseColormapMode.Always, false));

            // The whole point of the three-way setting: Auto inverts in light mode only,
            // with no second switch to contradict it.
            Assert.False(ThemePalette.ResolveReverse(ReverseColormapMode.AutoLight, true));
            Assert.True(ThemePalette.ResolveReverse(ReverseColormapMode.AutoLight, false));
        }

        [Fact]
        public void ResolveWaveColor_PrefersTheUserPickThenFallsBack()
        {
            var custom = ThemePalette.ResolveWaveColor("#FF66BB6A", true);
            Assert.Equal(0x66, custom.R);
            Assert.Equal(0xBB, custom.G);
            Assert.Equal(0x6A, custom.B);

            // An unset pick follows the theme, and differs between the two palettes.
            Assert.Equal(ThemePalette.WaveformWave(true), ThemePalette.ResolveWaveColor("", true));
            Assert.Equal(ThemePalette.WaveformWave(false), ThemePalette.ResolveWaveColor(null, false));
        }

        [Fact]
        public void ResolveWaveColor_CorruptValueFallsBackInsteadOfThrowing()
        {
            // This runs during control construction, so a hand-edited or truncated setting
            // must not take the whole preview down.
            Assert.Equal(ThemePalette.WaveformWave(true), ThemePalette.ResolveWaveColor("not-a-colour", true));
        }

        [Fact]
        public void WavePalette_StartsWithThemeDefaultAndIsOtherwiseConcrete()
        {
            Assert.Null(ThemePalette.WavePalette[0].Hex);
            for (int i = 1; i < ThemePalette.WavePalette.Length; i++)
                Assert.False(string.IsNullOrWhiteSpace(ThemePalette.WavePalette[i].Hex));
        }

        [Fact]
        public void EnumOrder_IsLoadBearingForTheSettingsCombos()
        {
            Assert.Equal(0, (int)ThemeMode.FollowHost);
            Assert.Equal(1, (int)ThemeMode.Light);
            Assert.Equal(2, (int)ThemeMode.Dark);

            Assert.Equal(0, (int)ReverseColormapMode.Never);
            Assert.Equal(1, (int)ReverseColormapMode.Always);
            Assert.Equal(2, (int)ReverseColormapMode.AutoLight);
        }
    }
}
