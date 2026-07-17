using System;
using System.IO;
using System.Linq;
using System.Windows;
using QuickLook.Common.Plugin;

namespace QuickLook.Plugin.AudioViewer
{
    public class Plugin : IViewer
    {
        public static readonly string[] SupportedExtensions =
        {
            ".wav", ".mp3", ".flac", ".ogg", ".oga", ".m4a", ".aac", ".wma", ".aif", ".aiff"
        };

        private AudioViewerControl _control;

        public int Priority => 0;

        public void Init()
        {
        }

        public bool CanHandle(string path)
        {
            if (Directory.Exists(path))
                return false;

            var ext = Path.GetExtension(path);
            return SupportedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
        }

        public void Prepare(string path, ContextObject context)
        {
            context.PreferredSize = new Size(800, 480);
        }

        public void View(string path, ContextObject context)
        {
            context.Title = Path.GetFileName(path);
            _control = new AudioViewerControl(path, context);   // self-loads async
            context.ViewerContent = _control;
            context.IsBusy = false;                             // control shows its own "Loading…"
        }

        public void Cleanup()
        {
            _control?.Unload();
            _control = null;
        }
    }
}
