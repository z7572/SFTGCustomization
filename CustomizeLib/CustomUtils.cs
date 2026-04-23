using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CustomizeLib;

public static class CustomUtils
{
    public class CoroutineRunner : MonoBehaviour
    {
        private static CoroutineRunner _instance;
        public static CoroutineRunner Instance
        {
            get
            {
                if (_instance) return _instance;
                GameObject runnerObject = new("CoroutineRunner");
                _instance = runnerObject.AddComponent<CoroutineRunner>();
                DontDestroyOnLoad(runnerObject);
                return _instance;
            }
        }

        public static Coroutine Run(IEnumerator coroutine)
        {
            return Instance.StartCoroutine(coroutine);
        }

        public static void Stop(Coroutine coroutine)
        {
            if (coroutine != null) Instance.StopCoroutine(coroutine);
        }
    }

    public static void ReplaceWeaponCollider(GameObject oriObj, GameObject newObj, string replaceObjName = "collider")
    {
        foreach (Transform child in oriObj.transform)
        {
            if (!child.name.ToLower().Contains(replaceObjName.ToLower())) continue;

            child.GetComponent<MeshRenderer>().enabled = false;
        }
        //foreach (var renderer in newObj.GetComponentsInChildren<Renderer>())
        //{
        //    foreach (Material mat in renderer.materials)
        //    {
        //        mat.shader = Shader.Find("Standard");
        //    }
        //}
        foreach (Transform child in newObj.transform)
        {
            if (!child.name.ToLower().Contains(replaceObjName.ToLower())) continue;

            GameObject newChild = Object.Instantiate(child.gameObject, oriObj.transform);
            newChild.name = child.name;
            Object.Destroy(newChild.GetComponent<BoxCollider>());
        }
    }

    public static void StartCoroutine(IEnumerator coroutine)
    {
        CoroutineRunner.Run(coroutine);
    }

    public static void StopCoroutine(Coroutine coroutine)
    {
        CoroutineRunner.Stop(coroutine);
    }

    // https://github.com/Infinite-75/PVZRHCustomization/blob/master/BepInEx/CustomizeLib.BepInEx/CustomCore.cs#L67
    public static AssetBundle GetAssetBundle(Assembly assembly, string name)
    {
        var logger = BepInEx.Logging.Logger.CreateLogSource(CustomCore.PLUGIN_NAME);
        try
        {
            using Stream stream = assembly.GetManifestResourceStream(assembly.FullName!.Split(',')[0] + "." + name) ?? assembly.GetManifestResourceStream(name)!;
            using MemoryStream stream1 = new();
            stream.CopyTo(stream1);
            var ab = AssetBundle.LoadFromMemory(stream1.ToArray());
            logger.LogInfo($"Successfully load AssetBundle {name}.");

            return ab;
        }
        catch (Exception e)
        {
            logger.LogError(e.Source);
            throw new ArgumentException($"Failed to load {name} \n{e}");
        }
    }

    /// <summary>
    /// 内部专用的内存清理组件，带智能防重复克隆缓存
    /// </summary>
    internal class MaterialCleanup : MonoBehaviour
    {
        // 核心优化：记录组件(Renderer/Graphic)到其专属克隆材质的映射
        private readonly Dictionary<Component, Material> _clonedMaterials = new();

        public Material GetOrCloneMaterial(Renderer renderer)
        {
            // 1. 如果字典里已经有这个组件的克隆体，直接返回，绝对不重复克隆！
            if (_clonedMaterials.TryGetValue(renderer, out var mat) && mat != null)
                return mat;

            // 2. 首次调用：利用 Unity 底层机制克隆（renderer.material 会自动克隆并赋值）
            mat = renderer.material;

            // 3. 缓存起来
            _clonedMaterials[renderer] = mat;
            return mat;
        }

        public Material GetOrCloneMaterial(Graphic graphic)
        {
            if (_clonedMaterials.TryGetValue(graphic, out var mat) && mat != null)
                return mat;

            // 首次调用：UI 不会自动克隆，我们必须手动 new
            mat = new Material(graphic.material);
            graphic.material = mat; // 别忘了把新材质赋回给 UI

            _clonedMaterials[graphic] = mat;
            return mat;
        }

        void OnDestroy()
        {
            // 遍历所有缓存的材质并安全销毁
            foreach (var mat in _clonedMaterials.Values)
            {
                if (mat != null)
                {
                    Destroy(mat);
                }
            }
            _clonedMaterials.Clear();
        }
    }

    public static Controller controller;
}
