using BepInEx;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using CustomizeLib;
using static MDPack.BlackHoleFix;

namespace MDPack;

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
[BepInDependency(CustomCore.PLUGIN_GUID, CustomCore.PLUGIN_VERSION)]
public class Core : BaseUnityPlugin
{
    public const string PLUGIN_GUID = "z7572.MDPack";
    public const string PLUGIN_NAME = "MDPack";
    public const string PLUGIN_VERSION = "2.0.0";

    internal static AssetBundle ab_railgun;
    internal static AssetBundle ab_blackhole;

    internal static AudioClip RechargingSfx;
    internal static AudioClip BlackHoleFadeSfx;
    internal static AudioClip BlackHoleBgm;

    public void Awake()
    {
        ab_railgun = CustomUtils.GetAssetBundle(Assembly.GetExecutingAssembly(), "sickashellrailgun");
        ab_blackhole = CustomUtils.GetAssetBundle(Assembly.GetExecutingAssembly(), "nullblackhole");

        RechargingSfx = ab_railgun.LoadAsset<AudioClip>("RECHARGING");
        BlackHoleBgm = ab_blackhole.LoadAsset<AudioClip>("heh, nothing personal kid");
        BlackHoleFadeSfx = ab_blackhole.LoadAsset<AudioClip>("blackhole fade");

        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        CodeTextManager.InitTextAndFont("CodeToShow.cs");

        PrefabManager.OnPrefabsLoaded += ReplaceLogic;
    }

    [HarmonyPatch]
    public class Patches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(WeaponPickUp), "Awake")]
        public static void WeaponPickUpAwakePostfix(WeaponPickUp __instance)
        {
            if (__instance.IsGroundWeapon)
            {
                if (__instance.gameObject.name == "Gun39")
                {
                    CustomUtils.ReplaceWeaponCollider(__instance.gameObject, ab_railgun.LoadAsset<GameObject>("Gun39"));
                }
            }
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(Fighting), "NetworkThrowWeapon")]
        [HarmonyPatch(typeof(Fighting), "ThrowWeapon")]
        public static IEnumerable<CodeInstruction> ThrowWeaponTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            for (int i = 0; i < codes.Count; i++)
            {
                // Find dropped and thrown weapon beam
                if (codes[i].opcode == OpCodes.Call && codes[i].operand.ToString().Contains("Instantiate") && codes[i + 1].opcode != OpCodes.Dup)
                {
                    // gameObject = Instantiate(...)
                    // SomeMethod(gameObject)
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Dup)); // Copy gameObject
                    codes.Insert(i + 2, new CodeInstruction(OpCodes.Ldarg_0)); // Fighting instance
                    codes.Insert(i + 3, new CodeInstruction(OpCodes.Ldarg_1)); // bool justDrop
                    codes.Insert(i + 4, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Core), nameof(OnBeamThrow))));
                    continue;
                }
            }
            return codes;
        }

        // Maybe we should patch typeof(BlackHole) and check this component by frame? idk which is better
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ShrinkOverTime), "Start")]
        public static void ShrinkOverTimeStartPrefix(ShrinkOverTime __instance)
        {
            if (__instance.gameObject.GetComponent<DoGlitchWhenShrink>() != null)
            {
                __instance.enabled = false;
                __instance.gameObject.AddComponent<BlackHoleFadeAnim>();
            }
        }
    }

    private static void OnBeamThrow(GameObject gameObject, Fighting fighting, bool justDrop)
    {
        if (!gameObject.name.Contains("39")) return;

        List<MeshRenderer> renderers = [];
        foreach (var renderer in gameObject.GetComponentsInChildren<MeshRenderer>())
        {
            if (renderer == null) continue;
            renderers.Add(renderer);
            if (renderer.GetMaterialColor().Equals(Color.green))
            {
                renderer.SetMaterialColor(Color.red, Color.red);
            }
        }
        if (!justDrop)
        {
            var part = gameObject.GetComponentInChildren<ParticleSystem>();
            var au = gameObject.AddComponent<AudioSource>();
            if (part == null || au == null) return;

            part.Play();
            au.PlayOneShot(RechargingSfx);
            Destroy(au, RechargingSfx.length);
            fighting.StartCoroutine(Recharging());

            IEnumerator Recharging()
            {
                while (part != null && gameObject != null && part.IsAlive())
                {
                    yield return null;
                }
                if (gameObject == null) yield break;

                foreach (var renderer in renderers)
                {
                    if (renderer == null || !renderer.GetMaterialColor().Equals(Color.red)) continue;
                    renderer.SetMaterialColor(Color.green, Color.green);
                }
            }
        }
    }

    // Render Queue:
    // 3000: Default
    // 3001: Bullet Lens
    // 3002: Bullet Particles, Bullet OuterRing
    // 3003: BlackHole Lens, Suck Particle
    // 3004: BlackHole Particles, Hole, OuterRing, NULL, CodeText
    private static void ReplaceLogic()
    {
        //var allAudioSources = Resources.FindObjectsOfTypeAll<AudioSource>().ToList();
        //var allClips = Resources.FindObjectsOfTypeAll<AudioClip>().ToList();
        foreach (var oriPrefab in PrefabManager.AllPrefabs)
        {
            if (oriPrefab.name == "39 Beam")
            {
                CustomUtils.ReplaceWeaponCollider(oriPrefab, ab_railgun.LoadAsset<GameObject>("39 Beam"));
                foreach (Transform child in oriPrefab.transform)
                {
                    foreach (var part in child.GetComponentsInChildren<ParticleSystemRenderer>())
                    {
                        part.material = ab_railgun.LoadAsset<Material>("GreenGlow2");
                    }
                }
                var weapon = oriPrefab.GetComponent<Weapon>();
                weapon.projectile.GetComponent<TimeEvent>().enabled = false;
                weapon.clips[0] = ab_railgun.LoadAsset<AudioClip>("lava laser");
                foreach (Transform child in weapon.projectile.transform)
                {
                    foreach (var part in child.GetComponentsInChildren<ParticleSystemRenderer>())
                    {
                        part.material = ab_railgun.LoadAsset<Material>("GreenGlow2");
                    }
                }
            }
            if (oriPrefab.name == "Gun39")
            {
                var newPrefab = ab_railgun.LoadAsset<GameObject>("Gun39");
                CustomUtils.ReplaceWeaponCollider(oriPrefab, newPrefab);
                foreach (var part in newPrefab.GetComponentsInChildren<ParticleSystem>())
                {
                    GameObject newChild = Instantiate(part.gameObject, oriPrefab.transform);
                }
            }
            if (oriPrefab.name == "41 Black Hole")
            {
                var weapon = oriPrefab.GetComponent<Weapon>();
                var oriProjectile = weapon.projectile;
                var abProjectile = ab_blackhole.LoadAsset<GameObject>("BulletBlackHole");

                var ringObj = Instantiate(abProjectile.transform.Find("OuterRing"), oriProjectile.transform);
                var lensObj = Instantiate(abProjectile.transform.Find("Lens"), oriProjectile.transform);

                lensObj.GetComponent<MeshRenderer>().ModifyMaterialSafely(mat => mat.renderQueue = 3001);
                ringObj.GetComponent<ParticleSystemRenderer>().ModifyMaterialSafely(mat => mat.renderQueue = 3002);
                foreach (var part in oriProjectile.GetComponentsInChildren<ParticleSystemRenderer>())
                {
                    if (part == ringObj.GetComponent<ParticleSystemRenderer>()) continue;
                    part.ModifyMaterialSafely(mat => mat.renderQueue = 3002);
                }

                lensObj.gameObject.AddComponent<BulletLensController>();
            }
            if (oriPrefab.name == "BlackHole")
            {
                oriPrefab.GetComponent<AudioSource>().clip = BlackHoleBgm;
                var abPrefab = ab_blackhole.LoadAsset<GameObject>("BlackHole");

                var oriHole = oriPrefab.transform.Find("Hole");
                var abHole = abPrefab.transform.Find("Hole");

                var partSuck = oriPrefab.transform.Find("Particle System (1)").GetComponent<ParticleSystemRenderer>();
                if (partSuck != null) partSuck.ModifyMaterialSafely(mat => mat.renderQueue = 3003);

                var holeRenderer = oriHole.GetComponent<SpriteRenderer>();
                holeRenderer.ModifyMaterialSafely(mat => mat.renderQueue = 3004);

                oriHole.Find("Particle System (1)").gameObject.SetActive(false); // Balls that blocks the outer ring
                var part = oriHole.transform.Find("Particle System").GetComponent<ParticleSystemRenderer>();
                if (part != null && part.enabled) part.ModifyMaterialSafely(mat => mat.renderQueue = 3004);

                var abLens = abPrefab.transform.Find("Lens");
                if (abLens != null)
                {
                    var lensObj = Instantiate(abLens.gameObject, oriPrefab.transform);
                    lensObj.transform.localScale = Vector3.one;
                    lensObj.GetComponent<MeshRenderer>().ModifyMaterialSafely(mat => mat.renderQueue = 3003);

                    var lens = lensObj.AddComponent<BlackHoleLensController>();
                    lens.holeSprite = holeRenderer;
                }

                var newObj1 = Instantiate(abHole.Find("OuterRing"), oriHole);
                var newObj2 = Instantiate(abHole.Find("NULL"), oriHole);

                newObj1.GetComponent<ParticleSystemRenderer>().ModifyMaterialSafely(mat => mat.renderQueue = 3004);
                newObj2.GetComponent<ParticleSystemRenderer>().ModifyMaterialSafely(mat => mat.renderQueue = 3004);

                // Original prefab will not be destroyed, so we need to check if already has the component
                if (oriPrefab.GetComponent<DoGlitchWhenShrink>() == null)
                    oriPrefab.gameObject.AddComponent<DoGlitchWhenShrink>();
                if (oriPrefab.GetComponent<CodeTextManager.SpawnCodes>() == null)
                    oriPrefab.gameObject.AddComponent<CodeTextManager.SpawnCodes>();

            }
        }
    }
}