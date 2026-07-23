using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NAudio.Dsp;
using NAudio.Wave;
using QuickLook.Plugin.AudioViewer.Settings;

namespace QuickLook.Plugin.AudioViewer.Audio
{
    /// <summary>Options controlling how linear STFT frames are rendered to a bitmap.</summary>
    public sealed class SpectrogramRenderOptions
    {
        public int SampleRate { get; set; } = 44100;
        public int FftSize { get; set; } = 1024;
        public int PixelWidth { get; set; } = 800;
        public int PixelHeight { get; set; } = 360;
        public FrequencyScale FrequencyScale { get; set; } = FrequencyScale.Linear;
        public AmplitudeScale AmplitudeScale { get; set; } = AmplitudeScale.Decibel;
        public SpectrogramColormap Colormap { get; set; } = SpectrogramColormap.Magma;
        /// <summary>Invert the colormap (high magnitude maps to the dark end).</summary>
        public bool ReverseColormap { get; set; }
        /// <summary>Gamma applied to the normalized magnitude before coloring (1 = linear; &lt;1 lifts detail).</summary>
        public double Gamma { get; set; } = 1.0;
        /// <summary>Contrast floor in dB (negative). In dB mode this is the absolute black level.</summary>
        public double DbFloor { get; set; } = -90;
        /// <summary>Top of the axis in Hz; &lt;= 0 means full (Nyquist).</summary>
        public double MaxFrequencyHz { get; set; } = 0;
    }

    /// <summary>The amplitude-domain range a render normalized against, for the colorbar legend.</summary>
    public struct SpectrogramLevels
    {
        /// <summary>Value mapped to the bottom (darkest) of the colormap.</summary>
        public double Black;
        /// <summary>Value mapped to the top (brightest) of the colormap.</summary>
        public double Ceiling;
        /// <summary>True if the values are in dB; otherwise linear magnitude.</summary>
        public bool Decibel;
    }

    /// <summary>
    /// Short-time Fourier transform of an audio signal, plus a colormapped bitmap
    /// renderer. <see cref="ComputeMagnitudes"/> returns raw <b>linear</b> magnitude
    /// frames (pure, unit-tested); <see cref="Render"/> does all frequency-axis
    /// warping, amplitude scaling, and coloring (WPF, verified manually).
    /// </summary>
    public static class SpectrogramGenerator
    {
        // Convenience overload with the default STFT parameters. Split from the
        // full overload because C# forbids a required `out` after optional params.
        public static float[][] ComputeMagnitudes(ISampleProvider source, out bool capped)
            => ComputeMagnitudes(source, 1024, 512, 4000, out capped);

        public static float[][] ComputeMagnitudes(ISampleProvider source, int fftSize,
            int hop, int maxFrames, out bool capped, CancellationToken token = default)
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
                token.ThrowIfCancellationRequested(); // abort promptly if the preview closed
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
                mag[b] = (float)Math.Sqrt(re * re + im * im); // linear magnitude
            }
            return mag;
        }

        // Largest bitmap area we will allocate (pixels). PixelWidth is already bounded
        // by the caller (<= MaxTimelineColumns) and PixelHeight is 512, so this ceiling
        // is never hit in practice — it is a defensive guard against a pathological
        // PixelWidth so we clamp instead of allocating our way into an OutOfMemoryException.
        private const long MaxRenderPixels = 16_000_000;

        public static WriteableBitmap Render(float[][] linearFrames, SpectrogramRenderOptions o)
            => Render(linearFrames, o, out _);

        /// <summary>
        /// Render and also report the amplitude-domain normalization used, so a colorbar
        /// legend can label the same black/ceiling the image was scaled against.
        /// </summary>
        public static WriteableBitmap Render(float[][] linearFrames, SpectrogramRenderOptions o,
            out SpectrogramLevels levels)
        {
            byte[] pixels = RenderToPixels(linearFrames, o, out int pw, out int ph, out double black, out double ceiling);
            levels = new SpectrogramLevels
            {
                Black = black,
                Ceiling = ceiling,
                Decibel = o.AmplitudeScale == AmplitudeScale.Decibel
            };
            var bmp = new WriteableBitmap(pw, ph, 96, 96, PixelFormats.Bgra32, null);
            bmp.WritePixels(new System.Windows.Int32Rect(0, 0, pw, ph), pixels, pw * 4, 0);
            return bmp;
        }

        internal static byte[] RenderToPixels(float[][] linearFrames, SpectrogramRenderOptions o,
            out int pw, out int ph)
            => RenderToPixels(linearFrames, o, out pw, out ph, out _, out _);

        /// <summary>
        /// Colorize linear STFT frames into a BGRA32 pixel buffer. Pure (no WPF bitmap,
        /// no threading), so the heavy path is unit-tested directly. <paramref name="pw"/>/
        /// <paramref name="ph"/> return the (possibly clamped) output dimensions, and
        /// <paramref name="black"/>/<paramref name="ceiling"/> the amplitude-domain range
        /// the colors were normalized against (for the colorbar legend).
        /// </summary>
        internal static byte[] RenderToPixels(float[][] linearFrames, SpectrogramRenderOptions o,
            out int pw, out int ph, out double black, out double ceiling)
        {
            pw = Math.Max(1, o.PixelWidth);
            ph = Math.Max(1, o.PixelHeight);
            if ((long)pw * ph > MaxRenderPixels)
                pw = (int)Math.Max(1, MaxRenderPixels / ph);
            var pixels = new byte[pw * ph * 4];

            int frames = linearFrames?.Length ?? 0;
            int binsAvail = frames > 0 ? linearFrames[0].Length : 0;
            bool db = o.AmplitudeScale == AmplitudeScale.Decibel;

            // row 0 = lowest frequency (bottom); each row aggregates a span of FFT bins.
            var rowBins = FrequencyScaleMapper.BuildRowBins(ph, o.FrequencyScale, o.FftSize, o.SampleRate, o.MaxFrequencyHz);

            // Pass 1: peak-hold power per (column frame, row) into the amplitude domain.
            var vals = new double[pw * ph];
            double maxVal = double.NegativeInfinity;
            for (int x = 0; x < pw; x++)
            {
                int f = frames > 0 ? (int)((long)x * frames / pw) : -1;
                float[] frame = f >= 0 ? linearFrames[f] : null;
                for (int r = 0; r < ph; r++)
                {
                    double v;
                    if (frame == null)
                    {
                        v = db ? -400.0 : 0.0;
                    }
                    else
                    {
                        var br = rowBins[r];
                        int hi = Math.Min(br.Hi, binsAvail - 1);
                        double peakPow = 0.0;
                        for (int b = br.Lo; b <= hi; b++)
                        {
                            double m = frame[b];
                            double p = m * m;
                            if (p > peakPow) peakPow = p;
                        }
                        v = db ? 10.0 * Math.Log10(peakPow + 1e-12) : Math.Sqrt(peakPow);
                    }
                    vals[x * ph + r] = v;
                    if (v > maxVal) maxVal = v;
                }
            }
            if (double.IsNegativeInfinity(maxVal)) maxVal = db ? 0.0 : 1.0;

            // Normalization: dB uses an absolute floor + adaptive ceiling (silence stays
            // dark, matching the original look); linear is peak-relative with a silence guard.
            double range;
            if (db)
            {
                double dbCeiling = Math.Max(maxVal, o.DbFloor + 1.0);
                black = o.DbFloor;
                range = Math.Max(1.0, dbCeiling - black);
            }
            else if (maxVal < 1e-6)
            {
                black = 0.0; range = 1.0; // effectively silent -> all dark
            }
            else
            {
                black = maxVal * Math.Pow(10.0, o.DbFloor / 20.0);
                range = Math.Max(1e-12, maxVal - black);
            }
            ceiling = black + range; // top of the color scale, in the amplitude domain

            // Pass 2: normalize + colorize. y=0 is the top => highest frequency.
            double gamma = o.Gamma > 0 ? o.Gamma : 1.0;
            bool reverse = o.ReverseColormap;
            for (int x = 0; x < pw; x++)
            {
                for (int y = 0; y < ph; y++)
                {
                    int r = ph - 1 - y;
                    double t = (vals[x * ph + r] - black) / range;
                    if (t < 0) t = 0; else if (t > 1) t = 1;
                    t = Math.Pow(t, gamma);
                    if (reverse) t = 1 - t;
                    Colormaps.Sample(o.Colormap, t, out byte cr, out byte cg, out byte cb);

                    int idx = (y * pw + x) * 4;
                    pixels[idx + 0] = cb;
                    pixels[idx + 1] = cg;
                    pixels[idx + 2] = cr;
                    pixels[idx + 3] = 255;
                }
            }

            return pixels;
        }
    }
}
