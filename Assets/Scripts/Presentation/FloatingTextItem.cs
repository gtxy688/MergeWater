using TMPro;
using UnityEngine;

namespace MergeWater.Presentation
{
    /// <summary>
    /// 单条飘字的生命周期：上浮 + 淡出 + punch 缩放。对象由场景预置，播放完只隐藏不销毁
    /// （销毁会让「运行时不生成 UI」这条约束失效）。
    ///
    /// <para><b>本类必须与文件同名</b>（`FloatingTextItem.cs`）。Unity 只为「与文件名相同的类」
    /// 生成稳定的 MonoScript GUID；把第二个 MonoBehaviour 塞进别的文件里，往场景写组件时会写成
    /// 无 GUID 的本地 fileID，重载后变成 Missing Script。2026-09-12 的飘字池就踩过这个坑。</para>
    /// </summary>
    [RequireComponent(typeof(TextMeshPro))]
    public sealed class FloatingTextItem : MonoBehaviour
    {
        private static readonly float Duration = 0.8f;

        [Tooltip("基准字号；飘字的大小倍率作用在它之上。")]
        [SerializeField] private float baseFontSize = 4f;

        private TextMeshPro _mesh;
        private Color _baseColor = Color.white;
        private float _remaining;
        private float _rise;
        private float _baseFontSize;

        private void Awake()
        {
            _mesh = GetComponent<TextMeshPro>();
            _baseFontSize = baseFontSize > 0f ? baseFontSize : 4f;
        }

        /// <summary>播一条飘字。对象已是池中预置的，这里只重置状态。</summary>
        public void Play(string text, Color color, float sizeMultiplier = 1f)
        {
            if (_mesh == null)
                _mesh = GetComponent<TextMeshPro>();

            if (_mesh == null)
                return;

            _mesh.text = text;
            _mesh.color = color;
            _baseColor = color;
            // 世界空间 TextMeshPro 没有 characterSize（那是 legacy TextMesh 的字段与 UGUI TMP 的缩放），
            // 视觉大小用 fontSize 表达（与场景里预置的 mesh.fontSize 基准一致）。
            _mesh.fontSize = _baseFontSize * Mathf.Max(0.1f, sizeMultiplier);

            transform.localScale = Vector3.one;
            gameObject.SetActive(true);

            _remaining = Duration;
            _rise = 1.0f * Mathf.Max(0.1f, sizeMultiplier);
        }

        private void Update()
        {
            if (_remaining <= 0f)
                return;

            var unscaled = Time.unscaledDeltaTime;
            _remaining = Mathf.Max(0f, _remaining - unscaled);
            var progress = 1f - _remaining / Duration;

            transform.position += Vector3.up * (_rise * unscaled / Duration);

            if (_mesh != null)
            {
                var color = _baseColor;
                color.a = Mathf.Clamp01(1f - progress * progress);
                _mesh.color = color;
            }

            var punch = 1f + Mathf.Sin(Mathf.Clamp01(progress * 3f) * Mathf.PI) * 0.35f;
            transform.localScale = Vector3.one * punch;

            // 播完隐藏并归还池子（不销毁：销毁会让「运行时不生成 UI」这条约束失效）。
            if (_remaining <= 0f)
                gameObject.SetActive(false);
        }
    }
}
