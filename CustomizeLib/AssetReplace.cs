using BepInEx;
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CustomizeLib;

public static class TextureStore
{
    private static string textureDir;
    private static readonly DateTime UnmodifiedTime = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private const int MaxDumpSize = 4096;

    private static readonly string[] BlacklistKeywords =
    [
        "unity_",           // Unity 内置引擎贴图
        "default-",         // Unity 默认贴图
        "font texture",     // 旧版 UGUI 动态字体贴图
        " sdf",             // TMP SDF 字体特征
        " atlas",           // TMP 图集特征
        "liberationsans",   // TMP 默认英文字体
        "reflection",       // 场景反射探针
        "lightmap",         // 场景光照贴图
        "shadowmap",        // 阴影贴图
        "camera target",    // 摄像机渲染目标
        "watermark",        // 某些内置水印
        "ldr_lll"           // 后处理噪声贴图
    ];

    public static void Init()
    {
        if (!ConfigHandler.GetEntry<bool>("EnableTextureReplace"))
        {
            LogInfo("Texture replacement is disabled in config. Skipping texture dump/replace.");
            return;
        }

        textureDir = Path.Combine(Paths.PluginPath, "Textures");

        if (!Directory.Exists(textureDir))
        {
            Directory.CreateDirectory(textureDir);
        }

        ProcessAllTextures();

        var scannerObj = new GameObject("TextureScanner");
        Object.DontDestroyOnLoad(scannerObj);
        scannerObj.AddComponent<TextureScanner>();
    }

    private class TextureScanner : MonoBehaviour
    {
        void Start()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StartCoroutine(ScanDelayed());
        }

        private IEnumerator ScanDelayed()
        {
            yield return new WaitForSecondsRealtime(2f);
            ProcessAllTextures();
        }
    }

    public static void ProcessAllTextures()
    {
        var newDumpCount = 0;
        var replaceCount = 0;

        var enableAutoDump = ConfigHandler.GetEntry<bool>("EnableAutoDumpTexture");

        foreach (var tex in Resources.FindObjectsOfTypeAll<Texture2D>())
        {
            if (!IsValidTextureToDump(tex)) continue;

            // 过滤掉名称中不能作为文件名的非法字符
            var safeName = string.Join("_", tex.name.Split(Path.GetInvalidFileNameChars()));
            var filePath = Path.Combine(textureDir, safeName + ".png");

            if (!File.Exists(filePath))
            {
                if (enableAutoDump)
                {
                    DumpTexture(tex, filePath);
                    newDumpCount++;
                }
            }
            else
            {
                // 如果文件修改时间大于我们设定的 1970.1.1 (加上极小容错)
                if (File.GetLastWriteTimeUtc(filePath) > UnmodifiedTime.AddSeconds(1))
                {
                    if (TryReplaceTexture(tex, filePath))
                    {
                        tex.name = "replaced_" + tex.name;
                        replaceCount++;
                    }
                }
            }
        }

        foreach (var sprite in Resources.FindObjectsOfTypeAll<Sprite>())
        {
            if (sprite.texture == null || !sprite.texture.name.StartsWith("replaced_")) continue;

            sprite.OverrideGeometry(
                // Left Bottom, Left Top, Right Top, Right Bottom
                [
                    new Vector2(0, 0),
                    new Vector2(0, sprite.texture.height),
                    new Vector2(sprite.texture.width, sprite.texture.height),
                    new Vector2(sprite.texture.width, 0)
                ],
                [0, 1, 2, 2, 3, 0]
            );
        }

        if (newDumpCount > 0 || replaceCount > 0)
        {
            LogInfo($"Scan Complete. Dumped {newDumpCount} new textures, Replaced {replaceCount} textures.");
        }
    }

    private static bool IsValidTextureToDump(Texture2D tex)
    {
        if (string.IsNullOrEmpty(tex.name) || tex.name.StartsWith("replaced_"))
            return false;

        if (tex.width > MaxDumpSize || tex.height > MaxDumpSize)
            return false;

        var lowerName = tex.name.ToLower();
        foreach (var keyword in BlacklistKeywords)
        {
            if (lowerName.Contains(keyword))
                return false;
        }

        if ((tex.hideFlags & HideFlags.HideAndDontSave) == HideFlags.HideAndDontSave)
            return false;

        return true;
    }

    private static void DumpTexture(Texture2D tex, string savePath)
    {
        Texture2D readableTex = null;
        try
        {
            var renderTex = RenderTexture.GetTemporary(
                tex.width,
                tex.height,
                0,
                RenderTextureFormat.Default,
                RenderTextureReadWrite.Linear);

            Graphics.Blit(tex, renderTex);
            var previous = RenderTexture.active;
            RenderTexture.active = renderTex;

            readableTex = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            readableTex.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            readableTex.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);

            var bytes = readableTex.EncodeToPNG();

            SafeSaveAndSetTime(savePath, bytes, UnmodifiedTime);

            LogInfo("Dumped original texture: " + tex.name);
        }
        catch (Exception e)
        {
            LogWarning("Failed to dump texture " + tex.name + ": " + e.Message);
        }
        finally
        {
            if (readableTex != null)
            {
                Object.Destroy(readableTex);
            }
        }
    }

    private static void SafeSaveAndSetTime(string savePath, byte[] bytes, DateTime targetTime)
    {
        File.WriteAllBytes(savePath, bytes);

        for (var i = 0; i < 10; i++)
        {
            try
            {
                File.SetLastWriteTimeUtc(savePath, targetTime);
                return;
            }
            catch
            {
                System.Threading.Thread.Sleep(20);
            }
        }

        LogWarning($"Failed to set 1970 time for {Path.GetFileName(savePath)} (File locked by OS)");
    }

    private static bool TryReplaceTexture(Texture2D originalTex, string replacePath)
    {
        if (originalTex == null || !File.Exists(replacePath)) return false;

        try
        {
            var texData = File.ReadAllBytes(replacePath);

            originalTex.LoadImage(texData);
            originalTex.wrapMode = TextureWrapMode.Clamp;

            return true;
        }
        catch (Exception e)
        {
            LogError("Replace error for " + originalTex.name + ": " + e.Message);
            return false;
        }
    }
}