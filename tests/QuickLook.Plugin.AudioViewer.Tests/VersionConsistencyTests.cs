using System;
using System.IO;
using System.Xml.Linq;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    /// <summary>
    /// QuickLook's installer reads the version from QuickLook.Plugin.Metadata.config, while
    /// the DLL carries its own assembly version. Both are single-sourced from
    /// Directory.Build.props; this test fails if they ever drift.
    /// </summary>
    public class VersionConsistencyTests
    {
        [Fact]
        public void MetadataConfigVersion_MatchesAssemblyVersion()
        {
            var asm = typeof(Plugin).Assembly;
            // AppContext.BaseDirectory is the test output dir (where the config is copied);
            // asm.Location would point to xUnit's shadow-copy cache instead.
            var dir = AppContext.BaseDirectory;
            var configPath = Path.Combine(dir, "QuickLook.Plugin.Metadata.config");
            Assert.True(File.Exists(configPath), $"metadata config not found at {configPath}");

            var configVersion = XDocument.Load(configPath).Root.Element("Version").Value.Trim();
            var asmVersion = asm.GetName().Version.ToString(3); // major.minor.build

            Assert.Equal(configVersion, asmVersion);
        }
    }
}
