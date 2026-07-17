using QuickLook.Plugin.AudioViewer.Settings;

namespace QuickLook.Plugin.AudioViewer.Audio
{
    /// <summary>
    /// Perceptual color palettes for the spectrogram, mapping a normalized value
    /// t in [0,1] to an RGB triple. Magma/Viridis use five evenly-spaced anchors;
    /// Grayscale is a linear ramp. Pure (no WPF) so it is unit-testable.
    /// </summary>
    public static class Colormaps
    {
        private static readonly byte[][] MagmaStops =
        {
            new byte[] { 0, 0, 4 }, new byte[] { 81, 18, 124 },
            new byte[] { 183, 55, 121 }, new byte[] { 252, 137, 97 },
            new byte[] { 252, 253, 191 }
        };

        private static readonly byte[][] ViridisStops =
        {
            new byte[] { 68, 1, 84 }, new byte[] { 59, 82, 139 },
            new byte[] { 33, 145, 140 }, new byte[] { 94, 201, 98 },
            new byte[] { 253, 231, 37 }
        };

        public static void Sample(SpectrogramColormap map, double t, out byte r, out byte g, out byte b)
        {
            switch (map)
            {
                case SpectrogramColormap.Viridis:
                    Interp(t, ViridisStops, out r, out g, out b);
                    break;
                case SpectrogramColormap.Grayscale:
                    Grayscale(t, out r, out g, out b);
                    break;
                default:
                    Interp(t, MagmaStops, out r, out g, out b);
                    break;
            }
        }

        public static void Magma(double t, out byte r, out byte g, out byte b) => Interp(t, MagmaStops, out r, out g, out b);

        public static void Viridis(double t, out byte r, out byte g, out byte b) => Interp(t, ViridisStops, out r, out g, out b);

        public static void Grayscale(double t, out byte r, out byte g, out byte b)
        {
            if (t < 0) t = 0; else if (t > 1) t = 1;
            byte v = (byte)(t * 255.0 + 0.5);
            r = g = b = v;
        }

        // piecewise-linear interpolation over 5 stops at {0, 0.25, 0.5, 0.75, 1}
        private static void Interp(double t, byte[][] cols, out byte r, out byte g, out byte b)
        {
            if (t < 0) t = 0; else if (t > 1) t = 1;
            double[] stops = { 0.0, 0.25, 0.5, 0.75, 1.0 };
            int i = 0;
            while (i < stops.Length - 2 && t > stops[i + 1]) i++;
            double u = (t - stops[i]) / (stops[i + 1] - stops[i]);
            r = (byte)(cols[i][0] + u * (cols[i + 1][0] - cols[i][0]));
            g = (byte)(cols[i][1] + u * (cols[i + 1][1] - cols[i][1]));
            b = (byte)(cols[i][2] + u * (cols[i + 1][2] - cols[i][2]));
        }
    }
}
