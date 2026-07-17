using System;
using System.IO;
using NAudio.Vorbis;
using NAudio.Wave;

namespace QuickLook.Plugin.AudioViewer.Audio
{
    /// <summary>
    /// Creates a seekable NAudio WaveStream for a supported audio file,
    /// preferring native readers and falling back to Media Foundation.
    /// </summary>
    public static class AudioReaderFactory
    {
        public static WaveStream Create(string path)
        {
            if (!File.Exists(path))
                throw new AudioFormatException($"File not found: {path}");

            var ext = Path.GetExtension(path)?.ToLowerInvariant();
            try
            {
                switch (ext)
                {
                    case ".wav":
                        return new WaveFileReader(path);
                    case ".mp3":
                        return new Mp3FileReader(path);
                    case ".aif":
                    case ".aiff":
                        // NAudio's AiffFileReader handles plain PCM AIFF but throws on
                        // AIFF-C (e.g. "fl32" float) — and leaks its file handle when it
                        // does. Route AIFC to our own reader by peeking the FORM type.
                        return IsAifc(path) ? (WaveStream)new AiffReader(path) : new AiffFileReader(path);
                    case ".ogg":
                    case ".oga":
                        return new VorbisWaveReader(path);
                    case ".flac":
                    case ".m4a":
                    case ".aac":
                    case ".wma":
                        return new MediaFoundationReader(path);
                    default:
                        throw new AudioFormatException($"Unsupported audio format: {ext}");
                }
            }
            catch (AudioFormatException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new AudioFormatException(
                    $"Could not open audio file (unsupported or corrupt): {Path.GetFileName(path)}", ex);
            }
        }

        // Peek the FORM type: AIFF-C ("AIFC") needs our reader; plain "AIFF" uses NAudio's.
        private static bool IsAifc(string path)
        {
            try
            {
                using (var fs = File.OpenRead(path))
                {
                    var b = new byte[12];
                    if (fs.Read(b, 0, 12) < 12) return false;
                    return b[0] == 'F' && b[1] == 'O' && b[2] == 'R' && b[3] == 'M'
                        && b[8] == 'A' && b[9] == 'I' && b[10] == 'F' && b[11] == 'C';
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
