using UnityEngine;

namespace MergeWater.Presentation
{
    /// <summary>
    /// 震屏：只抖动目标 Transform 的本地坐标，时长结束必定还原（V2.20/V2.23/V2.25）。
    /// 用非缩放时间计时，因此在顿帧（timeScale=0）期间仍能正常结束。
    /// </summary>
    public sealed class ScreenShaker : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float frequency = 34f;

        private Vector3 _baseLocalPosition;
        private bool _hasBase;
        private float _remaining;
        private float _duration;
        private float _amplitude;
        private float _seed;

        public bool IsShaking => _remaining > 0f;

        public void SetTarget(Transform shakeTarget)
        {
            Restore();
            target = shakeTarget;
            _hasBase = false;
            CaptureBase();
        }

        /// <summary>三档强度由 <see cref="MergeWater.Core.FeelRules.ShakeTier"/> 决定，取较强的一次。</summary>
        public void Shake(float amplitude, float seconds)
        {
            if (target == null || amplitude <= 0f || seconds <= 0f)
                return;

            CaptureBase();

            if (_remaining > 0f && amplitude < _amplitude)
                return;

            _amplitude = amplitude;
            _duration = seconds;
            _remaining = seconds;
            _seed = Random.value * 100f;
        }

        public void Restore()
        {
            _remaining = 0f;
            _amplitude = 0f;

            if (target != null && _hasBase)
                target.localPosition = _baseLocalPosition;
        }

        private void CaptureBase()
        {
            if (_hasBase || target == null)
                return;

            _baseLocalPosition = target.localPosition;
            _hasBase = true;
        }

        private void Update()
        {
            if (target == null)
                return;

            if (_remaining <= 0f)
                return;

            CaptureBase();

            _remaining = Mathf.Max(0f, _remaining - Time.unscaledDeltaTime);
            var falloff = _duration > 0f ? _remaining / _duration : 0f;
            var amount = _amplitude * falloff;

            var t = (Time.unscaledTime + _seed) * frequency;
            var offset = new Vector3(
                (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f,
                0f) * amount;

            target.localPosition = _baseLocalPosition + offset;

            if (_remaining <= 0f)
            {
                _amplitude = 0f;
                target.localPosition = _baseLocalPosition;
            }
        }

        private void OnDisable() => Restore();
    }
}
