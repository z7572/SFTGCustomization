using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CustomizeLib;
using UnityEngine;

namespace MDPack;

public static class BlackHoleFix
{
    internal class DoGlitchWhenShrink : MonoBehaviour;

    internal class BlackHoleFadeAnim : MonoBehaviour
    {
        private Camera _camera;
        private static AudioSource au;
        private static readonly object _lock = new();

        void Awake()
        {
            var codeAnim = GetComponent<CodeAnimation>();
            if (codeAnim != null) codeAnim.StopAllCoroutines();
        }

        void Start()
        {
            _camera = Camera.main;

            // 播放音效
            if (au == null)
            {
                lock (_lock)
                {
                    if (au == null)
                    {
                        au = gameObject.AddComponent<AudioSource>();
                        au.PlayOneShot(Core.BlackHoleFadeSfx);
                        var rootAudio = GetComponent<AudioSource>();
                        if (rootAudio != null) rootAudio.Stop();
                        Destroy(au, Core.BlackHoleFadeSfx.length);
                    }
                }
            }

            StartCoroutine(DeathSequence());
        }

        IEnumerator DeathSequence()
        {
            int glitchLayer = 13;

            // 1. 设置图层 (递归查找所有子物体，直接一网打尽)
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (!child.name.StartsWith("Particle System (1)"))
                {
                    child.gameObject.layer = glitchLayer;
                }
            }

            yield return new WaitForSeconds(0.1f);

            // 2. 设置副相机和全屏画布
            GameObject camObj = new GameObject("GlitchCam");
            camObj.transform.SetParent(_camera.transform);
            camObj.transform.localPosition = Vector3.zero;
            camObj.transform.localRotation = Quaternion.identity;

            Camera glitchCam = camObj.AddComponent<Camera>();
            glitchCam.CopyFrom(_camera);
            glitchCam.cullingMask = 1 << glitchLayer;
            glitchCam.clearFlags = CameraClearFlags.SolidColor;
            glitchCam.backgroundColor = new Color(0, 0, 0, 0);

            var rt = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.DefaultHDR);
            glitchCam.targetTexture = rt;

            GameObject canvasObj = new GameObject("GlitchCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _camera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 32000;

            GameObject imgObj = new GameObject("GlitchImage");
            imgObj.transform.SetParent(canvasObj.transform, false);
            var rawImage = imgObj.AddComponent<UnityEngine.UI.RawImage>();
            rawImage.texture = rt;
            rawImage.rectTransform.anchorMin = Vector2.zero;
            rawImage.rectTransform.anchorMax = Vector2.one;
            rawImage.rectTransform.offsetMin = Vector2.zero;
            rawImage.rectTransform.offsetMax = Vector2.zero;

            var glitchShader = Core.ab_blackhole.LoadAsset<Shader>("Glitch");
            var glitchMat = new Material(glitchShader);
            rawImage.material = glitchMat;

            _camera.cullingMask &= ~(1 << glitchLayer);

            // 3. 执行闪烁 (根节点自己去找出 OuterRing 和 NULL)
            var RingRed = new Color(1f, 0.4f, 0.4f, 1f);
            var RingYellow = new Color(1f, 0.878f, 0.421f, 1f);
            var TextBaseColor = Color.white;
            var TextEmissionRed = new Color(1f, 0f, 0f, 1f);
            var TextEmissionYellow = new Color(1f, 0.883f, 0.057f, 1f);

            var outerRings = GetComponentsInChildren<ParticleSystem>(true).Where(p => p.gameObject.name.StartsWith("OuterRing")).ToList();
            var nullTexts = GetComponentsInChildren<ParticleSystemRenderer>(true).Where(p => p.gameObject.name.StartsWith("NULL")).ToList();

            for (int blinkCount = 0; blinkCount < 5; blinkCount++)
            {
                var ringColor = blinkCount % 2 == 0 ? RingRed : RingYellow;
                var emissionColor = blinkCount % 2 == 0 ? TextEmissionRed : TextEmissionYellow;

                foreach (var ps in outerRings)
                {
                    var main = ps.main;
                    main.startColor = ringColor;
                    var particles = new ParticleSystem.Particle[ps.main.maxParticles];
                    int count = ps.GetParticles(particles);
                    for (int i = 0; i < count; i++) particles[i].startColor = ringColor;
                    ps.SetParticles(particles, count);
                }

                foreach (var text in nullTexts)
                {
                    text.SetMaterialColor(TextBaseColor, emissionColor);
                }

                yield return new WaitForSeconds(0.05f);
            }

            // 5. 触发极速无限逼近缩小
            gameObject.AddComponent<FastShrink>();

            // 【核心修复：由音效决定寿命】
            // 整个协程在前面花费了时间：0.1s (图层切换等待) + 0.25s (5次闪烁) = 0.35s。
            // 我们用音效的总时长减去已经流逝的 0.35s，就是我们需要挂机等待的时间。
            float remainingAudioTime = Core.BlackHoleFadeSfx.length - 0.35f;

            // 兜底防御，以防某种极端情况下音效比前摇还短
            if (remainingAudioTime < 0f) remainingAudioTime = 0.1f;

            // 6. 耐心等待，让黑洞在这段时间里无限趋近于 0.8f 直径，同时把音效彻底放完
            yield return new WaitForSeconds(remainingAudioTime);

            // 7. 音效结束的瞬间，完美谢幕！
            Destroy(rt);
            Destroy(camObj);
            Destroy(canvasObj);
            if (_camera != null) _camera.cullingMask |= (1 << glitchLayer);

            Destroy(gameObject);
        }
    }

    public class BlackHoleLensController : MonoBehaviour
    {
        public SpriteRenderer holeSprite;
        public float SpriteToQuadRatio = 6.39f;

        // 【曲线定义宽度】：横坐标为秒，纵坐标为“超出黑洞边缘的物理米数”
        //public AnimationCurve widthCurve;

        public float distortionStrength = 0.2f; // 对应物理公式中的 _DistortionMass

        private MaterialPropertyBlock _propBlock;
        private Renderer _renderer;
        private float _startTime;

        void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _propBlock = new MaterialPropertyBlock();
            _startTime = Time.time;
        }

        void Update()
        {
            if (holeSprite == null) return;

            transform.position = holeSprite.transform.position;

            // 1. 获取黑洞当前的真实物理缩放倍率和世界半径
            float holeScale = holeSprite.transform.localScale.y;
            float holeWorldRadius = (holeScale * SpriteToQuadRatio) / 2f;

            float elapsed = Time.time - _startTime;
            float currentExtraWidth;

            // 【阶段 1】：前 0.5 秒，迅速炸开到 1.5f
            if (elapsed < 0.5f)
            {
                float t = elapsed / 0.5f;
                // 使用 smoothstep 平滑公式，让瞬间炸开更有张力和阻尼感
                currentExtraWidth = Mathf.Lerp(0f, 1.5f, t * t * (3f - 2f * t));
            }
            else
            {
                // 0.5 秒后，计算黑洞自然的引力透镜宽度 (基础宽 1.5 * 当前倍率)
                float calculatedWidth = holeScale * 1.5f;

                // 【阶段 2】：如果计算出的宽度还没追上 1.5f，强制保持 1.5f（绝不缩小！）
                if (calculatedWidth <= 1.5f)
                {
                    currentExtraWidth = 1.5f;
                }
                // 【阶段 3】：如果计算宽度追上了，就跟随真实物理放大，但最高不超过 5f 上限
                else if (calculatedWidth <= 5f)
                {
                    currentExtraWidth = calculatedWidth;
                }
                // 【阶段 4】：触碰 5f 上限，不再增加边缘宽度
                else
                {
                    currentExtraWidth = 5f;
                }
            }

            // 计算总尺寸并应用
            float totalWorldRadius = holeWorldRadius + currentExtraWidth;
            float totalQuadDiameter = totalWorldRadius * 2f;

            transform.localScale = new Vector3(totalQuadDiameter, totalQuadDiameter, 1f);

            // 反算 UV 传给 Shader
            float eventHorizonUV = (holeWorldRadius / totalWorldRadius) * 0.5f;

            // 1. 分别计算两种状态下的目标强度
            float distortionEarly = distortionStrength;
            float distortionLate = distortionStrength * (eventHorizonUV * eventHorizonUV) * 15f;

            // 2. 设定一个“平滑过渡区”（例如 1.0 到 2.0 之间）
            // 当 currentExtraWidth <= 1.0 时，blend = 0
            // 当 currentExtraWidth >= 2.0 时，blend = 1
            // 在 1.0 到 2.0 之间时，blend 是一个 0~1 的平滑曲线
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.0f, 4.0f, currentExtraWidth));

            // 3. 完美融合！根据 blend 比例在两个公式之间平滑过渡，彻底消除突变
            float dynamicDistortion = Mathf.Lerp(distortionEarly, distortionLate, blend);

            _renderer.GetPropertyBlock(_propBlock);
            _propBlock.SetFloat("_EventHorizon", eventHorizonUV);
            _propBlock.SetFloat("_DistortionMass", dynamicDistortion);
            _renderer.SetPropertyBlock(_propBlock);
        }
    }

    internal class FastShrink : MonoBehaviour
    {
        public float targetScale = 0.03125f;
        public float shrinkSpeed = 18f;

        void Start()
        {
            // 依然强制所有粒子跟随根节点缩放
            foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
        }

        void Update()
        {
            // 【核心魔法：无限逼近算法】
            // Lerp 结合 Time.deltaTime 会产生完美的指数衰减曲线 (先极快，后无限慢)，
            // 永远在靠近 targetScale，但不会硬生生地“撞”到底部停止。
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * targetScale, Time.deltaTime * shrinkSpeed);
        }
    }

    public class BulletLensController : MonoBehaviour
    {
        // 曲线直接控制面片的物理直径 (Diameter)
        // 比如：0.15秒内迅速放大到 1.5 米宽，然后保持不变
        public AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.15f, 2f),
            new Keyframe(10f, 2f)
        );

        public float distortionStrength = 0.05f; // 子弹的扭曲力度（比完全体黑洞小一点）
        public float eventHorizonUV = 0.1f;      // 子弹黑洞死区占面片的比例（设小一点比较好看）

        private MaterialPropertyBlock _propBlock;
        private Renderer _renderer;
        private float _startTime;

        void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _propBlock = new MaterialPropertyBlock();
            _startTime = Time.time;
        }

        void Update()
        {
            float elapsed = Time.time - _startTime;

            // 直接按曲线控制面片的绝对大小
            float currentSize = sizeCurve.Evaluate(elapsed);
            transform.localScale = new Vector3(currentSize, currentSize, 1f);

            // 确保永远锁定在子弹正中心
            transform.localPosition = Vector3.zero;

            // 给 Shader 传参
            _renderer.GetPropertyBlock(_propBlock);
            _propBlock.SetFloat("_EventHorizon", eventHorizonUV);
            _propBlock.SetFloat("_DistortionMass", distortionStrength);
            _renderer.SetPropertyBlock(_propBlock);
        }
    }
}
