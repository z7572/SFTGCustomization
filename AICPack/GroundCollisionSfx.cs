using UnityEngine;

namespace AICPack
{
    public class GroundCollisionSfx : MonoBehaviour
    {
        [Header("Sfx Settings")]
        public AudioClip collisionSfx;
        public AudioSource audioSource;

        [Header("Frequency Control")]
        public float fps = 12f;
        public float sfxFrameInterval = 11f;

        private float minTimeInterval;
        private float lastPlayTime = -999f;

        void Start()
        {
            if (this.audioSource == null)
            {
                this.audioSource = GetComponent<AudioSource>();
            }

            // 计算最快播放间隔 (秒)
            this.minTimeInterval = this.sfxFrameInterval / this.fps;
        }

        void OnCollisionStay(Collision collision)
        {
            if (collision.gameObject.layer is 0 or 23)
            {
                // 检查冷却时间：当前时间 - 上次播放时间 >= 最小间隔
                if (Time.time - this.lastPlayTime >= this.minTimeInterval)
                {
                    if (this.audioSource != null && this.collisionSfx != null)
                    {
                        // 允许音效重叠播放
                        this.audioSource.PlayOneShot(this.collisionSfx);
                        this.lastPlayTime = Time.time;
                    }
                }
            }
        }
    }
}