using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CustomizeLib;

public static class Extensions
{
    public static void CopyTo(this Stream source, Stream destination, int bufferSize = 81920)
    {
        byte[] array = new byte[bufferSize];
        int count;
        while ((count = source.Read(array, 0, array.Length)) != 0)
        {
            destination.Write(array, 0, count);
        }
    }

    public static bool IsAI(this Controller controller)
    {
        return CustomCore.IsQOLExLoaded
            ? controller.isAI && !controller.gameObject.GetComponent("QOL.AFKManager")
            : controller.isAI;
    }

    private static readonly MaterialPropertyBlock _propBlock = new();
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    /// <summary>
    /// 仅设置基础颜色 (_Color)
    /// </summary>
    public static void SetMaterialColor(this Renderer renderer, Color color)
    {
        if (renderer == null) return;

        renderer.GetPropertyBlock(_propBlock);
        _propBlock.SetColor(ColorId, color);
        renderer.SetPropertyBlock(_propBlock);
    }

    /// <summary>
    /// 仅设置发光颜色 (_EmissionColor)
    /// </summary>
    public static void SetMaterialEmissionColor(this Renderer renderer, Color emissionColor)
    {
        if (renderer == null) return;

        renderer.GetPropertyBlock(_propBlock);
        _propBlock.SetColor(EmissionColorId, emissionColor);
        renderer.SetPropertyBlock(_propBlock);
    }

    /// <summary>
    /// 同时设置基础颜色 (_Color) 和发光颜色 (_EmissionColor)
    /// </summary>
    public static void SetMaterialColor(this Renderer renderer, Color color, Color emissionColor)
    {
        if (renderer == null) return;

        renderer.GetPropertyBlock(_propBlock);
        _propBlock.SetColor(ColorId, color);
        _propBlock.SetColor(EmissionColorId, emissionColor);
        renderer.SetPropertyBlock(_propBlock);
    }

    /// <summary>
    /// 获取当前真实的基础颜色 (优先读取 PropertyBlock，如果没有则读取 sharedMaterial)
    /// </summary>
    /// <returns>
    /// 当前的基础颜色，如果未设置则返回透明色
    /// </returns>
    public static Color GetMaterialColor(this Renderer renderer)
    {
        if (renderer == null) return Color.clear;

        renderer.GetPropertyBlock(_propBlock);
        Color blockColor = _propBlock.GetVector(ColorId);
        if (blockColor.maxColorComponent > 0.001f || blockColor.a > 0.001f)
        {
            return blockColor;
        }

        if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty(ColorId))
        {
            return renderer.sharedMaterial.color;
        }

        return Color.clear;
    }

    /// <summary>
    /// 获取当前真实的发光颜色 (优先读取 PropertyBlock，如果没有则读取 sharedMaterial)
    /// </summary>
    /// <returns>
    /// 当前的发光颜色，如果未设置则返回黑色
    /// </returns>
    public static Color GetMaterialEmissionColor(this Renderer renderer)
    {
        if (renderer == null) return Color.black;

        renderer.GetPropertyBlock(_propBlock);
        Color blockEmission = _propBlock.GetVector(EmissionColorId);
        if (blockEmission.maxColorComponent > 0.001f || blockEmission.a > 0.001f)
        {
            return blockEmission;
        }

        if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty(EmissionColorId))
        {
            return renderer.sharedMaterial.GetColor(EmissionColorId);
        }

        return Color.black;
    }
}
