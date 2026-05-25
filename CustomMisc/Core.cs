using System;
using System.Collections;
using System.Reflection;
using BepInEx;
using CustomizeLib;
using HarmonyLib;
using UnityEngine;
using static CustomizeLib.Logger;

namespace CustomMisc;

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
[BepInDependency(CustomCore.PLUGIN_GUID, "3.0.0")]
public class Core : BaseUnityPlugin
{
    public const string PLUGIN_GUID = "z7572.CustomMisc";
    public const string PLUGIN_NAME = "CustomMisc";
    public const string PLUGIN_VERSION = "0.1.0";

    private void Awake()
    {
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
    }

    [HarmonyPatch(typeof(Weapons))]
    private static class WeaponsPatch
    {
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        private static void StartPostfix(Controller __instance)
        {
            CustomUtils.CoroutineRunner.Run(Coroutine(__instance));
        }

        private static IEnumerator Coroutine(Controller __instance)
        {
            yield return null;
            var weapons = __instance.transform;
            if (weapons != null && weapons.childCount > 54)
            {
                weapons.GetChild(54).GetChild(0).GetChild(0).GetChild(1).localScale = new Vector3(1.3f, 6.5f, 1.3f);
                weapons.GetChild(55).GetChild(0).GetChild(0).GetChild(2).localScale = new Vector3(1f, 5f, 1f);
            }
        }
    }
}
