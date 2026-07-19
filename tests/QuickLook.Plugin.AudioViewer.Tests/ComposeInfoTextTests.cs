using QuickLook.Plugin.AudioViewer.Audio;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    public class ComposeInfoTextTests
    {
        [Fact]
        public void NullTags_ReturnsTechnicalUnchanged()
            => Assert.Equal("TECH", AudioViewerControl.ComposeInfoText(null, "TECH"));

        [Fact]
        public void EmptyTags_ReturnsTechnicalUnchanged()
            => Assert.Equal("TECH", AudioViewerControl.ComposeInfoText(new TagInfo(), "TECH"));

        [Fact]
        public void WithTags_PrependsTagLinesBeforeTechnical()
        {
            var tags = new TagInfo
            {
                Title = "Song",
                Artist = "Band",
                Album = "LP",
                Year = 2020,
                Genre = "Rock"
            };
            var result = AudioViewerControl.ComposeInfoText(tags, "TECHNICAL");

            Assert.Contains("Song", result);
            Assert.Contains("Band", result);
            Assert.Contains("LP", result);
            Assert.Contains("2020", result);
            Assert.Contains("Rock", result);
            Assert.Contains("TECHNICAL", result);
            Assert.True(result.IndexOf("Song") < result.IndexOf("TECHNICAL"),
                "tag lines should come before the technical block");
        }

        [Fact]
        public void PartialTags_OmitsEmptyFields()
        {
            var tags = new TagInfo { Title = "OnlyTitle" };
            var result = AudioViewerControl.ComposeInfoText(tags, "TECH");
            Assert.Contains("OnlyTitle", result);
            Assert.DoesNotContain("Artist", result);
            Assert.DoesNotContain("Album", result);
        }
    }
}
