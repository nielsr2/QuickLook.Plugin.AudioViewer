using System.Collections.Generic;
using QuickLook.Plugin.AudioViewer.Audio;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    public class NoteTextSerializerTests
    {
        [Fact]
        public void Format_SortsByStart_AndIncludesNoteNames()
        {
            var notes = new List<NoteModel>
            {
                new NoteModel { Midi = 67, StartSeconds = 1.0, DurationSeconds = 0.5, Velocity = 90 },
                new NoteModel { Midi = 60, StartSeconds = 0.0, DurationSeconds = 0.25, Velocity = 80 },
            };
            string text = NoteTextSerializer.Format(notes);

            Assert.Contains("C4", text);   // midi 60
            Assert.Contains("G4", text);   // midi 67
            Assert.Contains("0.250", text);
            // Sorted by start: C4 (t=0) appears before G4 (t=1).
            Assert.True(text.IndexOf("C4") < text.IndexOf("G4"));
        }

        [Fact]
        public void Format_Empty_IsJustTheHeader()
        {
            string text = NoteTextSerializer.Format(new List<NoteModel>());
            Assert.Contains("#", text);
            Assert.DoesNotContain("\n\n", text);
        }
    }
}
