using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace QuickLook.Plugin.AudioViewer.Audio
{
    /// <summary>
    /// Renders notes to a human-readable, paste-friendly text snippet (one line per note,
    /// sorted by start time). Pure, so it is unit-tested.
    /// </summary>
    internal static class NoteTextSerializer
    {
        public static string Format(IEnumerable<NoteModel> notes)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# note   start(s)   dur(s)   vel");
            foreach (var n in notes.OrderBy(n => n.StartSeconds).ThenBy(n => n.Midi))
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0,-6}  {1,8:0.000}  {2,7:0.000}  {3,3}",
                    MidiNote.NoteName(n.Midi), n.StartSeconds, n.DurationSeconds, n.Velocity));
            }
            return sb.ToString().TrimEnd();
        }
    }
}
