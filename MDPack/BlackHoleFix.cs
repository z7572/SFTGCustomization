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
    internal class BlackHoleAnim : MonoBehaviour
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

    internal class BlackHoleFade : MonoBehaviour;

    internal class BlackHoleFadeAnim : MonoBehaviour
    {
        private static AudioSource au;
        private static readonly object _lock = new();
        void Awake()
        {
            var codeAnim = transform.root.GetComponentInChildren<CodeAnimation>();
            codeAnim?.StopAllCoroutines();
        }
        void Start()
        {
            StartCoroutine(Blink());
            StartCoroutine(Glitch());
            if (au == null)
            {
                lock (_lock)
                {
                    if (au == null)
                    {
                        au = gameObject.AddComponent<AudioSource>();
                        au.PlayOneShot(Core.BlackHoleFadeSfx);
                        transform.root.GetComponent<AudioSource>().Stop();
                        Destroy(au, Core.BlackHoleFadeSfx.length);
                    }
                }
            }
        }

        IEnumerator Blink()
        {
            var RingRed = new Color(1f, 0.4f, 0.4f, 1f);
            var RingYellow = new Color(1f, 0.878f, 0.421f, 1f);

            var targetRenderer = GetComponent<ParticleSystemRenderer>();
            var TextBaseColor = Color.white;
            var TextEmissionRed = new Color(1f, 0f, 0f, 1f);
            var TextEmissionYellow = new Color(1f, 0.883f, 0.057f, 1f);

            if (gameObject.name.StartsWith("OuterRing"))
            {
                var particleSystem = GetComponent<ParticleSystem>();
                if (particleSystem == null)
                {
                    yield break;
                }

                var main = particleSystem.main;
                var emission = particleSystem.emission;
                var particles = new ParticleSystem.Particle[particleSystem.main.maxParticles];

                main.loop = true;
                emission.enabled = true;

                int blinkCount = 0;

                while (blinkCount < 5)
                {
                    var targetColor = blinkCount % 2 == 0 ? RingRed : RingYellow;

                    main.startColor = targetColor;

                    int aliveParticleCount = particleSystem.GetParticles(particles);
                    for (int i = 0; i < aliveParticleCount; i++)
                    {
                        particles[i].startColor = targetColor;
                    }
                    particleSystem.SetParticles(particles, aliveParticleCount);

                    blinkCount++;
                    yield return new WaitForSeconds(0.05f);
                }
            }
            else if (gameObject.name.StartsWith("NULL"))
            {
                int blinkCount = 0;

                while (blinkCount < 5)
                {
                    var emissionColor = blinkCount % 2 == 0 ? TextEmissionRed : TextEmissionYellow;
                    targetRenderer.SetMaterialColor(TextBaseColor, emissionColor);
                    blinkCount++;

                    yield return new WaitForSeconds(0.05f);
                }
            }

            yield return new WaitForSeconds(1f);
            Destroy(gameObject);
        }

        // TODO: Add a glitch effect to the shader (too hard)
        IEnumerator Glitch()
        {
            yield return new WaitForSeconds(0.2f);
            // Implement glitch effect here

        }
    }
}
