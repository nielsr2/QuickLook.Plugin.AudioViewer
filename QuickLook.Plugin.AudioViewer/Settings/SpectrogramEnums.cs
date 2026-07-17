namespace QuickLook.Plugin.AudioViewer.Settings
{
    /// <summary>How the waveform and spectrogram views are arranged.</summary>
    public enum LayoutMode
    {
        /// <summary>Waveform on top (1/4 height), spectrogram below (3/4+), both visible.</summary>
        Stacked,
        /// <summary>One view at a time; a button toggles waveform ⇄ spectrogram.</summary>
        Overlay
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
        Grayscale
    }
}
