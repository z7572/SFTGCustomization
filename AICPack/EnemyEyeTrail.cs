using UnityEngine;
using System.Collections.Generic;

namespace AICPack;
public class EnemyEyeTrail : MonoBehaviour
{
    [System.Serializable]
    public class TrailNode
    {
        public SpriteRenderer renderer;
        public float spawnTime;
    }

    public SpriteRenderer mainEyeRenderer;  
    public Transform trailGroupParent;      
    public Material trailMaterial;          
    
    public int maxTrails = 6;               
    public float recordInterval = 0.05f;    
    public float trailLifeTime = 0.3f;      

    private List<TrailNode> activeTrails = new List<TrailNode>();
    private Queue<SpriteRenderer> trailPool = new Queue<SpriteRenderer>();
    private float timer = 0f;

    void Start()
    {
        // 将拖尾容器脱离敌人父节点，留在世界空间中
        // 这样敌人的移动就不会再拖拽这些残影了。
        if (trailGroupParent != null)
        {
            trailGroupParent.SetParent(null);
        }

        // 初始化对象池
        for (int i = 0; i < maxTrails; i++)
        {
            GameObject trailObj = new GameObject("EyeTrail_" + i);
            trailObj.transform.SetParent(trailGroupParent);
            SpriteRenderer sr = trailObj.AddComponent<SpriteRenderer>();
            sr.material = trailMaterial;
            sr.sortingOrder = 2;
            trailObj.SetActive(false);
            trailPool.Enqueue(sr);
        }
    }

    void Update()
    {
        timer += Time.deltaTime;
        
        // 定期记录眼睛轨迹
        if (timer >= recordInterval && trailPool.Count > 0)
        {
            timer = 0f;
            SpawnTrail();
        }

        // 更新残影透明度与回收
        UpdateTrails();
    }

    // 当挂载该脚本的敌人本体被销毁时，顺手把世界空间里的拖尾组也销毁掉
    void OnDestroy()
    {
        if (trailGroupParent != null)
        {
            Destroy(trailGroupParent.gameObject);
        }
    }

    void SpawnTrail()
    {
        if (mainEyeRenderer.sprite == null) return;

        SpriteRenderer trail = trailPool.Dequeue();
        trail.gameObject.SetActive(true);

        // 拷贝当前眼睛的状态
        trail.sprite = mainEyeRenderer.sprite;
        trail.transform.position = mainEyeRenderer.transform.position;
        trail.transform.rotation = mainEyeRenderer.transform.rotation;
        trail.transform.localScale = mainEyeRenderer.transform.localScale;

        // 初始颜色设为纯白（带点透明度防止过曝）
        trail.color = new Color(1f, 0.5f, 0.5f, 0.8f);

        activeTrails.Add(new TrailNode { renderer = trail, spawnTime = Time.time });
    }

    void UpdateTrails()
    {
        for (int i = activeTrails.Count - 1; i >= 0; i--)
        {
            TrailNode node = activeTrails[i];
            float age = Time.time - node.spawnTime;

            if (age >= trailLifeTime)
            {
                // 回收
                node.renderer.gameObject.SetActive(false);
                trailPool.Enqueue(node.renderer);
                activeTrails.RemoveAt(i);
            }
            else
            {
                // 计算生命周期的总进度 (0 -> 1)
                float progress = age / trailLifeTime;

                // 颜色渐变进度：乘以2并截断。前半段(0~0.5)会映射为0~1，后半段(0.5~1)会保持为1
                float colorProgress = Mathf.Clamp01(progress * 2f);

                // 颜色从白色 (White) 渐变到红色 (Red)，前半段完成渐变，后半段保持纯红
                Color currentColor = Color.Lerp(Color.white, Color.red, colorProgress);

                // 透明度仍然按总进度平滑衰减 (1 -> 0)
                float alpha = 1f - progress;
                // 结合初始设置的 0.8f 基础透明度
                currentColor.a = alpha * 0.8f;

                // 应用最终的颜色和透明度
                node.renderer.color = currentColor;
            }
        }
    }
}