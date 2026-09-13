using UnityEngine;

namespace MergeWater.Presentation
{
    /// <summary>
    /// 对局世界背景：把一张背景图按「cover」铺满相机视野，并排在所有对局元素之后。
    ///
    /// 为什么放在世界里而不是 Canvas 上：Canvas 是 ScreenSpaceOverlay，永远绘制在世界之上，
    /// 任何满屏的不透明 UI 底板都会盖住水果、容器、警戒线与落点预览（见
    /// <c>HudLayoutTests.NoOpaqueFullScreenGraphic_HidesTheGameWorld</c>）。
    /// 因此背景由世界空间的 <see cref="SpriteRenderer"/> 承担，相机 SolidColor 清屏只作为
    /// 素材缺失时的兜底。换图只需在场景里选中 `GameRoot/Presentation/Backdrop` 节点，
    /// 替换它 <see cref="SpriteRenderer"/> 上的 Sprite（缩放由本组件在运行时按 cover 自动铺满）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BackdropView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer backdrop;
        [SerializeField] private Camera viewCamera;

        /// <summary>额外放大比例：吸收像素取整误差，避免分辨率变化时露出边缘缝隙。</summary>
        [SerializeField] private float overscan = 1.02f;

        private float _lastAspect = -1f;
        private float _lastOrthographicSize = -1f;

        public SpriteRenderer Renderer => backdrop;

        public void Configure(SpriteRenderer spriteRenderer, Camera camera)
        {
            backdrop = spriteRenderer;
            viewCamera = camera;
            FitToCamera(force: true);
        }

        private void LateUpdate() => FitToCamera(force: false);

        /// <summary>
        /// 按「cover」缩放：取宽高两个方向所需比例的较大者，保证任意屏幕比例下都铺满且不留边。
        /// 只在相机宽高比或正交尺寸变化时写入 Transform，避免每帧产生无意义的脏标记。
        /// </summary>
        private void FitToCamera(bool force)
        {
            var camera = viewCamera != null ? viewCamera : Camera.main;
            if (camera == null || !camera.orthographic || backdrop == null || backdrop.sprite == null)
                return;

            if (!force &&
                Mathf.Approximately(_lastAspect, camera.aspect) &&
                Mathf.Approximately(_lastOrthographicSize, camera.orthographicSize))
                return;

            _lastAspect = camera.aspect;
            _lastOrthographicSize = camera.orthographicSize;

            var spriteSize = backdrop.sprite.bounds.size;
            if (spriteSize.x <= 0.0001f || spriteSize.y <= 0.0001f)
                return;

            var viewHeight = camera.orthographicSize * 2f;
            var viewWidth = viewHeight * camera.aspect;

            var scale = Mathf.Max(viewWidth / spriteSize.x, viewHeight / spriteSize.y) * Mathf.Max(1f, overscan);

            var position = backdrop.transform.position;
            var center = camera.transform.position;
            backdrop.transform.position = new Vector3(center.x, center.y, position.z);
            backdrop.transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
