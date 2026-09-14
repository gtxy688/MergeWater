using UnityEngine;

namespace MergeWater.Presentation
{
    /// <summary>
    /// 警戒线视觉与越线脉冲（R6、V2.25）。
    ///
    /// <para><b>常态颜色的唯一来源是场景</b>（2026-09-14 需求方反馈「我明明调整过警戒线的颜色，
    /// 进游戏又被你们调回去了」）：运行时只从 `LineRenderer` 自身读取它在场景里的颜色，
    /// **不写任何"纠正"逻辑**。你想要半透明、想要接近白色、甚至想要完全透明，都由你决定。
    /// 脉冲（越线）期间才临时改色，脉冲结束后还原成你设的颜色。</para>
    ///
    /// <para><b>已删除的旧行为</b>：原 <c>EnsureVisibleColors()</c> 会在 <c>Awake</c> 里把
    /// 「alpha &lt; 0.45 或 RGB 全 &gt; 0.95」的常态色替换成默认色 <c>(0.86, 0.36, 0.30, 0.55)</c>
    /// ——那是 V2.25「线看不见」时加的兜底，正是它把你的调整改回去的。</para>
    /// </summary>
    public sealed class DangerLineView : MonoBehaviour
    {
        [SerializeField] private LineRenderer line;

        [Tooltip("越线脉冲时的颜色；常态色不用在这里填——运行时直接取 LineRenderer 在场景里的颜色")]
        [SerializeField] private Color dangerColor = new Color(0.95f, 0.2f, 0.2f, 1f);

        [SerializeField] private float pulseSpeed = 1.6f;

        private bool _pulsing;
        private float _pulse;
        private Color _normalColor = Color.white;
        private bool _hasNormalColor;

        public bool IsPulsing => _pulsing;

        private void Awake()
        {
            ResolveLine();
            CaptureNormalColor();
        }

        /// <summary>
        /// 取运行时使用的 LineRenderer。
        /// 不能只在编辑器期把引用缓存进非序列化字段——那样运行时是 null，
        /// SetLine 会直接提前返回，警戒线既没有位置也没有颜色（历史实测：线从未显示过）。
        /// </summary>
        private LineRenderer ResolveLine()
        {
            if (line == null)
                line = GetComponentInChildren<LineRenderer>(true);

            return line;
        }

        /// <summary>把场景里 LineRenderer 的颜色记为常态色——这就是唯一权威来源，运行时不改写它。</summary>
        private void CaptureNormalColor()
        {
            var target = ResolveLine();
            if (target == null)
                return;

            _normalColor = target.startColor;
            _hasNormalColor = true;
        }

        public void Configure(LineRenderer target)
        {
            line = target;
            CaptureNormalColor();
        }

        public void SetLine(float y, float halfWidth, float width)
        {
            var target = ResolveLine();
            if (target == null)
                return;

            target.useWorldSpace = true;
            target.positionCount = 2;
            target.SetPosition(0, new Vector3(-halfWidth, y, 0f));
            target.SetPosition(1, new Vector3(halfWidth, y, 0f));
            target.startWidth = width;
            target.endWidth = width;
            target.enabled = true;

            if (!_hasNormalColor)
                CaptureNormalColor(); // 场景里没接线（运行时新建）时补记一次

            if (!_pulsing)
                ApplyColor(_normalColor);
        }

        /// <summary>越线期间每秒一次心跳音由调用方负责；这里只做脉冲。</summary>
        public void SetPulsing(bool pulsing)
        {
            _pulsing = pulsing;
            if (!pulsing)
            {
                _pulse = 0f;
                ApplyColor(_normalColor);
            }
        }

        private void Update()
        {
            if (!_pulsing)
                return;

            _pulse += Time.unscaledDeltaTime * pulseSpeed;
            var t = (Mathf.Sin(_pulse * Mathf.PI * 2f) + 1f) * 0.5f;
            ApplyColor(Color.Lerp(_normalColor, dangerColor, t));
        }

        private void ApplyColor(Color color)
        {
            var target = ResolveLine();
            if (target == null)
                return;

            target.startColor = color;
            target.endColor = color;
        }
    }
}
