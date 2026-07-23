namespace QuickLook.Plugin.AudioViewer.Settings
{
    /// <summary>How the waveform and spectrogram views are arranged.</summary>
    public enum LayoutMode
    {
        /// <summary>Waveform on top (1/4 height), spectrogram below (3/4+), both visible.</summary>
        Stacked,
        /// <summary>One view at a time; a button toggles waveform ⇄ spectrogram.</summary>
        Single
    }

    /// <summary>Spectrogram vertical (frequency) axis warping.</summary>
    public enum FrequencyScale
    {
        Linear,
        Log,
        Mel
    }

    /// <summary>How magnitude maps to color intensity.</summary>
    public enum AmplitudeScale
    {
        /// <summary>Logarithmic (dB) — reveals quiet detail.</summary>
        Decibel,
        /// <summary>Linear magnitude — emphasizes loud peaks.</summary>
        Linear
    }

    /// <summary>Spectrogram color palette.</summary>
    public enum SpectrogramColormap
    {
        Magma,
        Viridis,
        Grayscale,
        Inferno,
        Plasma,
        Turbo,
        Heat
    }

    /// <summary>How the file-info overlay is presented.</summary>
    public enum FileInfoDisplay
    {
        /// <summary>Unobtrusive info pill in the bottom-right; hovering it reveals the box, which auto-hides.</summary>
        HoverIcon,
        /// <summary>Full info box: draggable anywhere in the pane, with a ✕ to dismiss, remembers its position.</summary>
        DraggableBox
    }

    /// <summary>What the middle term of the spectrogram hover readout shows.</summary>
    /// <remarks>
    /// Members are appended, never reordered: the settings combo casts its SelectedIndex
    /// straight to this enum, so the XAML item order must stay in lockstep.
    /// </remarks>
    public enum HoverReadoutMode
    {
        /// <summary>Nearest 12-TET note plus cents deviation, e.g. "E7 -14¢".</summary>
        MidiNote,
        /// <summary>Raw cursor frequency, e.g. "2.5 kHz".</summary>
        Frequency,
        /// <summary>Both terms, e.g. "A4 +2¢  ·  440 Hz" (default).</summary>
        Both
    }
}
