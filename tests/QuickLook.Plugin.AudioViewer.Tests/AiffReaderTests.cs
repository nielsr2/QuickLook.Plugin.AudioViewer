using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;
using QuickLook.Plugin.AudioViewer.Audio;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    public class AiffReaderTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void AiffReader_DecodesAifcFloat32(int channels)
        {
            var path = WriteAifcFloat32(Path.GetTempPath(), 44100, 0.5, 1000, 0.9f, channels);
            try
            {
                using (var r = new AiffReader(path))
                {
                    Assert.Equal(WaveFormatEncoding.IeeeFloat, r.WaveFormat.Encoding);
                    Assert.InRange(r.WaveFormat.SampleRate, 44099, 44101);
                    Assert.Equal(channels, r.WaveFormat.Channels);
                    Assert.Equal(32, r.WaveFormat.BitsPerSample);
                    Assert.InRange(r.TotalTime.TotalSeconds, 0.45, 0.55);
                    Assert.InRange(Peak(r.ToSampleProvider()), 0.85f, 0.95f); // endian-swap correct
                }
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void AudioReaderFactory_OpensAifcFloat32ViaFallback()
        {
            var path = WriteAifcFloat32(Path.GetTempPath(), 48000, 0.2, 1000, 0.5f, 2);
            try
            {
                using (var s = AudioReaderFactory.Create(path))
                {
                    Assert.Equal(2, s.WaveFormat.Channels);
                    Assert.Equal(48000, s.WaveFormat.SampleRate);
                    Assert.InRange(s.TotalTime.TotalSeconds, 0.15, 0.25);
                }
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void AiffReader_DecodesBigEndianPcm16()
        {
            var path = WritePcm16Aiff(Path.GetTempPath(), 44100, 0.3, 1000, 0.8f, 1);
            try
            {
                using (var r = new AiffReader(path))
                {
                    Assert.Equal(WaveFormatEncoding.Pcm, r.WaveFormat.Encoding);
                    Assert.Equal(16, r.WaveFormat.BitsPerSample);
                    Assert.Equal(1, r.WaveFormat.Channels);
                    Assert.InRange(Peak(r.ToSampleProvider()), 0.75f, 0.85f);
                }
            }
            finally { File.Delete(path); }
        }

        private static float Peak(ISampleProvider sp)
        {
            var buf = new float[4096];
            float mx = 0; int read;
            while ((read = sp.Read(buf, 0, buf.Length)) > 0)
                for (int i = 0; i < read; i++) { float a = Math.Abs(buf[i]); if (a > mx) mx = a; }
            return mx;
        }

        // ---- test-file writers (big-endian AIFF/AIFC) ----

        private static string WriteAifcFloat32(string dir, int sampleRate, double seconds,
            double freq, float amplitude, int channels)
        {
            int frames = (int)(sampleRate * seconds);
            var data = new List<byte>();
            for (int i = 0; i < frames; i++)
            {
                float s = (float)(amplitude * Math.Sin(2 * Math.PI * freq * i / sampleRate));
                for (int c = 0; c < channels; c++) data.AddRange(Be(BitConverter.GetBytes(s)));
            }

            var comm = new List<byte>();
            comm.AddRange(U16Be(channels));
            comm.AddRange(U32Be((uint)frames));
            comm.AddRange(U16Be(32));
            comm.AddRange(Extended(sampleRate));
            comm.AddRange(new byte[] { (byte)'f', (byte)'l', (byte)'3', (byte)'2' });
            comm.Add(0); // empty pascal compression-name

            var body = new List<byte>();
            body.AddRange(Chunk("FVER", U32Be(0xA2805140)));
            body.AddRange(Chunk("COMM", comm.ToArray()));
            var ssnd = new List<byte>();
            ssnd.AddRange(U32Be(0)); ssnd.AddRange(U32Be(0)); ssnd.AddRange(data);
            body.AddRange(Chunk("SSND", ssnd.ToArray()));

            return WriteForm(dir, "AIFC", body.ToArray());
        }

        private static string WritePcm16Aiff(string dir, int sampleRate, double seconds,
            double freq, float amplitude, int channels)
        {
            int frames = (int)(sampleRate * seconds);
            var data = new List<byte>();
            for (int i = 0; i < frames; i++)
            {
                short s = (short)(amplitude * 32767 * Math.Sin(2 * Math.PI * freq * i / sampleRate));
                for (int c = 0; c < channels; c++) data.AddRange(U16Be((ushort)s)); // big-endian PCM
            }

            var comm = new List<byte>();
            comm.AddRange(U16Be(channels));
            comm.AddRange(U32Be((uint)frames));
            comm.AddRange(U16Be(16));
            comm.AddRange(Extended(sampleRate));

            var body = new List<byte>();
            body.AddRange(Chunk("COMM", comm.ToArray()));
            var ssnd = new List<byte>();
            ssnd.AddRange(U32Be(0)); ssnd.AddRange(U32Be(0)); ssnd.AddRange(data);
            body.AddRange(Chunk("SSND", ssnd.ToArray()));

            return WriteForm(dir, "AIFF", body.ToArray());
        }

        private static string WriteForm(string dir, string formType, byte[] body)
        {
            var path = Path.Combine(dir, $"aiff_{Guid.NewGuid():N}.aif");
            var all = new List<byte>();
            all.AddRange(new[] { (byte)'F', (byte)'O', (byte)'R', (byte)'M' });
            all.AddRange(U32Be((uint)(4 + body.Length)));
            foreach (var ch in formType) all.Add((byte)ch);
            all.AddRange(body);
            File.WriteAllBytes(path, all.ToArray());
            return path;
        }

        private static byte[] Chunk(string id, byte[] data)
        {
            var c = new List<byte>();
            foreach (var ch in id) c.Add((byte)ch);
            c.AddRange(U32Be((uint)data.Length));
            c.AddRange(data);
            if ((data.Length & 1) == 1) c.Add(0); // pad to even
            return c.ToArray();
        }

        private static byte[] Be(byte[] littleEndian) { Array.Reverse(littleEndian); return littleEndian; }
        private static byte[] U16Be(int v) => new[] { (byte)(v >> 8), (byte)v };
        private static byte[] U32Be(uint v) => new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };

        private static byte[] Extended(double v)
        {
            var b = new byte[10];
            if (v == 0) return b;
            int sign = 0;
            if (v < 0) { sign = 0x8000; v = -v; }
            int exp = (int)Math.Floor(Math.Log(v, 2));
            double m = v / Math.Pow(2, exp);          // [1,2)
            ulong mant = (ulong)(m * Math.Pow(2, 63));
            int expField = exp + 16383;
            b[0] = (byte)(((expField >> 8) & 0x7F) | (sign >> 8));
            b[1] = (byte)(expField & 0xFF);
            for (int i = 0; i < 8; i++) b[2 + i] = (byte)(mant >> (56 - 8 * i));
            return b;
        }
    }
}
