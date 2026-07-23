namespace QuickLook.Plugin.AudioViewer.Audio
{
    /// <summary>
    /// A single note in the piano-roll overlay: a MIDI pitch held over a time span.
    /// Pure data (no WPF), so the geometry / export layers stay unit-testable.
    /// </summary>
    internal sealed class NoteModel
    {
        /// <summary>Stable handle for selection / hit-testing.</summary>
        public int Id { get; set; }

        /// <summary>MIDI note number, 0..127.</summary>
        public int Midi { get; set; }

        /// <summary>Start time in seconds (&gt;= 0).</summary>
        public double StartSeconds { get; set; }

        /// <summary>Duration in seconds (&gt; 0).</summary>
        public double DurationSeconds { get; set; }

        /// <summary>MIDI velocity, 1..127.</summary>
        public int Velocity { get; set; } = 80;

        /// <summary>Optional label; blank by default.</summary>
        public string Label { get; set; }

        public double EndSeconds => StartSeconds + DurationSeconds;
    }
}
