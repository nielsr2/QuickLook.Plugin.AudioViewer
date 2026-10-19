namespace QuickLook.Plugin.AudioViewer.Settings
{
    /// <summary>
    /// Short labels for the active scales, stacked in the plot's bottom-left corner under the
    /// frequency ruler (the control prefixes them "Hz " / "Amp "). Clicking one cycles to the
    /// next scale.
    /// </summary>
    internal static class ScaleBadge
    {
        internal static string Short(FrequencyScale scale)
        {
            switch (scale)
            {
                case FrequencyScale.Linear: return "LIN";
                case FrequencyScale.Log: return "LOG";
                case FrequencyScale.Mel: return "MEL";
                default: return scale.ToString().ToUpperInvariant();
            }
        }

        internal static string Short(AmplitudeScale scale)
        {
            switch (scale)
            {
                case AmplitudeScale.Decibel: return "dB";
                case AmplitudeScale.Linear: return "LIN";
                case AmplitudeScale.DecibelSquared: return "dB²";
                default: return scale.ToString();
            }
        }

        /// <summary>The combo index after <paramref name="current"/>, wrapping to the first.</summary>
        internal static int Next(int current, int count) =>
            count <= 0 ? -1 : (current < 0 ? 0 : (current + 1) % count);
    }
}
