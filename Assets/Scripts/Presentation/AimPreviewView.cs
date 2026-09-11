using System.Collections.Generic;
using UnityEngine;

namespace MergeWater.Presentation
{
    /// <summary>
    /// 世界空间瞄准预览：待投水果 + 预测路径虚线（决策 D8：垂直下落 + 镜像反弹）。
    /// 只用 M4 提供的点列绘制，不做自己的物理推算。
    /// </summary>
    public sealed class AimPreviewView : MonoBehaviour
    {
        [SerializeField] private LineRenderer trajectoryLine;
        [SerializeField] private SpriteRenderer pendingFruit;

        private readonly List<Vector3> _buffer = new List<Vector3>(128);
        private Vector3 _pendingBasePosition;
        private bool _pendingVisible;
        private bool _bobbing = true;
        private float _bobPhase;

        public bool PendingVisible => _pendingVisible;

        public void Configure(LineRenderer line, SpriteRenderer fruit)
        {
            trajectoryLine = line;
            pendingFruit = fruit;
        }

        public void SetPendingFruit(int level, Sprite sprite, Color color, float diameter)
        {
            if (pendingFruit == null)
                return;

            pendingFruit.sprite = sprite;
            pendingFruit.color = color;
            pendingFruit.transform.localScale = Vector3.one * diameter;
            pendingFruit.sortingOrder = 300 + level;
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
            if (pendingFruit != null)
                pendingFruit.enabled = visible;

            if (!visible)
                SetTrajectory(null, false);
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

            if (!_bobbing)
            {
                pendingFruit.transform.position = _pendingBasePosition;
                return;
            }

            _bobPhase += Time.unscaledDeltaTime * 6f;
            var offset = Mathf.Sin(_bobPhase) * 0.035f;
            pendingFruit.transform.position = _pendingBasePosition + new Vector3(0f, offset, 0f);
        }
    }
}
