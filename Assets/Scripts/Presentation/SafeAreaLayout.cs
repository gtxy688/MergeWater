using UnityEngine;

namespace MergeWater.Presentation
{
    /// <summary>
    /// 「安全区 → 顶栏下移量」的**纯计算**（不依赖场景、不依赖 RectTransform），便于 EditMode 直接单测。
    ///
    /// <para>规则（2026-09-13）：顶栏元素要避开的顶部遮挡 = <c>max(安全区顶边, 微信胶囊下沿)</c>；
    /// 再留一点间距。设计稿只能按「假定胶囊区」预留固定高度，真机（刘海/灵动岛）实际更低，
    /// 所以需要按实测值**补差**——而不是重新排一套布局。</para>
    /// </summary>
    public static class SafeAreaLayout
    {
        /// <summary>顶栏元素与「最大遮挡下沿」之间保留的间距（设计单位）。</summary>
        public const float DefaultMarginDesignUnits = 16f;

        /// <summary>
        /// 设备要求的最小顶部留白（像素，自屏幕顶算起）。
        /// 取安全区顶边与胶囊下沿的较大者，再加间距。
        /// </summary>
        public static float RequiredTopPixels(float screenHeightPx, Rect safeArea,
            bool hasMenuButton, Rect menuButton, float marginPx)
        {
            if (screenHeightPx <= 0f)
                return 0f;

            // 安全区顶边距屏幕顶的像素距离
            var required = Mathf.Max(0f, screenHeightPx - safeArea.yMax);

            if (hasMenuButton)
            {
                // 胶囊下沿距屏幕顶的像素距离（胶囊在右上角，往往比安全区更低）
                var capsuleBottom = Mathf.Max(0f, screenHeightPx - menuButton.yMin);
                required = Mathf.Max(required, capsuleBottom);
            }

            return required + Mathf.Max(0f, marginPx);
        }

        /// <summary>
        /// 还需要额外下移多少像素：设备要求减去设计稿已经预留的。
        /// 结果 ≥ 0——设备要求比设计低时**不上移**（否则无刘海设备上顶栏会往上跳）。
        /// </summary>
        public static float ExtraTopPixels(float requiredTopPx, float currentTopPx) =>
            Mathf.Max(0f, requiredTopPx - currentTopPx);

        /// <summary>像素 → 设计单位（画布用 CanvasScaler 按参考分辨率缩放）。</summary>
        public static float PixelsToDesignUnits(float pixels, float screenHeightPx, float canvasHeightDesign)
        {
            if (screenHeightPx <= 0f || canvasHeightDesign <= 0f)
                return 0f;

            return pixels * (canvasHeightDesign / screenHeightPx);
        }

        /// <summary>设计单位 → 像素（<see cref="PixelsToDesignUnits"/> 的逆）。</summary>
        public static float DesignUnitsToPixels(float designUnits, float screenHeightPx, float canvasHeightDesign)
        {
            if (canvasHeightDesign <= 0f)
                return 0f;

            return designUnits * (screenHeightPx / canvasHeightDesign);
        }
    }
}
