using HarmonyLib;
using LevelEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CustomizeLib;

public static class PrefabManager
{
    /// <summary>
    /// 公开的预制体列表，供所有功能 Mod 读取
    /// </summary>
    public static List<GameObject> AllPrefabs { get; private set; } = [];

    /// <summary>
    /// 当预制体抓取完毕后，会触发这个事件
    /// </summary>
    public static event Action OnPrefabsLoaded;

    // 让底层库自己去打 GameManager 的 Patch，确保时序正确
    [HarmonyPatch]
    private class Patches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameManager), "Start")]
        private static void GameManagerStartPostfix()
        {
            RefreshPrefabs();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(LevelCreator), "Start")]
        private static void LevelCreatorStartPostfix()
        {
            RefreshPrefabs();
        }
    }

    private static void RefreshPrefabs()
    {
        AllPrefabs = Resources.FindObjectsOfTypeAll<GameObject>().Where(obj => obj.scene.rootCount == 0).ToList();
        Debug.Log($"[CustomizeLib] Successfully loaded {AllPrefabs.Count} prefabs.");
        OnPrefabsLoaded?.Invoke();
    }
}