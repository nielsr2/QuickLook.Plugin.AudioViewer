namespace QuickLook.Plugin.AudioViewer.Audio
{
    public enum ChannelSelectionMode
    {
        /// <summary>Average all source channels into both output channels.</summary>
        Mix,
        /// <summary>Pass one source channel to both output channels.</summary>
        Solo,
        /// <summary>Pass two source channels straight through as left/right.</summary>
        SoloPair
    }

    /// <summary>Which source channel(s) a <see cref="ChannelMixSampleProvider"/> maps to stereo output.</summary>
    public struct ChannelSelection
    {
        public ChannelSelectionMode Mode { get; }
        public int ChannelA { get; }
        public int ChannelB { get; }

        private ChannelSelection(ChannelSelectionMode mode, int channelA, int channelB)
        {
            Mode = mode;
            ChannelA = channelA;
            ChannelB = channelB;
        }

        /// <summary>Average every source channel into both L and R.</summary>
        public static readonly ChannelSelection Mix = new ChannelSelection(ChannelSelectionMode.Mix, -1, -1);

        /// <summary>Play a single 0-indexed source channel on both L and R.</summary>
        public static ChannelSelection Solo(int channelIndex) =>
            new ChannelSelection(ChannelSelectionMode.Solo, channelIndex, channelIndex);

        /// <summary>Play two 0-indexed source channels directly as left/right.</summary>
        public static ChannelSelection SoloPair(int left, int right) =>
            new ChannelSelection(ChannelSelectionMode.SoloPair, left, right);
    }
}
