using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using LevelEditor;
using UnityEngine;

namespace CustomizeLib;

[HarmonyPatch]
public class Patches
{
    [HarmonyPatch(typeof(Controller), "Start")]
    [HarmonyPostfix]
    public static void ControllerStartPostfix(Controller __instance)
    {
        if (__instance.HasControl && !__instance.IsAI())
        {
            CustomUtils.controller = __instance;
        }
    }

    // Enable obsolete snake bomb particle
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(SnakeSpawner), "Start")]
    public static IEnumerable<CodeInstruction> EnableSnakeBombParticleTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        if (!ConfigHandler.GetEntry<bool>("EnableObsoleteSnakeBombSmoke")) return codes;

        for (int i = 0; i < codes.Count; i++)
        {
            // Remove code: UnityEngine.Object.Destroy(base.gameObject);
            if (codes[i].opcode == OpCodes.Call && codes[i].operand.ToString().Contains("Destroy"))
            {
                codes.RemoveRange(i - 2, 3);
                break;
            }
        }
        return codes;
    }
}