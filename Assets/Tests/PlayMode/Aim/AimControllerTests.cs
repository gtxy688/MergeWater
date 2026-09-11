using System.Collections.Generic;
using MergeWater.Aim;
using MergeWater.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MergeWater.Tests.PlayMode
{
    /// <summary>M4 验收 A6/A7 与边界 E1/E2：输入集成与指令互斥（R1、R8）。</summary>
    public sealed class AimControllerTests
    {
        private GameObject _root;
        private GameObject _cameraObject;
        private Camera _camera;
        private AimController _aim;
        private readonly List<float> _dropRequests = new List<float>();
        private readonly List<KeyValuePair<ItemKind, Vector2>> _itemRequests =
            new List<KeyValuePair<ItemKind, Vector2>>();

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("AimControllerTest");
            _cameraObject = new GameObject("TestCamera");
            _camera = _cameraObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 5f;
            _cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            _aim = _root.AddComponent<AimController>();
            _aim.enabled = false; // 只通过 HandleFrame 驱动，避免真实 Input 干扰
            _aim.SetCamera(_camera);
            _aim.SetDropBounds(-2.2f, 2.2f);
            _aim.SetDropRadius(0.5f);
            _aim.SetDropGeometry(5.2f, -4.2f, -9.81f, 0.8f);

            _dropRequests.Clear();
            _itemRequests.Clear();
            _aim.DropRequested += x => _dropRequests.Add(x);
            _aim.ItemTargetRequested += (kind, point) => _itemRequests.Add(new KeyValuePair<ItemKind, Vector2>(kind, point));
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root);
            if (_cameraObject != null) Object.Destroy(_cameraObject);
            _root = null;
            _cameraObject = null;
            _aim = null;
        }

        private Vector2 ScreenPointForWorldX(float worldX)
        {
            var screen = _camera.WorldToScreenPoint(new Vector3(worldX, 0f, 0f));
            return new Vector2(screen.x, screen.y);
        }

        private void Press(float worldX) =>
            _aim.HandleFrame(new PointerFrame(true, true, false, true, ScreenPointForWorldX(worldX)));

        private void Hold(float worldX) =>
            _aim.HandleFrame(new PointerFrame(false, true, false, true, ScreenPointForWorldX(worldX)));

        private void Release(float worldX) =>
            _aim.HandleFrame(new PointerFrame(false, false, true, true, ScreenPointForWorldX(worldX)));

        [Test]
        public void PressDragRelease_RaisesDropRequestedOnceWithClampedX()
        {
            Press(0f);
            Assert.That(_aim.State.IsAiming, Is.True, "按下应进入瞄准");

            Hold(1.0f);
            Assert.That(_aim.State.X, Is.EqualTo(1.0f).Within(0.02f), "拖动应更新落点");

            Release(1.0f);

            Assert.That(_dropRequests.Count, Is.EqualTo(1), "每次松手恰好一次投放指令");
            Assert.That(_dropRequests[0], Is.EqualTo(1.0f).Within(0.02f));
            Assert.That(_aim.State.IsAiming, Is.False, "松手后回到非瞄准状态");
        }

        [Test]
        public void DragBeyondBounds_ClampsToRadiusAdjustedLimit()
        {
            Press(0f);
            Hold(50f);
            Release(50f);

            Assert.That(_dropRequests.Count, Is.EqualTo(1));
            Assert.That(_dropRequests[0], Is.EqualTo(1.7f).Within(0.02f), "夹取到 maxX − radius");

            Press(0f);
            Hold(-50f);
            Release(-50f);

            Assert.That(_dropRequests[1], Is.EqualTo(-1.7f).Within(0.02f));
        }

        [Test]
        public void SetInteractableFalse_CancelsAimWithoutRaisingRequests()
        {
            Press(0f);
            Hold(0.5f);

            _aim.SetInteractable(false);

            Assert.That(_aim.State.IsAiming, Is.False, "禁用交互应取消瞄准");

            Release(0.5f);
            Assert.That(_dropRequests, Is.Empty, "取消后不应补发投放指令");

            _aim.SetInteractable(true);
            Press(0.2f);
            Release(0.2f);
            Assert.That(_dropRequests.Count, Is.EqualTo(1), "恢复交互后应可正常投放");
        }

        [Test]
        public void PointerLeavingScreen_OnRelease_CancelsInsteadOfDropping()
        {
            Press(0f);
            Hold(0.3f);

            _aim.HandleFrame(new PointerFrame(false, false, true, false, ScreenPointForWorldX(0.3f)));

            Assert.That(_dropRequests, Is.Empty, "指针离开屏幕后松手不应投放");
            Assert.That(_aim.State.IsAiming, Is.False);
        }

        [Test]
        public void ItemAim_ReleasesOneTargetRequest_AndCancelRaisesNothing()
        {
            _aim.BeginItemAim(ItemKind.Hammer, 1.2f);
            Assert.That(_aim.State.IsItemAim, Is.True);

            Press(0.4f);
            Hold(0.6f);
            Release(0.6f);

            Assert.That(_itemRequests.Count, Is.EqualTo(1), "道具瞄准松手恰好一次目标指令");
            Assert.That(_itemRequests[0].Key, Is.EqualTo(ItemKind.Hammer));
            Assert.That(_dropRequests, Is.Empty, "道具瞄准不应产生投放指令");

            _aim.BeginItemAim(ItemKind.Bomb, 0.6f);
            _aim.CancelItemAim();

            Assert.That(_itemRequests.Count, Is.EqualTo(1), "取消瞄准不产生指令");
            Assert.That(_aim.State.IsAiming, Is.False);
        }

        [Test]
        public void BuildPreview_ReturnsVerticalPathWithinField()
        {
            Press(0.5f);
            Hold(0.5f);

            var points = new List<Vector2>();
            var count = _aim.BuildPreview(points);

            Assert.That(count, Is.GreaterThanOrEqualTo(2));
            Assert.That(points[0].y, Is.EqualTo(5.2f).Within(1e-3f), "预览从生成高度开始");
            Assert.That(points[0].x, Is.EqualTo(0.5f).Within(0.05f), "预览起点跟随落点（决策 D8：垂直下落）");

            for (var i = 0; i < points.Count; i++)
                Assert.That(points[i].y, Is.GreaterThanOrEqualTo(-4.2f - 1e-3f));

            Release(0.5f);
        }

        [Test]
        public void AimState_NormalizedX_TracksBounds()
        {
            _aim.SetDropRadius(0f);

            Press(-2.2f);
            Assert.That(_aim.State.NormalizedX, Is.EqualTo(0f).Within(0.02f));

            Hold(2.2f);
            Assert.That(_aim.State.NormalizedX, Is.EqualTo(1f).Within(0.02f));

            Release(0f);
        }
    }
}
