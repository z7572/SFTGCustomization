using UnityEngine;

namespace AICPack;

public class EnemyAuraJitter : MonoBehaviour
{
    public float jitterRadius = 0.02f;      // 抖动半径
    public float scaleMultiplier = 1f;   // 放大倍数
    public float rotationJitter = 1f;       // 旋转抖动角度

    [Header("FPS Control")]
    public float jitterFps = 12f;           // 固定的抖动帧率（建议和你的动画帧率一致）

    private Vector3 baseLocalPos;
    private Quaternion baseLocalRot;

    private float timer = 0f;
    private Vector3 currentOffset;
    private Quaternion currentJitterRot;

    void Start()
    {
        // 记录初始状态
        baseLocalPos = transform.localPosition;
        baseLocalRot = transform.localRotation;

        // 放大一圈作为描边
        transform.localScale = Vector3.one * scaleMultiplier;

        // 赋予初始偏移
        CalculateNewJitter();
    }

    void LateUpdate()
    {
        timer += Time.deltaTime;
        float interval = 1f / jitterFps;

        // 只有达到设定的时间间隔，才计算一次新的抖动坐标
        if (timer >= interval)
        {
            // 扣除时间（保留余数使得计时更精准）
            timer -= interval;
            CalculateNewJitter();
        }

        // 每帧都应用当前记录的偏移值
        transform.localPosition = baseLocalPos + currentOffset;
        transform.localRotation = baseLocalRot * currentJitterRot;
    }

    // 独立出一个方法专门计算随机偏移
    private void CalculateNewJitter()
    {
        Vector2 randomOffset = Random.insideUnitCircle * jitterRadius;
        currentOffset = new Vector3(randomOffset.x, randomOffset.y, 0f);

        float randomRot = Random.Range(-rotationJitter, rotationJitter);
        currentJitterRot = Quaternion.Euler(0f, 0f, randomRot);
    }
}