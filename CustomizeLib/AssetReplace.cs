using BepInEx;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CustomizeLib;

public static class TextureStore
{
    private static Dictionary<string, string> texturePathDict = new();

    public static void Init()
    {
        var textureDir = Path.Combine(Paths.PluginPath, "Textures");

        if (!Directory.Exists(textureDir))
        {
            Directory.CreateDirectory(textureDir);
        }
        try
        {
            var textureFiles = Directory.GetFiles(textureDir, "*.png", SearchOption.AllDirectories);
            foreach (string path in textureFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(path);
                texturePathDict[fileName] = path;
                Debug.Log("Loaded texture: " + fileName);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Texture loading error: " + e.Message);
        }

        ReplaceAllTextures();
    }

    private static void ReplaceAllTextures()
    {
        foreach (Texture2D tex in Resources.FindObjectsOfTypeAll<Texture2D>())
        {
            if (tex.name.StartsWith("replaced_") || !texturePathDict.TryGetValue(tex.name, out string replacePath)) continue;

            if (TryReplaceTexture(tex, replacePath))
            {
                tex.name = "replaced_" + tex.name;
                Debug.Log("Replaced texture: " + tex.name);
            }
        }

        foreach (Sprite sprite in Resources.FindObjectsOfTypeAll<Sprite>())
        {
            if (sprite.texture == null || !sprite.texture.name.StartsWith("replaced_")) continue;

            // 放弃 Tight Mesh，强制使用 FullRect (全矩形) 以显示完整的新图片内容
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
        catch (System.Exception e)
        {
            Debug.LogError("Replace error for " + originalTex.name + ": " + e.Message);
            return false;
        }
    }
}