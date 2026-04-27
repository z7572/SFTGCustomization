using BepInEx;
using HarmonyLib;
using LevelEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Bootstrap;
using UnityEngine;

namespace CustomizeLib;

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
[BepInDependency(QOL_GUID, BepInDependency.DependencyFlags.SoftDependency)]
public class CustomCore : BaseUnityPlugin
{
    public const string PLUGIN_GUID = "z7572.CustomizeLib";
    public const string PLUGIN_NAME = "CustomizeLib";
    public const string PLUGIN_VERSION = "2.2.0";

    public void Awake()
    {
        Logger.LogInfo("CustomizeLib is loaded!");

        var qolVersion = GetTargetPluginVersion(QOL_GUID);
        if (qolVersion != null && qolVersion >= new Version(1, 22, 2)) IsQOLExLoaded = true;

        try
        {
            Logger.LogInfo("Loading configuration options from config file...");
            ConfigHandler.InitConfig(Config);
        }
        catch (Exception e)
        {
            Logger.LogError("Exception on loading configuration: " + e.StackTrace + e.Message + e.Source + e.InnerException);
        }
        try
        {
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
            TextureStore.Init();
        }
        catch (Exception e)
        {
            Logger.LogError(e);
        }
    }

    private static Version GetTargetPluginVersion(string targetPluginGuid)
    {
        var targetPlugin = Chainloader.PluginInfos.FirstOrDefault(p => p.Key == targetPluginGuid).Value;
        if (targetPlugin == null) return null;

        return targetPlugin.Metadata.Version;
    }

    public static bool IsQOLExLoaded { get; private set; }
    public const string QOL_GUID = "monky.plugins.QOL";
}