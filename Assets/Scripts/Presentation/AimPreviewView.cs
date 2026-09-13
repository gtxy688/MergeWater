using System.Collections.Generic;
using UnityEngine;

namespace MergeWater.Presentation
{
    /// <summary>
    /// 世界空间瞄准预览：待投水果 + 预测路径虚线（决策 D8：垂直下落 + 镜像反弹）。
    /// 只用 M4 提供的点列绘制，不做自己的物理推算。
    ///
    /// 另外负责待投水果的「出现节奏」（V2.33/V2.34）：投放后先等待一小段时间，
    /// 再让下一颗水果从屏幕中央渐显（缩放 + 淡入）出现，避免上一颗还在下落时新水果就突然冒出。
    /// </summary>
    public sealed class AimPreviewView : MonoBehaviour
    {
        private const float RevealStartScale = 0.55f;

        [SerializeField] private LineRenderer trajectoryLine;
        [SerializeField] private SpriteRenderer pendingFruit;

        private readonly List<Vector3> _buffer = new List<Vector3>(128);
        private Vector3 _pendingBasePosition;
        private bool _pendingVisible;
        private bool _bobbing = true;
        private float _bobPhase;

        private float _revealDelaySeconds = 0.45f;
        private float _revealDurationSeconds = 0.25f;
        private bool _revealing;
        private float _revealElapsed;
        private float _pendingDiameter = 1f;
        private Color _pendingColor = Color.white;

        public bool PendingVisible => _pendingVisible;

        /// <summary>出现动画是否仍在进行（等待延迟或渐显中）。</summary>
        public bool IsRevealing => _revealing;

        public void Configure(LineRenderer line, SpriteRenderer fruit)
        {
            trajectoryLine = line;
            pendingFruit = fruit;
        }

        /// <summary>注入出现节奏（V2.33 延迟 + V2.34 渐显时长），数值来源为 GameBalance。</summary>
        public void ConfigureReveal(float delaySeconds, float durationSeconds)
        {
            _revealDelaySeconds = Mathf.Max(0f, delaySeconds);
            _revealDurationSeconds = Mathf.Max(0.01f, durationSeconds);
        }

        public void SetPendingFruit(int level, Sprite sprite, Color color, float diameter)
        {
            if (pendingFruit == null)
                return;

            pendingFruit.sprite = sprite;
            pendingFruit.sortingOrder = 300 + level;

            _pendingColor = color;
            _pendingDiameter = diameter;

            if (!_revealing)
                ApplyRevealProgress(_pendingVisible ? 1f : 0f);
        }

        /// <summary>待投水果的静止位置（生成高度 + 落点 x）。</summary>
        public void SetPendingBasePosition(Vector2 position)
        {
            _pendingBasePosition = new Vector3(position.x, position.y, 0f);
            if (pendingFruit != null)
                pendingFruit.transform.position = _pendingBasePosition;
        }

        public void SetPendingVisible(bool visible)
        {
            _pendingVisible = visible;

            if (!visible)
            {
                _revealing = false;
                ApplyRevealProgress(0f);
                SetTrajectory(null, false);
                return;
            }

            // 出现动画进行中时不要打断：进度由 Update 推进。
            if (!_revealing)
                ApplyRevealProgress(1f);
        }

        /// <summary>
        /// 投放后重新出现：立即隐藏并开始计时，等待 <paramref name="delaySeconds"/> 后在
        /// <see cref="ConfigureReveal"/> 注入的时长内渐显。<paramref name="delaySeconds"/> 为负时用注入值。
        /// </summary>
        public void BeginPendingReveal(float delaySeconds = -1f)
        {
            if (delaySeconds >= 0f)
                _revealDelaySeconds = delaySeconds;

            _pendingVisible = true;
            _revealing = true;
            _revealElapsed = 0f;

            ApplyRevealProgress(0f);
            SetTrajectory(null, false);
        }

        /// <summary>把出现进度（0=完全不可见，1=完整显示）应用到待投水果。</summary>
        public void ApplyRevealProgress(float progress)
        {
            if (pendingFruit == null)
                return;

            var t = Mathf.Clamp01(progress);

            if (!_pendingVisible || t <= 0.0001f)
            {
                pendingFruit.enabled = false;
                return;
            }

            var eased = Mathf.SmoothStep(0f, 1f, t);
            var color = _pendingColor;
            color.a *= eased;

            pendingFruit.color = color;
            pendingFruit.transform.localScale = Vector3.one * (_pendingDiameter * Mathf.Lerp(RevealStartScale, 1f, eased));
            pendingFruit.enabled = true;
        }

        /// <summary>橡皮筋回弹：瞄准期间轻微上下浮动。</summary>
        public void SetBobbing(bool bobbing) => _bobbing = bobbing;

        public void SetTrajectory(IReadOnlyList<Vector2> points, bool visible)
        {
            if (trajectoryLine == null)
                return;

            var count = visible && points != null ? points.Count : 0;
            if (count < 2)
            {
                trajectoryLine.positionCount = 0;
                trajectoryLine.enabled = false;
                return;
            }

            _buffer.Clear();
            for (var i = 0; i < points.Count; i++)
                _buffer.Add(new Vector3(points[i].x, points[i].y, 0f));

            trajectoryLine.positionCount = _buffer.Count;
            trajectoryLine.SetPositions(_buffer.ToArray());
            trajectoryLine.enabled = true;
        }

        public void Clear()
        {
            SetPendingVisible(false);
            SetTrajectory(null, false);
        }

        private void Update()
        {
            if (!_pendingVisible || pendingFruit == null)
                return;

            var delta = Time.unscaledDeltaTime;

            if (_revealing)
            {
                _revealElapsed += delta;

                var progress = Mathf.Clamp01((_revealElapsed - _revealDelaySeconds) / _revealDurationSeconds);
                ApplyRevealProgress(progress);

                if (progress >= 1f)
                    _revealing = false;

                // 渐显期间不做上下浮动，避免两种动作叠加。
                return;
            }

            if (!_bobbing)
            {
                pendingFruit.transform.position = _pendingBasePosition;
                return;
            }

            _bobPhase += delta * 6f;
            var offset = Mathf.Sin(_bobPhase) * 0.035f;
            pendingFruit.transform.position = _pendingBasePosition + new Vector3(0f, offset, 0f);
        }
    }
}
