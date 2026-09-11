using UnityEngine;

namespace MergeWater.Presentation
{
    /// <summary>
    /// 表现时间的唯一管理者（决策 A5）：慢放、顿帧与恢复都走这里，
    /// 其他模块不得直接改 <c>Time.timeScale</c>。结束时必定恢复为 1。
    /// </summary>
    public sealed class TimeDirector : MonoBehaviour
    {
        private float _slowMoRemaining;
        private float _slowMoScale = 1f;
        private float _hitStopRemaining;

        public bool IsBusy => _slowMoRemaining > 0f || _hitStopRemaining > 0f;

        public float CurrentScale => Time.timeScale;

        public void RequestSlowMo(float scale, float seconds)
        {
            if (seconds <= 0f)
                return;

            var clampedScale = Mathf.Clamp(scale, 0.05f, 1f);
            if (_slowMoRemaining <= 0f || clampedScale < _slowMoScale)
                _slowMoScale = clampedScale;

            _slowMoRemaining = Mathf.Max(_slowMoRemaining, seconds);
            Apply();
        }

        public void RequestHitStop(float seconds)
        {
            if (seconds <= 0f)
                return;

            _hitStopRemaining = Mathf.Max(_hitStopRemaining, seconds);
            Apply();
        }

        /// <summary>由 <see cref="Update"/> 用非缩放时间驱动；测试可直接调用。</summary>
        public void Tick(float unscaledDeltaSeconds)
        {
            if (unscaledDeltaSeconds <= 0f)
                return;

            var changed = false;

            if (_hitStopRemaining > 0f)
            {
                _hitStopRemaining = Mathf.Max(0f, _hitStopRemaining - unscaledDeltaSeconds);
                changed = true;
            }

            if (_slowMoRemaining > 0f)
            {
                _slowMoRemaining = Mathf.Max(0f, _slowMoRemaining - unscaledDeltaSeconds);
                changed = true;

                if (_slowMoRemaining <= 0f)
                    _slowMoScale = 1f;
            }

            if (changed)
                Apply();
        }

        public void ResetToNormal()
        {
            _hitStopRemaining = 0f;
            _slowMoRemaining = 0f;
            _slowMoScale = 1f;
            Time.timeScale = 1f;
        }

        private void Update() => Tick(Time.unscaledDeltaTime);

        private void OnDisable() => ResetToNormal();

        private void OnDestroy() => ResetToNormal();

        private void Apply()
        {
            if (_hitStopRemaining > 0f)
                Time.timeScale = 0f;
            else if (_slowMoRemaining > 0f)
                Time.timeScale = _slowMoScale;
            else
                Time.timeScale = 1f;
        }
    }
}
