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

        var enableObsoleteSnakeGrenadeSmoke = config.Bind(CustomSect, "EnableObsoleteSnakeBombSmoke", false, "启用隐藏的蛇榴弹爆炸烟雾");
        EntriesDict[enableObsoleteSnakeGrenadeSmoke.Definition.Key] = enableObsoleteSnakeGrenadeSmoke;

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