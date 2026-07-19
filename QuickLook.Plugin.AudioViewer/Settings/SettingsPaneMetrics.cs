using System;

namespace QuickLook.Plugin.AudioViewer.Settings
{
    /// <summary>
    /// Size limits for the user-resizable settings popup. Pure arithmetic, kept free of
    /// WPF so the clamping is unit-tested: a remembered size must never be able to push
    /// the pane past the bottom of the screen and hide the last group.
    /// </summary>
    internal static class SettingsPaneMetrics
    {
        internal const double MinWidth = 220;
        internal const double MaxWidth = 560;
        internal const double MinHeight = 220;

        /// <summary>Preferred size on a screen with room for it.</summary>
        internal const double DefaultWidth = 300;
        internal const double DefaultHeight = 520;

        /// <summary>
        /// Vertical room reserved for the transport bar above the popup, its drop shadow
        /// and the taskbar, so the pane never runs to the very edge of the work area.
        /// </summary>
        internal const double WorkAreaMargin = 140;

        internal static double ClampWidth(double width)
        {
            if (double.IsNaN(width)) return DefaultWidth;
            return Clamp(width, MinWidth, MaxWidth);
        }

        internal static double ClampHeight(double height, double workAreaHeight)
        {
            double max = MaxHeightFor(workAreaHeight);
            if (double.IsNaN(height)) return Math.Min(DefaultHeight, max);
            return Clamp(height, MinHeight, max);
        }

        /// <summary>Starting height: the preferred one, shrunk if the screen is short.</summary>
        internal static double DefaultHeightFor(double workAreaHeight)
            => Math.Min(DefaultHeight, MaxHeightFor(workAreaHeight));

        /// <summary>
        /// Tallest the pane may be on this screen. Never returns less than
        /// <see cref="MinHeight"/>, so a degenerate work area still yields a usable pane.
        /// </summary>
        private static double MaxHeightFor(double workAreaHeight)
        {
            if (double.IsNaN(workAreaHeight)) return DefaultHeight;
            return Math.Max(MinHeight, workAreaHeight - WorkAreaMargin);
        }

        // .NET Framework 4.8 has no Math.Clamp.
        private static double Clamp(double v, double lo, double hi)
            => v < lo ? lo : v > hi ? hi : v;
    }
}
