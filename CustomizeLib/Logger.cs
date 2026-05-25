global using static CustomizeLib.Logger;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace CustomizeLib;

public static class Logger
{
    public static void LogInfo(params object[] obj)
    {
        CustomCore.Logger.LogInfo(string.Join(" ", obj.Select(o => o?.ToString() ?? "null").ToArray()));
    }

    public static void LogMessage(params object[] obj)
    {
        CustomCore.Logger.LogMessage(string.Join(" ", obj.Select(o => o?.ToString() ?? "null").ToArray()));
    }

    public static void LogWarning(params object[] obj)
    {
        CustomCore.Logger.LogWarning(string.Join(" ", obj.Select(o => o?.ToString() ?? "null").ToArray()));
    }

    public static void LogError(params object[] obj)
    {
        CustomCore.Logger.LogError(string.Join(" ", obj.Select(o => o?.ToString() ?? "null").ToArray()));
    }

    public static void LogDebug(params object[] obj)
    {
        CustomCore.Logger.LogDebug(GetPrefix() + string.Join(" ", obj.Select(o => o?.ToString() ?? "null").ToArray()));
    }

    private static string GetPrefix()
    {
        var callerMethod = new StackFrame(2).GetMethod();
        var callerType = callerMethod?.DeclaringType;
        if (callerType is not null)
        {
            // string ns = callerType.Namespace??"";
            // int i = ns.LastIndexOf('.');
            // string substr = ns.Substring(i >= 0 ? i : 0);
            return $"[{callerType.Name}.{callerMethod.Name}] ";
        }

        return "[Unknown method] ";
    }
}