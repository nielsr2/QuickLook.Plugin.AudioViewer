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
                        return new AiffFileReader(path);
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
    }
}
