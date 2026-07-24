using QuickLook.Plugin.AudioViewer.Settings;

namespace QuickLook.Plugin.AudioViewer.Audio
{
    /// <summary>
    /// Pure transform that collapses a stereo (2-channel) <see cref="WaveformData"/> into the
    /// single-lane form the chosen <see cref="StereoWaveformMode"/> asks for, or returns it
    /// unchanged for Split and for any non-stereo input. Applied at display time, so switching
    /// modes never triggers a re-scan.
    /// </summary>
    public static class StereoWaveform
    {
        public static WaveformData Apply(WaveformData raw, StereoWaveformMode mode)
        {
            // Only stereo has a meaningful "combine"; mono / multichannel always render per-channel.
            if (raw == null || raw.ChannelCount != 2 || mode == StereoWaveformMode.Split)
                return raw;

            switch (mode)
            {
                case StereoWaveformMode.MonoAverage:
                    // Sample-exact (L+R)/2 envelope captured during the scan. If it is somehow
                    // absent (defensive — a stereo scan always fills it), fall back to peak-union.
                    if (raw.CombinedMin != null && raw.CombinedMax != null)
                        return WaveformData.FromChannels(
                            new[] { raw.CombinedMin }, new[] { raw.CombinedMax });
                    goto case StereoWaveformMode.PeakUnion;

                case StereoWaveformMode.PeakUnion:
                    return PeakUnion(raw);

                default:
                    return raw;
            }
        }

        /// <summary>One lane whose column peaks are the union of both channels': the loudest
        /// excursion of either side. Derived from the per-channel peaks already computed.</summary>
        private static WaveformData PeakUnion(WaveformData raw)
        {
            int cols = raw.Columns;
            var min = new float[cols];
            var max = new float[cols];
            var min0 = raw.Min[0];
            var min1 = raw.Min[1];
            var max0 = raw.Max[0];
            var max1 = raw.Max[1];
            for (int i = 0; i < cols; i++)
            {
                min[i] = min0[i] < min1[i] ? min0[i] : min1[i];
                max[i] = max0[i] > max1[i] ? max0[i] : max1[i];
            }
            return WaveformData.FromChannels(new[] { min }, new[] { max });
        }
    }
}
