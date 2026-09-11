using UnityEngine;

namespace MergeWater.Presentation
{
    /// <summary>警戒线视觉与红色脉冲（R6、V2.25）。</summary>
    public sealed class DangerLineView : MonoBehaviour
    {
        [SerializeField] private LineRenderer line;
        [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color dangerColor = new Color(0.95f, 0.2f, 0.2f, 1f);
        [SerializeField] private float pulseSpeed = 1.6f;

        private LineRenderer _line;
        private bool _pulsing;
        private float _pulse;

        public bool IsPulsing => _pulsing;

        public void Configure(LineRenderer target)
        {
            _line = target;
            line = target;
            ApplyColor(normalColor);
        }

        public void SetLine(float y, float halfWidth, float width)
        {
            if (_line == null)
                return;

            _line.useWorldSpace = true;
            _line.positionCount = 2;
            _line.SetPosition(0, new Vector3(-halfWidth, y, 0f));
            _line.SetPosition(1, new Vector3(halfWidth, y, 0f));
            _line.startWidth = width;
            _line.endWidth = width;
            _line.enabled = true;
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
            if (_line == null)
                return;

            _line.startColor = color;
            _line.endColor = color;
        }
    }
}
