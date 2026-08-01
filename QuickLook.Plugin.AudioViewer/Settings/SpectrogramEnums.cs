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
    /// <remarks>
    /// Members are appended, never reordered: the settings combo casts its SelectedIndex
    /// straight to this enum, so the XAML item order must stay in lockstep.
    /// </remarks>
    public enum AmplitudeScale
    {
        /// <summary>Logarithmic (dB) — reveals quiet detail. 20*log10(magnitude).</summary>
        Decibel,
        /// <summary>Linear magnitude — emphasizes loud peaks.</summary>
        Linear,
        /// <summary>
        /// Half-gain logarithmic, 10*log10(magnitude), against the same contrast floor.
        /// Halving the log gain while keeping the floor lifts quiet material well up the
        /// colour ramp — a bin at 1e-4 reads -40 instead of -80 — so background detail and
        /// upper partials stay visible where the plain dB scale crushes them toward black.
        ///
        /// Named for Sonic Visualiser's dBV^2, which is what this option was requested as.
        /// Note that the literal reading of that name (log of the squared magnitude,
        /// 10*log10(m^2)) is identical to <see cref="Decibel"/> and would render exactly
        /// the same image, so it cannot be what distinguishes the two in practice.
        /// </summary>
        DecibelSquared
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

    /// <summary>How a stereo (2-channel) file's waveform is presented.</summary>
    /// <remarks>
    /// Members are appended, never reordered: the settings combo casts its SelectedIndex
    /// straight to this enum, so the XAML item order must stay in lockstep. Applies to
    /// stereo only; mono and 3+‑channel files ignore it and always render per-channel lanes.
    /// </remarks>
    public enum StereoWaveformMode
    {
        /// <summary>Two stacked lanes, one per channel (L over R).</summary>
        Split,
        /// <summary>One lane from the sample-exact (L+R)/2 mono mix — the classic pre-split look (default).</summary>
        MonoAverage,
        /// <summary>One lane; per column, min of both mins and max of both maxes. Never hides a peak.</summary>
        PeakUnion
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
