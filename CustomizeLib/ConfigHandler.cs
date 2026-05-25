using System;
using System.Linq;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace CustomizeLib;

// From Monky's QOL-Mod
public static class ConfigHandler
{
    private static readonly Dictionary<string, ConfigEntryBase> EntriesDict = new(StringComparer.InvariantCultureIgnoreCase);

    private const string CustomSect = "Custom";

    public static void InitConfig(ConfigFile config)
    {
        var enableTextureReplace = config.Bind(CustomSect, "EnableTextureReplace", true, "启用纹理替换");
        EntriesDict[enableTextureReplace.Definition.Key] = enableTextureReplace;

        var enableAutoDumpTexture = config.Bind(CustomSect, "EnableAutoDumpTexture", false, "是否自动导出纹理");
        EntriesDict[enableAutoDumpTexture.Definition.Key] = enableAutoDumpTexture;

        var enableTextureBackup = config.Bind(CustomSect, "EnableTextureBackup", false, "是否在纹理替换时备份原始纹理用于恢复");
        EntriesDict[enableTextureBackup.Definition.Key] = enableTextureBackup;

        var hotReloadKeybind = config.Bind(CustomSect, "HotReloadKeybind", new KeyboardShortcut(KeyCode.F3, KeyCode.T), "热重载快捷键");
        EntriesDict[hotReloadKeybind.Definition.Key] = hotReloadKeybind;

        hotReloadKeybind.SettingChanged += (_, _) =>
        {
            var shortcut = hotReloadKeybind.Value;
            TextureStore.HotReloadKey1 = shortcut.MainKey;
            TextureStore.HotReloadKey2 = shortcut.Modifiers.LastOrDefault();
            TextureStore.SingleHotReloadKey = TextureStore.HotReloadKey2 == KeyCode.None;
        };

        var enableObsoleteSnakeGrenadeParticle = config.Bind(CustomSect, "EnableObsoleteSnakeGrenadeParticle", false, "启用隐藏的蛇榴弹爆炸烟雾");
        EntriesDict[enableObsoleteSnakeGrenadeParticle.Definition.Key] = enableObsoleteSnakeGrenadeParticle;

    }

    public static T GetEntry<T>(string entryKey, bool defaultValue = false)
        => defaultValue ? (T)EntriesDict[entryKey].DefaultValue : (T)EntriesDict[entryKey].BoxedValue;

    public static void ModifyEntry(string entryKey, string value)
        => EntriesDict[entryKey].SetSerializedValue(value);

    public static void ResetEntry(string entryKey)
    {
        var configEntry = EntriesDict[entryKey];
        configEntry.BoxedValue = configEntry.DefaultValue;
    }

    public static bool EntryExists(string entryKey)
        => EntriesDict.ContainsKey(entryKey);

    public static string[] GetConfigKeys() => EntriesDict.Keys.ToArray();

}