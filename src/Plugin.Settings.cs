using BepInEx.Configuration;
using Tylevo.FieldAttachments.Runtime;

namespace Tylevo.FieldAttachments
{
    public sealed partial class Plugin
    {
        private ConfigEntry<T> BindSetting<T>(string section, string key, T value, string description) =>
            BindSetting(section, key, value, new ConfigDescription(description));

        private ConfigEntry<T> BindSetting<T>(string section, string key, T value, ConfigDescription description) =>
            Config.Bind(section, key, value, SettingsPresentation.Describe(section, key, description));
    }
}
