using System;
using System.Collections.Generic;
using MergeWater.Core;
using UnityEngine;

namespace MergeWater.Aim
{
    /// <summary>
    /// 指针输入 → 瞄准状态。实现 <see cref="IAimSource"/>。
    /// 每次松手最多发出一次 <see cref="DropRequested"/> 或 <see cref="ItemTargetRequested"/>；
    /// <see cref="SetInteractable"/> 为 false 或指针离开屏幕时取消瞄准且不发指令。
    /// </summary>
    public sealed class AimController : MonoBehaviour, IAimSource
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float defaultRadius = 0.24f;
        [SerializeField] private float previewBounceRestitution = 0.35f;

        private readonly List<Vector2> _preview = new List<Vector2>(64);

        private IPointerSource _input;
        private float _minX = -2.2f;
        private float _maxX = 2.2f;
        private float _dropRadius;
        private float _dropSpawnY = 5.2f;
        private float _floorY = -4.2f;
        private float _gravity = -9.81f;
        private float _maxPreviewTime = 0.8f;
        private float _aimRadius = 0.6f;

        private bool _interactable = true;
        private bool _aiming;
        private bool _itemAim;
        private bool _pointerLeftScreen;
        private ItemKind _itemKind = ItemKind.None;
        private float _x;
        private Vector2 _aimPoint;

        public event Action<float> DropRequested;
        public event Action<ItemKind, Vector2> ItemTargetRequested;

        public AimState State => new AimState(
            _aiming,
            _itemAim ? _itemKind : ItemKind.None,
            _x,
            AimSolver.NormalizeX(_x, _minX, _maxX),
            _itemAim);

        public bool Interactable => _interactable;

        public float DropRadius => _dropRadius;

        public static IPointerSource DefaultPointerSource { get; set; }

        private void Awake()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            _dropRadius = defaultRadius;
        }

        private void Update()
        {
            if (!_interactable || targetCamera == null)
                return;

            var source = _input ?? DefaultPointerSource ?? (_input = new LegacyPointerSource());
            HandleFrame(source.Read());
        }

        /// <summary>测试与自定义输入源入口；也可由外部驱动以复现触屏行为。</summary>
        public void HandleFrame(in PointerFrame frame)
        {
            if (!_interactable || targetCamera == null)
                return;

            var pointerInside = frame.InsideScreen;

            if (frame.Down)
            {
                _aiming = true;
                _pointerLeftScreen = !pointerInside;
                UpdateFromPointer(frame.ScreenPosition);
                return;
            }

            if (_aiming && !pointerInside)
                _pointerLeftScreen = true;

            if (_aiming && frame.Held)
            {
                UpdateFromPointer(frame.ScreenPosition);
                return;
            }

            if (_aiming && frame.Up)
            {
                if (_pointerLeftScreen)
                {
                    CancelAimInternal();
                    return;
                }

                UpdateFromPointer(frame.ScreenPosition);
                Release();
            }
        }

        public void SetInputSource(IPointerSource source) => _input = source;

        /// <summary>注入相机（测试与运行时装配均可使用；未设置时回退 Camera.main）。</summary>
        public void SetCamera(Camera camera) => targetCamera = camera;

        public void SetDropRadius(float radius) => _dropRadius = Mathf.Max(0f, radius);

        public void SetDropBounds(float minX, float maxX)
        {
            _minX = minX;
            _maxX = maxX;
            _x = AimSolver.ClampX(_x, _minX, _maxX, _dropRadius, out _);
        }

        /// <summary>预览所需的几何参数（由 M7 从数值注入）。</summary>
        public void SetDropGeometry(float spawnY, float floorY, float gravity, float maxPreviewTime)
        {
            _dropSpawnY = spawnY;
            _floorY = floorY;
            _gravity = gravity;
            _maxPreviewTime = maxPreviewTime;
        }

        public void SetInteractable(bool interactable)
        {
            _interactable = interactable;
            if (!interactable)
                CancelAimInternal();
        }

        public void BeginItemAim(ItemKind kind, float radius)
        {
            if (!_interactable || kind == ItemKind.None)
                return;

            _itemAim = true;
            _itemKind = kind;
            _aimRadius = Mathf.Max(0.01f, radius);
            _aiming = true;
            _pointerLeftScreen = false;
            _aimPoint = new Vector2(_x, _floorY);
        }

        public void CancelItemAim()
        {
            if (_itemAim)
                CancelAimInternal();
        }

        /// <summary>采样当前落点的预测路径（垂直下落 + 镜像反弹）。</summary>
        public int BuildPreview(List<Vector2> output)
        {
            var start = new Vector2(_x, _dropSpawnY);
            return AimSolver.SampleTrajectory(
                start,
                Vector2.zero,
                _gravity,
                _floorY,
                _minX,
                _maxX,
                previewBounceRestitution,
                _maxPreviewTime,
                0.04f,
                output);
        }

        public float AimRadius => _aimRadius;

        private void UpdateFromPointer(Vector2 screenPosition)
        {
            var world = WorldFromScreen(screenPosition);

            if (_itemAim)
            {
                _aimPoint = world;
                _x = Mathf.Clamp(world.x, _minX, _maxX);
                return;
            }

            _x = AimSolver.ClampX(world.x, _minX, _maxX, _dropRadius, out var inverted);
            if (inverted)
                Debug.LogWarning("[AimController] 落点区间因半径反转，已使用区间中点。");
        }

        private Vector2 WorldFromScreen(Vector2 screenPosition)
        {
            if (targetCamera == null)
                return new Vector2(_x, _floorY);

            var depth = Mathf.Abs(targetCamera.transform.position.z);

            var world = targetCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
            return new Vector2(world.x, world.y);
        }

        private void Release()
        {
            var wasItemAim = _itemAim;
            var kind = _itemKind;
            var dropX = _x;
            var point = wasItemAim ? _aimPoint : new Vector2(_x, _floorY);

            CancelAimInternal();

            if (wasItemAim)
                ItemTargetRequested?.Invoke(kind, point);
            else
                DropRequested?.Invoke(dropX);
        }

        private void CancelAimInternal()
        {
            _aiming = false;
            _itemAim = false;
            _itemKind = ItemKind.None;
            _pointerLeftScreen = false;
        }

        private void OnDisable() => CancelAimInternal();
    }
}
