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

    // 修改为 1970 年 1 月 1 日 (Unix Epoch)
    private static readonly DateTime UnmodifiedTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // 最大允许 Dump 的贴图尺寸，超过此尺寸直接跳过（防止 16K 字体贴图撑爆内存）
    private const int MaxDumpSize = 4096;

    // 贴图黑名单关键词（全小写），包含这些名称的将不会被 Dump
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
        "watermark"         // 某些内置水印
    ];

    public static void Init()
    {
        if (!ConfigHandler.GetEntry<bool>("EnableTextureReplace"))
        {
            Debug.Log("[CustomizeLib] Texture replacement is disabled in config. Skipping texture dump/replace.");
            return;
        }

        textureDir = Path.Combine(Paths.PluginPath, "Textures");

        if (!Directory.Exists(textureDir))
        {
            Directory.CreateDirectory(textureDir);
        }

        // 启动时先扫描一次初始内存
        ProcessAllTextures();

        // 挂载一个常驻后台的扫描器，用于跨场景扫描和热键触发
        GameObject scannerObj = new GameObject("TextureScanner");
        Object.DontDestroyOnLoad(scannerObj);
        scannerObj.AddComponent<TextureScanner>();
    }

    // 后台扫描组件
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
            // 延迟 2 秒扫描，等待场景内的资源完全加载完毕
            StartCoroutine(ScanDelayed());
        }

        private IEnumerator ScanDelayed()
        {
            yield return new WaitForSeconds(2f);
            ProcessAllTextures();
        }
    }

    public static void ProcessAllTextures()
    {
        int newDumpCount = 0;
        int replaceCount = 0;

        bool enableAutoDump = ConfigHandler.GetEntry<bool>("EnableAutoDumpTexture");

        foreach (Texture2D tex in Resources.FindObjectsOfTypeAll<Texture2D>())
        {
            if (!IsValidTextureToDump(tex)) continue;

            // 过滤掉名称中不能作为文件名的非法字符
            string safeName = string.Join("_", tex.name.Split(Path.GetInvalidFileNameChars()));
            string filePath = Path.Combine(textureDir, safeName + ".png");

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

        foreach (Sprite sprite in Resources.FindObjectsOfTypeAll<Sprite>())
        {
            if (sprite.texture == null || !sprite.texture.name.StartsWith("replaced_")) continue;

            sprite.OverrideGeometry(
                new Vector2[] {
                    new Vector2(0, 0),
                    new Vector2(0, sprite.texture.height),
                    new Vector2(sprite.texture.width, sprite.texture.height),
                    new Vector2(sprite.texture.width, 0)
                },
                new ushort[] { 0, 1, 2, 2, 3, 0 }
            );
        }

        if (newDumpCount > 0 || replaceCount > 0)
        {
            Debug.Log($"[CustomizeLib] Scan Complete. Dumped {newDumpCount} new textures, Replaced {replaceCount} textures.");
        }
    }

    private static bool IsValidTextureToDump(Texture2D tex)
    {
        if (string.IsNullOrEmpty(tex.name) || tex.name.StartsWith("replaced_"))
            return false;

        if (tex.width > MaxDumpSize || tex.height > MaxDumpSize)
            return false;

        string lowerName = tex.name.ToLower();
        foreach (string keyword in BlacklistKeywords)
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
            RenderTexture renderTex = RenderTexture.GetTemporary(
                tex.width,
                tex.height,
                0,
                RenderTextureFormat.Default,
                RenderTextureReadWrite.Linear);

            Graphics.Blit(tex, renderTex);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTex;

            readableTex = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            readableTex.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            readableTex.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);

            byte[] bytes = readableTex.EncodeToPNG();

            // 调用新的带重试机制的写入方法
            SafeSaveAndSetTime(savePath, bytes, UnmodifiedTime);

            Debug.Log("Dumped original texture: " + tex.name);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Failed to dump texture " + tex.name + ": " + e.Message);
        }
        finally
        {
            // [Critical Fix] 无论是否报错，强制销毁临时贴图，防止 OOM 内存泄漏
            if (readableTex != null)
            {
                Object.Destroy(readableTex);
            }
        }
    }

    // 专属的带重试的安全写入机制
    private static void SafeSaveAndSetTime(string savePath, byte[] bytes, DateTime targetTime)
    {
        File.WriteAllBytes(savePath, bytes);

        // 增加重试机制：最多尝试 10 次，每次间隔 20 毫秒
        for (int i = 0; i < 10; i++)
        {
            try
            {
                File.SetLastWriteTimeUtc(savePath, targetTime);
                return; // 设置成功直接返回
            }
            catch
            {
                System.Threading.Thread.Sleep(20);
            }
        }

        Debug.LogWarning($"[CustomizeLib] Failed to set 1970 time for {Path.GetFileName(savePath)} (File locked by OS)");
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
            Debug.LogError("Replace error for " + originalTex.name + ": " + e.Message);
            return false;
        }
    }
}