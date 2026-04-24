using UnityEngine;

namespace AICPack;

public class SpriteFrameAnimator : MonoBehaviour
{
    [Header("Renderers")]
    public SpriteRenderer bodyMainRenderer;
    public SpriteRenderer bodyAuraRenderer;
    public SpriteRenderer eyeMainRenderer;

    [Header("Frames")]
    public Sprite[] bodyFrames; // 身体序列帧
    public Sprite[] eyeFrames;  // 眼睛序列帧 (需与身体帧数对齐)

    [Header("Settings")]
    public float fps = 12f;     // 帧率
    public bool loop = true;    // 是否循环

    private float timer = 0f;
    private int currentFrame = 0;

    void Start()
    {
        // 初始化第一帧
        this.UpdateSprites();
    }

    void Update()
    {
        if (this.bodyFrames == null || this.bodyFrames.Length == 0) return;

        this.timer += Time.deltaTime;
        float frameInterval = 1f / this.fps;

        // 达到切帧时间
        if (this.timer >= frameInterval)
        {
            this.timer -= frameInterval;
            this.currentFrame++;

            // 循环控制
            if (this.currentFrame >= this.bodyFrames.Length)
            {
                if (this.loop) 
                {
                    this.currentFrame = 0;
                }
                else 
                {
                    this.currentFrame = this.bodyFrames.Length - 1;
                }
            }

            this.UpdateSprites();
        }
    }

    // 更新所有图层的Sprite
    private void UpdateSprites()
    {
        if (this.bodyFrames != null && this.bodyFrames.Length > this.currentFrame)
        {
            Sprite currentBody = this.bodyFrames[this.currentFrame];
            
            // 本体和光环使用相同的身体图片
            if (this.bodyMainRenderer != null) this.bodyMainRenderer.sprite = currentBody;
            if (this.bodyAuraRenderer != null) this.bodyAuraRenderer.sprite = currentBody;
        }

        if (this.eyeFrames != null && this.eyeFrames.Length > this.currentFrame)
        {
            // 眼睛层单独替换
            if (this.eyeMainRenderer != null) this.eyeMainRenderer.sprite = this.eyeFrames[this.currentFrame];
        }
    }
}