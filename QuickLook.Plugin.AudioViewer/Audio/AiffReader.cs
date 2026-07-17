using System;
using System.IO;
using NAudio.Wave;

namespace QuickLook.Plugin.AudioViewer.Audio
{
    /// <summary>
    /// A minimal AIFF / AIFF-C reader for the formats NAudio's own AiffFileReader
    /// rejects — notably AIFC "fl32" (32-bit float) — plus big/little-endian PCM.
    /// AIFF stores samples big-endian; this reader byte-swaps them to little-endian
    /// on read so the exposed WaveFormat is standard (IEEE float or PCM).
    /// </summary>
    public sealed class AiffReader : WaveStream
    {
        private readonly FileStream _fs;
        private readonly WaveFormat _format;
        private readonly long _dataStart;
        private readonly long _dataLength;
        private readonly int _bytesPerSample; // per channel sample
        private readonly int _blockAlign;
        private readonly bool _swap;          // big-endian -> little-endian
        private readonly bool _signed8;       // AIFF 8-bit PCM is signed; WAV is unsigned
        private long _position;

        /// <summary>AIFF-C compression 4CC (e.g. "fl32", "sowt"); "NONE" for plain AIFF.</summary>
        public string Compression { get; private set; }

        public AiffReader(string path)
        {
            _fs = File.OpenRead(path);
            try
            {
            if (ReadFourCC() != "FORM")
                throw new FormatException("Not an AIFF file (missing FORM).");
            ReadU32BE(); // form size (unused)
            string formType = ReadFourCC();
            bool isAifc = formType == "AIFC";
            if (formType != "AIFF" && !isAifc)
                throw new FormatException("Not an AIFF file (form type '" + formType + "').");

            int channels = 0, sampleSize = 0;
            long numFrames = 0;
            double sampleRate = 0;
            string compression = isAifc ? null : "NONE";
            long ssndStart = -1, ssndLen = 0;

            while (_fs.Position + 8 <= _fs.Length)
            {
                string cid = ReadFourCC();
                long csize = ReadU32BE();
                long next = _fs.Position + csize + (csize & 1); // chunks pad to even length

                if (cid == "COMM")
                {
                    channels = ReadU16BE();
                    numFrames = ReadU32BE();
                    sampleSize = ReadU16BE();
                    sampleRate = ReadExtended(ReadBytes(10));
                    if (isAifc) compression = ReadFourCC();
                }
                else if (cid == "SSND")
                {
                    long offset = ReadU32BE();
                    ReadU32BE(); // blockSize
                    ssndStart = _fs.Position + offset;
                    ssndLen = csize - 8 - offset;
                }

                _fs.Position = next; // advances >= 8 bytes/iteration; the while bound terminates

            }

            if (channels <= 0 || sampleSize <= 0 || ssndStart < 0)
                throw new FormatException("AIFF missing COMM/SSND data.");

            _bytesPerSample = (sampleSize + 7) / 8;
            _blockAlign = _bytesPerSample * channels;

            string comp = (compression ?? "NONE").Trim().ToLowerInvariant();
            bool isFloat;
            switch (comp)
            {
                case "none":
                case "twos":
                case "in24":
                case "in32":
                    isFloat = false; _swap = true; break;   // big-endian PCM
                case "sowt":
                    isFloat = false; _swap = false; break;  // little-endian PCM
                case "fl32":
                case "flt ":
                    isFloat = true; _swap = true; break;    // big-endian 32-bit float
                default:
                    throw new NotSupportedException("Unsupported AIFF compression '" + compression + "'.");
            }

            if (isFloat && _bytesPerSample != 4)
                throw new NotSupportedException("Only 32-bit AIFF float is supported.");

            _signed8 = !isFloat && _bytesPerSample == 1;

            _format = isFloat
                ? WaveFormat.CreateIeeeFloatWaveFormat((int)Math.Round(sampleRate), channels)
                : new WaveFormat((int)Math.Round(sampleRate), _bytesPerSample * 8, channels);

            long framesBytes = numFrames * _blockAlign;
            _dataLength = framesBytes > 0 ? Math.Min(framesBytes, ssndLen) : ssndLen;
            _dataLength -= _dataLength % _blockAlign; // whole frames only
            _dataStart = ssndStart;
            Compression = (compression ?? "NONE").Trim();
            }
            catch
            {
                _fs.Dispose(); // don't leak the handle on a malformed file
                throw;
            }
        }

        public override WaveFormat WaveFormat => _format;

        public override long Length => _dataLength;

        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0) value = 0;
                if (value > _dataLength) value = _dataLength;
                _position = value - (value % _blockAlign);
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            long remaining = _dataLength - _position;
            if (remaining <= 0) return 0;

            int toRead = (int)Math.Min(count, remaining);
            toRead -= toRead % _blockAlign; // keep whole frames (callers always request >= one frame)
            if (toRead <= 0) return 0;

            _fs.Position = _dataStart + _position;
            int done = 0;
            while (done < toRead)
            {
                int r = _fs.Read(buffer, offset + done, toRead - done);
                if (r <= 0) break;
                done += r;
            }

            if (_swap && _bytesPerSample > 1)
                for (int i = 0; i + _bytesPerSample <= done; i += _bytesPerSample)
                    Array.Reverse(buffer, offset + i, _bytesPerSample);

            if (_signed8)
                for (int i = 0; i < done; i++)
                    buffer[offset + i] = (byte)(buffer[offset + i] + 128); // signed -> unsigned

            _position += done;
            return done;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _fs?.Dispose();
            base.Dispose(disposing);
        }

        private string ReadFourCC()
        {
            var b = ReadBytes(4);
            return new string(new[] { (char)b[0], (char)b[1], (char)b[2], (char)b[3] });
        }

        private byte[] ReadBytes(int n)
        {
            var b = new byte[n];
            int done = 0;
            while (done < n)
            {
                int r = _fs.Read(b, done, n - done);
                if (r <= 0) throw new EndOfStreamException();
                done += r;
            }
            return b;
        }

        private uint ReadU32BE()
        {
            var b = ReadBytes(4);
            return (uint)((b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]);
        }

        private int ReadU16BE()
        {
            var b = ReadBytes(2);
            return (b[0] << 8) | b[1];
        }

        // 80-bit IEEE 754 extended precision (big-endian) -> double.
        private static double ReadExtended(byte[] b)
        {
            int exponent = ((b[0] & 0x7F) << 8) | b[1];
            ulong mantissa = 0;
            for (int i = 0; i < 8; i++) mantissa = (mantissa << 8) | b[2 + i];
            if (exponent == 0 && mantissa == 0) return 0;
            double sign = (b[0] & 0x80) != 0 ? -1.0 : 1.0;
            return sign * mantissa * Math.Pow(2.0, exponent - 16383 - 63);
        }
    }
}
