using BepInEx;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.SceneManagement;
using CustomizeLib;

namespace AICPack;

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
[BepInDependency(CustomCore.PLUGIN_GUID, CustomCore.PLUGIN_VERSION)]
public class Core : BaseUnityPlugin
{
    public const string PLUGIN_GUID = "z7572.AICPack";
    public const string PLUGIN_NAME = "AICPack";
    public const string PLUGIN_VERSION = "1.0.0";

    internal static AssetBundle ab_uni;

    public void Awake()
    {
        ab_uni = CustomUtils.GetAssetBundle(Assembly.GetExecutingAssembly(), "urchincontaminated");

        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (scene.name == "Castle14" && mode == LoadSceneMode.Additive)
            {
                ReplaceLogic(scene);
            }
        };
    }

    [HarmonyPatch]
    public class Patches
    {

    }

    private static void ReplaceLogic(Scene targetScene)
    {
        var map21 = targetScene.GetRootGameObjects().FirstOrDefault(obj => obj.name == "Map21");

        var scaryBall = map21?.transform.Find("ScaryBall")?.gameObject;

        if (scaryBall != null)
        {
            foreach (var renderer in scaryBall.GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = false;
            }

            var uni = ab_uni.LoadAsset<GameObject>("Uni");
            var uniObj = Instantiate(uni);
            SceneManager.MoveGameObjectToScene(uniObj, targetScene);

            uniObj.transform.localScale = Vector3.one * 11f;

            var followTransform = uniObj.gameObject.AddComponent<FollowTransform>();
            followTransform.target = scaryBall.transform;

            var trail = uniObj.gameObject.AddComponent<EnemyEyeTrail>();
            var animator = uniObj.gameObject.AddComponent<SpriteFrameAnimator>();

            trail.mainEyeRenderer = uniObj.transform.Find("Eye_Main").GetComponent<SpriteRenderer>();
            trail.trailGroupParent = uniObj.transform.Find("Uni_Eye_Trail");
            trail.trailMaterial = ab_uni.LoadAsset<Material>("Mat_Eye");

            animator.bodyMainRenderer = uniObj.transform.Find("Body_Main").GetComponent<SpriteRenderer>();
            animator.bodyAuraRenderer = uniObj.transform.Find("Body_Aura").GetComponent<SpriteRenderer>();
            animator.eyeMainRenderer = uniObj.transform.Find("Eye_Main").GetComponent<SpriteRenderer>();

            for (var i = 0; i <= 10; i++)
            {
                animator.bodyFrames = animator.bodyFrames.AddToArray(ab_uni.LoadAsset<Sprite>($"{i}_Layer"));
                animator.eyeFrames = animator.eyeFrames.AddToArray(ab_uni.LoadAsset<Sprite>($"{i}_combined"));
            }

            var groundSfx = scaryBall.AddComponent<GroundCollisionSfx>();
            groundSfx.collisionSfx = ab_uni.LoadAsset<AudioClip>("uni_in_ground");
            groundSfx.audioSource = scaryBall.GetComponent<AudioSource>();
            if (groundSfx.audioSource == null)
            {
                groundSfx.audioSource = scaryBall.AddComponent<AudioSource>();

                groundSfx.audioSource.spatialBlend = 1f; // 让音效具有 3D 空间感
                groundSfx.audioSource.volume = 0.5f;
            }

            var aura = uniObj.transform.Find("Body_Aura").gameObject;
            var jitter = aura.gameObject.AddComponent<EnemyAuraJitter>();

        }
    }
}