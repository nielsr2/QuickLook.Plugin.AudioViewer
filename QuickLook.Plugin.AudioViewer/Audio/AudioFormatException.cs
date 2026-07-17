using System;

namespace QuickLook.Plugin.AudioViewer.Audio
{
    public sealed class AudioFormatException : Exception
    {
        public AudioFormatException(string message, Exception inner = null)
            : base(message, inner)
        {
        }
    }
}
