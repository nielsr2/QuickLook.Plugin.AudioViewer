using QuickLook.Common.Helpers;

namespace QuickLook.Plugin.AudioViewer.Settings
{
    public static class PluginSettings
    {
        public const string Domain = "QuickLook.Plugin.AudioViewer";

        public static bool AutoPlay
        {
            get => SettingHelper.Get("AutoPlay", false, Domain);
            set => SettingHelper.Set("AutoPlay", value, Domain);
        }
    }
}
