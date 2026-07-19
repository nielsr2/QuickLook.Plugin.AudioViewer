using QuickLook.Plugin.AudioViewer.Settings;

namespace QuickLook.Plugin.AudioViewer.Audio
{
    /// <summary>
    /// Perceptual color palettes for the spectrogram, mapping a normalized value
    /// t in [0,1] to an RGB triple. Each palette is a set of N evenly-spaced anchors
    /// interpolated piecewise-linearly; Grayscale is a linear ramp. Pure (no WPF)
    /// so it is unit-testable.
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

        private static readonly byte[][] InfernoStops =
        {
            new byte[] { 0, 0, 4 }, new byte[] { 27, 12, 65 },
            new byte[] { 74, 12, 107 }, new byte[] { 120, 28, 109 },
            new byte[] { 165, 44, 96 }, new byte[] { 207, 68, 70 },
            new byte[] { 237, 105, 37 }, new byte[] { 251, 155, 6 },
            new byte[] { 247, 209, 61 }, new byte[] { 252, 255, 164 }
        };

        private static readonly byte[][] PlasmaStops =
        {
            new byte[] { 13, 8, 135 }, new byte[] { 70, 3, 159 },
            new byte[] { 114, 1, 168 }, new byte[] { 156, 23, 158 },
            new byte[] { 189, 55, 134 }, new byte[] { 216, 87, 107 },
            new byte[] { 237, 121, 83 }, new byte[] { 251, 159, 58 },
            new byte[] { 253, 202, 38 }, new byte[] { 240, 249, 33 }
        };

        private static readonly byte[][] TurboStops =
        {
            new byte[] { 48, 18, 59 }, new byte[] { 65, 69, 171 },
            new byte[] { 70, 117, 237 }, new byte[] { 57, 162, 252 },
            new byte[] { 27, 207, 212 }, new byte[] { 36, 236, 166 },
            new byte[] { 97, 252, 108 }, new byte[] { 164, 252, 59 },
            new byte[] { 209, 232, 52 }, new byte[] { 243, 198, 58 },
            new byte[] { 254, 155, 45 }, new byte[] { 243, 99, 21 },
            new byte[] { 217, 56, 6 }, new byte[] { 177, 25, 1 },
            new byte[] { 122, 4, 2 }
        };

        private static readonly byte[][] HeatStops =
        {
            new byte[] { 0, 0, 0 }, new byte[] { 85, 0, 0 },
            new byte[] { 170, 0, 0 }, new byte[] { 255, 0, 0 },
            new byte[] { 255, 85, 0 }, new byte[] { 255, 170, 0 },
            new byte[] { 255, 255, 0 }, new byte[] { 255, 255, 128 },
            new byte[] { 255, 255, 255 }
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
                case SpectrogramColormap.Inferno:
                    Interp(t, InfernoStops, out r, out g, out b);
                    break;
                case SpectrogramColormap.Plasma:
                    Interp(t, PlasmaStops, out r, out g, out b);
                    break;
                case SpectrogramColormap.Turbo:
                    Interp(t, TurboStops, out r, out g, out b);
                    break;
                case SpectrogramColormap.Heat:
                    Interp(t, HeatStops, out r, out g, out b);
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

        // Piecewise-linear interpolation over N evenly-spaced stops (stops at
        // i/(N-1) for i in [0, N-1]). Byte-identical to the previous fixed 5-stop
        // form for a 5-entry palette, so existing Magma/Viridis output is unchanged.
        private static void Interp(double t, byte[][] cols, out byte r, out byte g, out byte b)
        {
            if (t < 0) t = 0; else if (t > 1) t = 1;
            int n = cols.Length;
            double scaled = t * (n - 1);
            int i = (int)scaled;
            if (i > n - 2) i = n - 2;
            double u = scaled - i;
            r = (byte)(cols[i][0] + u * (cols[i + 1][0] - cols[i][0]));
            g = (byte)(cols[i][1] + u * (cols[i + 1][1] - cols[i][1]));
            b = (byte)(cols[i][2] + u * (cols[i + 1][2] - cols[i][2]));
        }
    }
}
