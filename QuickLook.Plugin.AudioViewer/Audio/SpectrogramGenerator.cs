using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NAudio.Dsp;
using NAudio.Wave;

namespace QuickLook.Plugin.AudioViewer.Audio
{
    /// <summary>
    /// Short-time Fourier transform of an audio signal, plus a colormapped
    /// bitmap renderer. The magnitude core is pure and unit-tested; the
    /// bitmap render is WPF and verified manually.
    /// </summary>
    public static class SpectrogramGenerator
    {
        // Convenience overload with the default STFT parameters. Split from the
        // full overload because C# forbids a required `out` after optional params.
        public static float[][] ComputeMagnitudes(ISampleProvider source, out bool capped)
            => ComputeMagnitudes(source, 1024, 512, 4000, out capped);

        public static float[][] ComputeMagnitudes(ISampleProvider source, int fftSize,
            int hop, int maxFrames, out bool capped)
        {
            capped = false;
            int channels = source.WaveFormat.Channels;
            int bins = fftSize / 2;
            int log2 = (int)Math.Log(fftSize, 2.0);

            var window = new float[fftSize];
            for (int i = 0; i < fftSize; i++)
                window[i] = (float)FastFourierTransform.HammingWindow(i, fftSize);

            var frames = new List<float[]>();
            var ring = new float[fftSize];
            int filled = 0;
            var readBuf = new float[channels * 4096];
            int read;
            int sinceLastFrame = 0;

            // mono-mix into a sliding window; emit a frame every `hop` samples.
            while ((read = source.Read(readBuf, 0, readBuf.Length)) > 0)
            {
                for (int i = 0; i + channels <= read; i += channels)
                {
                    float mono = 0f;
                    for (int c = 0; c < channels; c++) mono += readBuf[i + c];
                    mono /= channels;

                    if (filled < fftSize)
                    {
                        ring[filled++] = mono;
                    }
                    else
                    {
                        Array.Copy(ring, 1, ring, 0, fftSize - 1);
                        ring[fftSize - 1] = mono;
                    }

                    if (filled >= fftSize)
                    {
                        sinceLastFrame++;
                        if (sinceLastFrame >= hop)
                        {
                            sinceLastFrame = 0;
                            frames.Add(Fft(ring, window, fftSize, log2, bins));
                            if (frames.Count >= maxFrames) { capped = true; return frames.ToArray(); }
                        }
                    }
                }
            }

            // one final frame if we have a full window but never hit the hop.
            if (filled >= fftSize && frames.Count == 0)
                frames.Add(Fft(ring, window, fftSize, log2, bins));

            return frames.ToArray();
        }

        private static float[] Fft(float[] samples, float[] window, int fftSize, int log2, int bins)
        {
            var complex = new Complex[fftSize];
            for (int i = 0; i < fftSize; i++)
            {
                complex[i].X = samples[i] * window[i];
                complex[i].Y = 0f;
            }
            FastFourierTransform.FFT(true, log2, complex);

            var mag = new float[bins];
            for (int b = 0; b < bins; b++)
            {
                double re = complex[b].X, im = complex[b].Y;
                double m = Math.Sqrt(re * re + im * im);
                mag[b] = (float)(20.0 * Math.Log10(m + 1e-9)); // dB
            }
            return mag;
        }

        public static WriteableBitmap Render(float[][] magnitudes, int pixelWidth, int pixelHeight,
            double dbFloor = -90)
        {
            if (pixelWidth < 1) pixelWidth = 1;
            if (pixelHeight < 1) pixelHeight = 1;

            var bmp = new WriteableBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Bgra32, null);
            int frames = magnitudes.Length;
            int bins = frames > 0 ? magnitudes[0].Length : 1;
            var pixels = new byte[pixelWidth * pixelHeight * 4];

            // find a stable ceiling for normalization
            double dbMax = -300;
            for (int f = 0; f < frames; f++)
                for (int b = 0; b < bins; b++)
                    if (magnitudes[f][b] > dbMax) dbMax = magnitudes[f][b];
            if (dbMax < dbFloor + 1) dbMax = dbFloor + 1;

            for (int x = 0; x < pixelWidth; x++)
            {
                int f = frames > 0 ? (int)((long)x * frames / pixelWidth) : 0;
                for (int y = 0; y < pixelHeight; y++)
                {
                    // y=0 is top → high frequency; low freq at bottom
                    int b = bins > 0 ? (int)((long)(pixelHeight - 1 - y) * bins / pixelHeight) : 0;
                    double db = (frames > 0) ? magnitudes[f][b] : dbFloor;
                    double t = (db - dbFloor) / (dbMax - dbFloor);
                    if (t < 0) t = 0; if (t > 1) t = 1;
                    Magma(t, out byte r, out byte g, out byte bl);

                    int idx = (y * pixelWidth + x) * 4;
                    pixels[idx + 0] = bl;
                    pixels[idx + 1] = g;
                    pixels[idx + 2] = r;
                    pixels[idx + 3] = 255;
                }
            }

            bmp.WritePixels(new System.Windows.Int32Rect(0, 0, pixelWidth, pixelHeight),
                pixels, pixelWidth * 4, 0);
            return bmp;
        }

        // compact magma-ish colormap (t in [0,1])
        private static void Magma(double t, out byte r, out byte g, out byte b)
        {
            double[] stops = { 0.0, 0.25, 0.5, 0.75, 1.0 };
            byte[][] cols =
            {
                new byte[] { 0, 0, 4 }, new byte[] { 81, 18, 124 },
                new byte[] { 183, 55, 121 }, new byte[] { 252, 137, 97 },
                new byte[] { 252, 253, 191 }
            };
            int i = 0;
            while (i < stops.Length - 2 && t > stops[i + 1]) i++;
            double u = (t - stops[i]) / (stops[i + 1] - stops[i]);
            r = (byte)(cols[i][0] + u * (cols[i + 1][0] - cols[i][0]));
            g = (byte)(cols[i][1] + u * (cols[i + 1][1] - cols[i][1]));
            b = (byte)(cols[i][2] + u * (cols[i + 1][2] - cols[i][2]));
        }
    }
}
