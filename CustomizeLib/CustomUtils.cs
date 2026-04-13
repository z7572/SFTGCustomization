using System;
using System.IO;
using System.Reflection;
using System.Collections;
using UnityEngine;
using HarmonyLib;
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

    public static Controller controller;
}
