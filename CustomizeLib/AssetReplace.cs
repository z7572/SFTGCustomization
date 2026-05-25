using BepInEx;
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CustomizeLib;

public static class TextureStore
{
    private static string textureDir;
    private static readonly DateTime UnmodifiedTime = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private const int MaxDumpSize = 4096;

    private static JSONNode textureMeta;
    private static string metaFilePath;

    private static readonly Dictionary<Sprite, Sprite> _spriteMap = new();
    private static Dictionary<string, byte[]> _originalTextureBackup = new();

    public static KeyCode HotReloadKey1;
    public static KeyCode HotReloadKey2;
    public static bool SingleHotReloadKey;

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

        HotReloadKey1 = ConfigHandler.GetEntry<KeyboardShortcut>("HotReloadKeybind").MainKey;
        HotReloadKey2 = ConfigHandler.GetEntry<KeyboardShortcut>("HotReloadKeybind").Modifiers.LastOrDefault();
        if (HotReloadKey2 == KeyCode.None) SingleHotReloadKey = true;

        SceneManager.sceneLoaded += OnSceneLoaded;

        CustomUtils.CoroutineRunner.Run(DelayedProcess("Initial_Boot", 2f));
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!ConfigHandler.GetEntry<bool>("EnableTextureReplace")) return;
        CustomUtils.CoroutineRunner.Run(DelayedProcess(scene.name, 2f));
    }

    private static IEnumerator DelayedProcess(string sceneName, float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);
        LogInfo($"Processing textures for scene '{sceneName}' after {delaySeconds}s delay...");
        ScanAndProcessTextures();
        RebuildAndApplySprites();
    }

    public static void HotReload()
    {
        if (!ConfigHandler.GetEntry<bool>("EnableTextureReplace")) return;

        LogInfo("Hot Reload Triggered! Restoring and rebuilding...");

        RestoreOriginalSprites();

        foreach (var tex in Resources.FindObjectsOfTypeAll<Texture2D>())
        {
            if (tex != null && tex.name.StartsWith("replaced_"))
            {
                tex.name = tex.name.Substring("replaced_".Length);
            }
        }

        ScanAndProcessTextures();
        RebuildAndApplySprites();
    }

    private static void RestoreOriginalSprites()
    {
        Dictionary<Sprite, Sprite> reverseMap = new();
        foreach (var kvp in _spriteMap)
        {
            if (kvp.Value != null) reverseMap[kvp.Value] = kvp.Key;
        }

        foreach (var sr in Resources.FindObjectsOfTypeAll<SpriteRenderer>())
        {
            if (sr.sprite != null && reverseMap.TryGetValue(sr.sprite, out Sprite orig))
            {
                sr.sprite = orig;
            }
        }

        foreach (var img in Resources.FindObjectsOfTypeAll<Image>())
        {
            if (img.sprite != null && reverseMap.TryGetValue(img.sprite, out Sprite orig))
            {
                img.sprite = orig;
            }
        }

        // 还原完成后，安全释放内存
        foreach (var customSprite in _spriteMap.Values)
        {
            if (customSprite != null) Object.Destroy(customSprite);
        }
        _spriteMap.Clear();
    }

    private static void ScanAndProcessTextures()
    {
        var newDumpCount = 0;
        var replaceCount = 0;

        var enableAutoDump = ConfigHandler.GetEntry<bool>("EnableAutoDumpTexture");
        var enableBackup = ConfigHandler.GetEntry<bool>("EnableTextureBackup");

        metaFilePath = Path.Combine(textureDir, "TextureMeta.json");
        if (File.Exists(metaFilePath))
        {
            try
            {
                textureMeta = JSON.Parse(File.ReadAllText(metaFilePath));
            }
            catch
            {
                LogWarning("Failed to load TextureMeta.json.");
                textureMeta = new JSONObject();
            }
        }
        else
        {
            textureMeta = new JSONObject();
        }

        var keysToRemove = new List<string>();
        foreach (var key in textureMeta.Keys)
        {
            var filePath = Path.Combine(textureDir, key + ".png");
            if (!File.Exists(filePath))
            {
                keysToRemove.Add(key);
            }
        }
        foreach (var key in keysToRemove)
        {
            textureMeta.Remove(key);
        }

        foreach (var tex in Resources.FindObjectsOfTypeAll<Texture2D>())
        {
            if (!IsValidTextureToDump(tex)) continue;

            var safeName = string.Join("_", tex.name.Split(Path.GetInvalidFileNameChars()));
            var filePath = Path.Combine(textureDir, safeName + ".png");
            var fileExists = File.Exists(filePath);

            if (!fileExists && enableBackup && _originalTextureBackup.TryGetValue(safeName, out var oriBytes))
            {
                try
                {
                    tex.LoadImage(oriBytes);
                    tex.wrapMode = TextureWrapMode.Clamp;
                    LogMessage($"Restored original texture from memory backup: {tex.name}");
                }
                catch (Exception e)
                {
                    LogWarning($"Failed to restore texture {tex.name}: {e.Message}");
                }
            }

            fileExists = File.Exists(filePath);
            var isModified = fileExists && File.GetLastWriteTimeUtc(filePath) > UnmodifiedTime.AddSeconds(1);

            if (!fileExists && enableAutoDump)
            {
                DumpTexture(tex, filePath);
                newDumpCount++;
                fileExists = true;
            }

            if (fileExists)
            {
                if (isModified && enableBackup && !_originalTextureBackup.ContainsKey(safeName))
                {
                    var backupBytes = GetTextureBytes(tex);
                    if (backupBytes != null)
                    {
                        _originalTextureBackup[safeName] = backupBytes;
                        LogMessage($"Created memory backup for original texture: {tex.name}");
                    }
                }

                if (textureMeta[safeName] == null)
                {
                    var obj = new JSONObject
                    {
                        ["OriginalWidth"] = tex.width,
                        ["OriginalHeight"] = tex.height,
                        ["Anchor"] = 5,
                        ["Scale"] = 1.0f
                    };
                    textureMeta[safeName] = obj;
                }

                if (File.GetLastWriteTimeUtc(filePath) > UnmodifiedTime.AddSeconds(1))
                {
                    if (TryReplaceTexture(tex, filePath))
                    {
                        tex.name = "replaced_" + safeName;
                        replaceCount++;
                    }
                }
            }
        }

        // Save JSON
        var sortedMeta = new JSONObject();
        var keys = new List<string>();
        foreach (var key in textureMeta.Keys)
        {
            keys.Add(key);
        }

        // Sort by last write time, then by name
        keys.Sort((a, b) =>
        {
            var pathA = Path.Combine(textureDir, a + ".png");
            var pathB = Path.Combine(textureDir, b + ".png");
            var timeA = File.Exists(pathA) ? File.GetLastWriteTimeUtc(pathA) : UnmodifiedTime;
            var timeB = File.Exists(pathB) ? File.GetLastWriteTimeUtc(pathB) : UnmodifiedTime;
            var timeComparison = timeB.CompareTo(timeA);
            return timeComparison == 0 ? a.CompareTo(b) : timeComparison;
        });

        foreach (var key in keys)
        {
            sortedMeta[key] = textureMeta[key];
        }
        textureMeta = sortedMeta;

        File.WriteAllText(metaFilePath, textureMeta.ToString(4));
        

        if (newDumpCount > 0 || replaceCount > 0)
        {
            LogMessage($"Texture Scan Complete. Dumped: {newDumpCount}, Replaced: {replaceCount}.");
        }
    }

    private static void RebuildAndApplySprites()
    {
        foreach (var oldSprite in Resources.FindObjectsOfTypeAll<Sprite>())
        {
            if (oldSprite == null || string.IsNullOrEmpty(oldSprite.name)) continue;
            if (oldSprite.name.StartsWith("custom_")) continue;

            // 如果该原版 Sprite 已经生成过替代品（跨场景复用），则直接跳过创建，节省极大性能
            if (_spriteMap.ContainsKey(oldSprite)) continue;

            var texName = oldSprite.texture != null ? oldSprite.texture.name : "";
            var isReplaced = texName.StartsWith("replaced_");

            var safeName = isReplaced ?
                texName.Substring("replaced_".Length) :
                string.Join("_", texName.Split(Path.GetInvalidFileNameChars()));

            var meta = textureMeta[safeName];
            if (meta == null) continue;

            var origTexW = meta["OriginalWidth"].AsFloat;
            var origTexH = meta["OriginalHeight"].AsFloat;
            var anchor = meta["Anchor"].AsInt;
            var scale = meta["Scale"].AsFloat;

            if (!isReplaced && scale == 1.0f && anchor == 5) continue;

            var widthRatio = oldSprite.texture.width / origTexW;
            var heightRatio = oldSprite.texture.height / origTexH;

            Rect newRect = new(
                oldSprite.rect.x * widthRatio,
                oldSprite.rect.y * heightRatio,
                oldSprite.rect.width * widthRatio,
                oldSprite.rect.height * heightRatio
            );

            Vector2 origPivotNorm = new(
                oldSprite.rect.width > 0 ? oldSprite.pivot.x / oldSprite.rect.width : 0.5f,
                oldSprite.rect.height > 0 ? oldSprite.pivot.y / oldSprite.rect.height : 0.5f
            );

            var px = origPivotNorm.x;
            var py = origPivotNorm.y;

            if (anchor != 5)
            {
                px = anchor switch
                {
                    1 or 4 or 7 => 0f,
                    3 or 6 or 9 => 1f,
                    _ => px
                };
                py = anchor switch
                {
                    1 or 2 or 3 => 0f,
                    7 or 8 or 9 => 1f,
                    _ => py
                };
            }
            Vector2 newPivot = new(px, py);

            var newPPU = oldSprite.pixelsPerUnit / scale;
            if (newPPU <= 0.001f) newPPU = 0.001f;

            var newSprite = Sprite.Create(
                oldSprite.texture,
                newRect,
                newPivot,
                newPPU,
                0,
                SpriteMeshType.FullRect,
                oldSprite.border
            );
            newSprite.name = "custom_" + oldSprite.name;

            // 存入缓存池
            _spriteMap[oldSprite] = newSprite;
        }

        // 应用缓存池中的 Custom Sprite 给当前场景的所有 Renderer
        var updatedRenderers = 0;
        foreach (var sr in Resources.FindObjectsOfTypeAll<SpriteRenderer>())
        {
            if (sr.sprite != null && !sr.sprite.name.StartsWith("custom_"))
            {
                if (_spriteMap.TryGetValue(sr.sprite, out var replacement))
                {
                    sr.sprite = replacement;
                    updatedRenderers++;
                }
            }
        }

        foreach (var img in Resources.FindObjectsOfTypeAll<Image>())
        {
            if (img.sprite != null && !img.sprite.name.StartsWith("custom_"))
            {
                if (_spriteMap.TryGetValue(img.sprite, out var replacement))
                {
                    img.sprite = replacement;
                    updatedRenderers++;
                }
            }
        }

        if (updatedRenderers > 0)
        {
            LogMessage($"Applied custom sprites to {updatedRenderers} objects.");
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

            LogMessage("Dumped original texture: " + tex.name);
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

    /// <summary>
    /// 安全读取当前内存中 Texture2D 的原生 PNG 字节码
    /// 用于在贴图被污染前进行备份
    /// </summary>
    private static byte[] GetTextureBytes(Texture2D tex)
    {
        if (tex == null) return null;
        var renderTex = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
        Graphics.Blit(tex, renderTex);

        var previous = RenderTexture.active;
        RenderTexture.active = renderTex;

        var readableTex = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
        readableTex.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
        readableTex.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTex);

        byte[] bytes = null;
        try
        {
            bytes = readableTex.EncodeToPNG();
            LogMessage("Backup original texture: " + tex.name);
        }
        catch (Exception e)
        {
            LogWarning("Failed to get texture bytes for backup: " + e.Message);
        }
        finally
        {
            if (readableTex != null) Object.Destroy(readableTex);
        }
        return bytes;
    }
}