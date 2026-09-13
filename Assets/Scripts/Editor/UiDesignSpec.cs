using UnityEngine;

namespace MergeWater.Editor
{
    /// <summary>
    /// 竖屏 UI 的设计规格（GDD §6.2、R27）——**只有常量，没有任何生成逻辑**。
    ///
    /// <para>界面本身活在 <c>Assets/Scenes/Main.unity</c> 里，由手工编辑维护；这里的数字是
    /// 校验它是否合规的**判据**（EditMode 门禁 <c>HudLayoutTests</c> / <c>SceneAssetTests</c>），
    /// 也是改 UI 时该遵守的设计约束：全部元素必须落在 1080×1920 设计画布内、顶栏避开微信胶囊区、
    /// 底部留出净空、底色由相机 SolidColor 清屏提供（HUD 里不得再放满屏不透明底板）。</para>
    ///
    /// <para>历史：这些常量原属已删除的 UI 生成器 <c>HudBuilder</c>（2026-09-13 随需求方
    /// 「以后只手动编辑 UI」一并移除）。取值未变——门禁断言的仍是同一套规格。</para>
    /// </summary>
    public static class UiDesignSpec
    {
        /// <summary>设计画布宽度（CanvasScaler.referenceResolution.x）。</summary>
        public const float ReferenceWidth = 1080f;

        /// <summary>设计画布高度（CanvasScaler.referenceResolution.y，竖屏：高 &gt; 宽）。</summary>
        public const float ReferenceHeight = 1920f;

        /// <summary>微信胶囊保留区宽度（右上角，参考分辨率单位）：顶栏元素不得进入（GDD §6.2）。</summary>
        public const float CapsuleZoneWidth = 300f;

        /// <summary>微信胶囊保留区高度。</summary>
        public const float CapsuleZoneHeight = 115f;

        /// <summary>底部净空：HUD 不放任何元素，对应 R27（底部不放 Banner / 信息流 / 推广位）。</summary>
        public const float BottomClearance = 60f;

        /// <summary>
        /// 对局底色：由相机 SolidColor 清屏提供（世界空间背景由 `Backdrop` 节点覆盖时它只是兜底）。
        /// HUD 里**不能**再放一块满屏不透明底板——Canvas 是 ScreenSpaceOverlay，永远绘制在世界之上，
        /// 一块不透明的满屏 Image 会把水果、容器、警戒线、落点预览线全部盖住（2026-09-11 实际发生过）。
        /// 门禁：<c>HudLayoutTests.NoOpaqueFullScreenGraphic_HidesTheGameWorld</c>。
        /// </summary>
        public static readonly Color BackgroundColor = new Color(0.99f, 0.93f, 0.84f, 1f);
    }
}
