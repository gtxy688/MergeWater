using UnityEngine;

namespace MergeWater.Presentation
{
    /// <summary>警戒线视觉与红色脉冲（R6、V2.25）。</summary>
    public sealed class DangerLineView : MonoBehaviour
    {
        [SerializeField] private LineRenderer line;
        /// <summary>
        /// 常态颜色。必须是可见的：早期用白色 35% 透明画在米色背景上，等于没有警戒线。
        /// 场景里可能保留了旧默认值，运行时由 <see cref="EnsureVisibleColors"/> 纠正。
        /// </summary>
        [SerializeField] private Color normalColor = DefaultNormalColor;

        public static readonly Color DefaultNormalColor = new Color(0.86f, 0.36f, 0.30f, 0.55f);
        [SerializeField] private Color dangerColor = new Color(0.95f, 0.2f, 0.2f, 1f);
        [SerializeField] private float pulseSpeed = 1.6f;

        private bool _pulsing;
        private float _pulse;

        public bool IsPulsing => _pulsing;

        private void Awake()
        {
            EnsureVisibleColors();
            ResolveLine();
        }

        /// <summary>
        /// 取运行时使用的 LineRenderer。
        /// 不能只在编辑器期把引用缓存进非序列化字段——那样运行时是 null，
        /// SetLine 会直接提前返回，警戒线既没有位置也没有颜色（本轮实测：线从未显示过）。
        /// </summary>
        private LineRenderer ResolveLine()
        {
            if (line == null)
                line = GetComponentInChildren<LineRenderer>(true);

            return line;
        }

        /// <summary>
        /// 兜底：若场景/预制体保留了旧版「近白色低透明」的常态色，运行时就地纠正为可见颜色。
        /// 不能只靠改代码默认值——序列化到场景里的旧值会覆盖它（本轮实测 alpha 仍为 0.35）。
        /// </summary>
        private void EnsureVisibleColors()
        {
            var looksInvisible = normalColor.a < 0.45f
                                 || (normalColor.r > 0.95f && normalColor.g > 0.95f && normalColor.b > 0.95f);

            if (looksInvisible)
                normalColor = DefaultNormalColor;
        }

        public void Configure(LineRenderer target)
        {
            EnsureVisibleColors();
            line = target;
            ApplyColor(normalColor);
        }

        public void SetLine(float y, float halfWidth, float width)
        {
            if (ResolveLine() == null)
                return;

            ResolveLine().useWorldSpace = true;
            ResolveLine().positionCount = 2;
            ResolveLine().SetPosition(0, new Vector3(-halfWidth, y, 0f));
            ResolveLine().SetPosition(1, new Vector3(halfWidth, y, 0f));
            ResolveLine().startWidth = width;
            ResolveLine().endWidth = width;
            ResolveLine().enabled = true;

            // 线的颜色也要在此刻写入：场景里烘焙的是旧版「近白低透明」色，
            // 只纠正 normalColor 字段而不落到 LineRenderer 上，画面上依旧看不见。
            if (!_pulsing)
                ApplyColor(normalColor);
        }

        /// <summary>越线期间每秒一次心跳音由调用方负责；这里只做脉冲。</summary>
        public void SetPulsing(bool pulsing)
        {
            _pulsing = pulsing;
            if (!pulsing)
            {
                _pulse = 0f;
                ApplyColor(normalColor);
            }
        }

        private void Update()
        {
            if (!_pulsing)
                return;

            _pulse += Time.unscaledDeltaTime * pulseSpeed;
            var t = (Mathf.Sin(_pulse * Mathf.PI * 2f) + 1f) * 0.5f;
            ApplyColor(Color.Lerp(normalColor, dangerColor, t));
        }

        private void ApplyColor(Color color)
        {
            if (ResolveLine() == null)
                return;

            ResolveLine().startColor = color;
            ResolveLine().endColor = color;
        }
    }
}
