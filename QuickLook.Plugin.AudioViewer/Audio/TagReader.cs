namespace QuickLook.Plugin.AudioViewer.Audio
{
    /// <summary>Embedded metadata for the info overlay. Any field may be empty.</summary>
    internal sealed class TagInfo
    {
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public uint Year { get; set; }
        public string Genre { get; set; }
        public byte[] CoverArt { get; set; }

        public bool HasText =>
            !string.IsNullOrWhiteSpace(Title) || !string.IsNullOrWhiteSpace(Artist) ||
            !string.IsNullOrWhiteSpace(Album) || !string.IsNullOrWhiteSpace(Genre) || Year > 0;
    }

    /// <summary>
    /// Reads embedded tags via TagLibSharp, which transparently handles the different
    /// container formats (ID3 for MP3, Vorbis comments for OGG/FLAC, MP4 atoms for M4A,
    /// etc.). Intended to run off the UI thread. Never throws — returns null on any
    /// failure or unsupported/corrupt input, so callers just fall back to no tags.
    /// </summary>
    internal static class TagReader
    {
        internal static TagInfo Read(string path)
        {
            try
            {
                using (var file = TagLib.File.Create(path))
                {
                    var t = file.Tag;
                    var info = new TagInfo
                    {
                        Title = t.Title,
                        Artist = t.JoinedPerformers,
                        Album = t.Album,
                        Year = t.Year,
                        Genre = t.JoinedGenres
                    };
                    var pics = t.Pictures;
                    if (pics != null && pics.Length > 0 && pics[0].Data != null)
                        info.CoverArt = pics[0].Data.Data;
                    return info;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
