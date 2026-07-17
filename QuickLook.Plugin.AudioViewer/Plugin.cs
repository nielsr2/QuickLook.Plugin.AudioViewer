using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using QuickLook.Common.Plugin;

namespace QuickLook.Plugin.AudioViewer
{
    public class Plugin : IViewer
    {
        public static readonly string[] SupportedExtensions =
        {
            ".wav", ".mp3", ".flac", ".ogg", ".oga", ".m4a", ".aac", ".wma", ".aif", ".aiff"
        };

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
            context.ViewerContent = new TextBlock
            {
                Text = "Audio viewer (stub)",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            context.IsBusy = false;
        }

        public void Cleanup()
        {
        }
    }
}
