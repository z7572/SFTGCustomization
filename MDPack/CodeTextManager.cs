using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;
using CustomizeLib;

namespace MDPack;

public class CodeTextManager
{
    private static Font font;
    private static string fullCodeText = "";

    public static void InitTextAndFont(string name)
    {
        try
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using Stream stream = assembly.GetManifestResourceStream(assembly.FullName!.Split(',')[0] + "." + name) ?? assembly.GetManifestResourceStream(name)!;
            {
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    fullCodeText = reader.ReadToEnd();
                }
            }
            font = Font.CreateDynamicFontFromOSFont("Consolas", 14);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error loading font: {e.Message}");
            return;
        }
    }

    public class SpawnCodes : MonoBehaviour
    {
        private float SizeMultiplier => Random.Range(0.8f, 1.25f);
        private float RandomDelay => Random.Range(0.1f, 0.4f);

        private Coroutine spawnCoroutine;
        private bool isSpawning = false;

        void Start()
        {
            if (!isSpawning)
            {
                spawnCoroutine = StartCoroutine(SpawnCodesRoutine(transform));
            }
        }

        void OnDisable()
        {
            StopAllSpawning();
        }

        void OnDestroy()
        {
            StopAllSpawning();
        }

        private void StopAllSpawning()
        {
            isSpawning = false;
            if (spawnCoroutine != null)
            {
                StopCoroutine(spawnCoroutine);
                spawnCoroutine = null;
            }

            var allAnimations = GetComponentsInChildren<CodeFlashAnim>();
            foreach (var anim in allAnimations)
            {
                if (anim != null && anim.gameObject != null)
                {
                    anim.StopAllCoroutines();
                    Destroy(anim.gameObject);
                }
            }
        }
        IEnumerator SpawnCodesRoutine(Transform target)
        {
            if (isSpawning) yield break;

            isSpawning = true;
            var count = Random.Range(2, 6);

            for (int i = 0; i < count; i++)
            {
                if (!isSpawning || !isActiveAndEnabled)
                    yield break;

                var randomOffset = new Vector3(Random.Range(-300f, 300f), Random.Range(-300f, 300f), 0f);
                while (Vector3.Distance(randomOffset, Vector3.zero) < 100f)
                {
                    randomOffset = new Vector3(Random.Range(-300f, 300f), Random.Range(-300f, 300f), 0f);
                }
                CreateTextOnObject(target, randomOffset);

                yield return new WaitForSeconds(RandomDelay);
            }
            isSpawning = false;
        }

        private Text CreateTextOnObject(Transform target, Vector3 localOffset = default)
        {
            var canvasObj = target.GetComponentInChildren<Canvas>()?.gameObject;
            if (canvasObj == null)
            {
                canvasObj = Instantiate(CustomUtils.controller.transform.Find("GameCanvas").gameObject, target);
                Destroy(canvasObj.GetComponent<OrigoMeBro>());
                for (var i = 0; i < canvasObj.transform.childCount; i++)
                {
                    Destroy(canvasObj.transform.GetChild(i).gameObject);
                }
                var pos = target.Find("Hole").position;
                canvasObj.transform.position = new Vector3(pos.x - 0.1f, pos.y, pos.z);
            }

            var textObjName = $"CodeText_{Time.frameCount}{Random.Range(0, 1001)}";
            GameObject textObj = new GameObject(textObjName);
            textObj.transform.SetParent(canvasObj.transform);
            textObj.transform.localPosition = localOffset;
            textObj.transform.localRotation = Quaternion.identity;
            textObj.transform.localScale = Vector3.one * 0.2f;

            textObj.AddComponent<CodeFlashAnim>();
            var text = textObj.AddComponent<Text>();
            SetupTextProperties(text, SizeMultiplier);
            text.ModifyMaterialSafely(mat => mat.renderQueue = 3004);

            return text;
        }
    }

    private static void SetupTextProperties(Text text, float sizeMultiper)
    {
        text.font = font;
        text.fontSize = 50;
        text.fontStyle = FontStyle.Bold;
        text.lineSpacing = 0.8f;
        text.alignment = TextAnchor.LowerLeft;
        text.color = new Color(1f, 1f, 0.6f);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.supportRichText = false;

        RectTransform rt = text.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(Screen.width, Screen.height) * sizeMultiper;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
    }

    class CodeFlashAnim : MonoBehaviour
    {
        public Text textComponent;
        private string fullCodeText;
        private Coroutine coroutine;

        private float SpeedMultiplier => Random.Range(2f, 4f);
        private int maxDisplayLines = 25;
        private int charsPerChunk = 35;
        private float targetFrameDuration = 1f / 60f;

        void Start()
        {
            textComponent = GetComponent<Text>();
            fullCodeText = CodeTextManager.fullCodeText;
            coroutine = StartCoroutine(ScrollCodeInChunksRoutine());
        }

        void OnDisable()
        {
            if (coroutine != null) StopCoroutine(coroutine);
        }

        void OnDestroy()
        {
            if (coroutine != null) StopCoroutine(coroutine);
        }

        private IEnumerator ScrollCodeInChunksRoutine()
        {
            var currentCodeText = "";
            var currentCharIndex = 0;
            textComponent.text = "";
            var charsPerChunk = (int)(this.charsPerChunk * SpeedMultiplier);
            var dynamicChunkDelay = targetFrameDuration / SpeedMultiplier;

            while (currentCharIndex < fullCodeText.Length && this != null && isActiveAndEnabled)
            {
                if (textComponent == null) yield break;

                currentCodeText += fullCodeText.Substring(currentCharIndex, Math.Min(charsPerChunk, fullCodeText.Length - currentCharIndex));
                currentCharIndex += charsPerChunk;

                string[] lines = currentCodeText.Split('\n');
                if (lines.Length > maxDisplayLines)
                {
                    textComponent.text = string.Join("\n", lines, lines.Length - maxDisplayLines, maxDisplayLines);
                }
                else
                {
                    textComponent.text = currentCodeText;
                }

                yield return new WaitForSeconds(dynamicChunkDelay);
            }

            if (this != null && isActiveAndEnabled)
            {
                yield return new WaitForSeconds(0.5f);
                if (this != null && gameObject != null)
                    gameObject.SetActive(false);
            }
        }
    }
}
