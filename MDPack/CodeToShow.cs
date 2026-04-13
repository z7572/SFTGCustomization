using BepInEx;
using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Reflection.Emit;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using LevelEditor;
using Random = UnityEngine.Random;

namespace CustomizeLib;

[BepInPlugin("z7572.sftgcustomization", "SFTGCustomization", "1.3.0")]
public class CustomizeCore : BaseUnityPlugin
{
    public static AssetBundle ab_railgun;
    public static AssetBundle ab_blackhole;
    public static List<GameObject> allPrefabs = new();

    static AudioClip RechargingSound;
    static AudioClip BlackHoleSound;

    public void Awake()
    {
        ab_railgun = CustomUtils.GetAssetBundle(Assembly.GetExecutingAssembly(), "sickashellrailgun");
        ab_blackhole = CustomUtils.GetAssetBundle(Assembly.GetExecutingAssembly(), "nullblackhole");

        RechargingSound = ab_railgun.LoadAsset<AudioClip>("RECHARGING");
        BlackHoleSound = ab_blackhole.LoadAsset<AudioClip>("heh, nothing personal kid");

        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        CodeText.InitTextAndFont("ExampleCode.cs");
        Testing.TextureStore.Init();
    }

    [HarmonyPatch]
    public class Patches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameManager), "Start")]
        public static void GameManagerStartPostfix()
        {
            allPrefabs = Resources.FindObjectsOfTypeAll<GameObject>().Where(obj => obj.scene.rootCount == 0).ToList();
            ReplaceLogic();
        }

        [HarmonyPatch(typeof(Controller), "Start")]
        [HarmonyPostfix]
        public static void ControllerStartPostfix(Controller __instance)
        {
            if (__instance.HasControl && !__instance.isAI)
            {
                CustomUtils.controller = __instance;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(LevelCreator), "Start")]
        public static void LevelCreatorStartPostfix()
        {
            allPrefabs = Resources.FindObjectsOfTypeAll<GameObject>().Where(obj => obj.scene.rootCount == 0).ToList();
            ReplaceLogic();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WeaponPickUp), "Awake")]
        public static void WeaponPickUpAwakePostfix(WeaponPickUp __instance)
        {
            if (__instance.IsGroundWeapon)
            {
                if (__instance.gameObject.name == "Gun39")
                {
                    ReplaceWeaponCollider(__instance.gameObject, ab_railgun.LoadAsset<GameObject>("Gun39"));
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
                if (codes[i].opcode == OpCodes.Call && codes[i].operand.ToString().Contains("Instantiate") && codes[i + 1].opcode != OpCodes.Dup)
                {
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Dup));
                    codes.Insert(i + 2, new CodeInstruction(OpCodes.Ldarg_0));
                    codes.Insert(i + 3, new CodeInstruction(OpCodes.Ldarg_1));
                    codes.Insert(i + 4, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CustomCore), nameof(OnBeamThrow))));
                    continue;
                }
            }
            return codes;
        }

        [Obsolete]
        public static IEnumerable<CodeInstruction> EnableSnakeBombParticleTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Call && codes[i].operand.ToString().Contains("Destroy"))
                {
                    codes.RemoveRange(i - 2, 3);
                    break;
                }
            }
            return codes;
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
            if (renderer.material.color.Equals(Color.green))
            {
                renderer.SetMaterialColor(Color.red);
            }
        }
        if (!justDrop)
        {
            var part = gameObject.GetComponentInChildren<ParticleSystem>();
            var au = gameObject.AddComponent<AudioSource>();
            if (part == null || au == null) return;

            part.Play();
            au.PlayOneShot(RechargingSound);
            Destroy(au, RechargingSound.length);
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
                    if (renderer == null || !renderer.material.color.Equals(Color.red)) continue;
                    renderer.SetMaterialColor(Color.green);
                }
            }
        }
    }

    private static void ReplaceLogic()
    {
        foreach (var oriPrefab in allPrefabs)
        {
            if (oriPrefab.name == "39 Beam")
            {
                ReplaceWeaponCollider(oriPrefab, ab_railgun.LoadAsset<GameObject>("39 Beam"));
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
                ReplaceWeaponCollider(oriPrefab, newPrefab);
                foreach (var part in newPrefab.GetComponentsInChildren<ParticleSystem>())
                {
                    GameObject newChild = Instantiate(part.gameObject, oriPrefab.transform);
                }
            }
            if (oriPrefab.name == "41 Black Hole")
            {
                var newPrefab = ab_blackhole.LoadAsset<GameObject>("BulletBlackHole").transform;
                var weapon = oriPrefab.GetComponent<Weapon>();
                var newObj1 = Instantiate(newPrefab.Find("OuterRing"), weapon.projectile.transform);


            }
            if (oriPrefab.name == "BlackHole")
            {
                oriPrefab.GetComponent<AudioSource>().clip = BlackHoleSound;
                var newPrefab = ab_blackhole.LoadAsset<GameObject>("BlackHole").transform.Find("Hole");
                var child = oriPrefab.transform.Find("Hole");
                child.Find("Particle System (1)").gameObject.SetActive(false);
                var newObj1 = Instantiate(newPrefab.Find("OuterRing"), child);
                var newObj2 = Instantiate(newPrefab.Find("NULL"), child);
                var anim1 = newObj1.gameObject.AddComponent<BlackHoleAnim>();
                var anim2 = newObj2.gameObject.AddComponent<BlackHoleAnim>();
                anim1.target = child;
                anim2.target = child;
                newObj1.gameObject.AddComponent<RemoveOnLevelChange>();
                newObj2.gameObject.AddComponent<RemoveOnLevelChange>();
                var codeAnim = child.transform.parent.gameObject.AddComponent<CodeText.SpawnCodes>();

            }
        }
    }

    public static void ReplaceWeaponCollider(GameObject oriObj, GameObject newObj, string replaceObjName = "collider")
    {
        foreach (Transform child in oriObj.transform)
        {
            if (!child.name.ToLower().Contains(replaceObjName.ToLower())) continue;

            child.GetComponent<MeshRenderer>().enabled = false;
        }
        foreach (Transform child in newObj.transform)
        {
            if (!child.name.ToLower().Contains(replaceObjName.ToLower())) continue;

            GameObject newChild = Instantiate(child.gameObject, oriObj.transform);
            newChild.name = child.name;
            Destroy(newChild.GetComponent<BoxCollider>());
        }
    }

    class BlackHoleAnim : MonoBehaviour
    {
        public Transform target;
        public float updateInterval = 0f;
        public float multiplier = 1f;
        private float lastUpdateTime;
        void Update()
        {
            if (updateInterval <= 0 || Time.time - lastUpdateTime >= updateInterval)
            {
                SyncScale();
                lastUpdateTime = Time.time;
            }
        }
        public void SyncScale()
        {
            if (!target) return;
            Vector3 newScale = target.localScale;
            transform.localScale = newScale * multiplier;
        }
    }
}