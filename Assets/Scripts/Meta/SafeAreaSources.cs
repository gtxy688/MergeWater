using MergeWater.Core;
using UnityEngine;

namespace MergeWater.Meta
{
    /// <summary>
    /// 非微信平台实现：用 Unity 自带的 <see cref="Screen.safeArea"/>（编辑器里就是 Game 视图，
    /// 真机上是系统安全区）。胶囊信息不存在，永远返回 false。
    /// </summary>
    public sealed class UnitySafeAreaSource : ISafeAreaSource
    {
        public Rect SafeArea
        {
            get
            {
                var area = Screen.safeArea;
                return area.width > 0f && area.height > 0f
                    ? area
                    : new Rect(0f, 0f, Screen.width, Screen.height);
            }
        }

        public bool TryGetMenuButton(out Rect rect)
        {
            rect = default;
            return false;
        }
    }

    /// <summary>
    /// 微信小游戏实现：<c>wx.getWindowInfo()</c> 的 <c>safeArea</c> + <c>wx.getMenuButtonBoundingClientRect()</c>。
    ///
    /// <para>两个接口都是**左上角原点、CSS 逻辑像素**，这里换算成 Unity 的**左下角原点、设备像素**
    /// （逻辑像素 → 设备像素要乘 <c>Screen.width / windowWidth</c>，即 DPR）。</para>
    ///
    /// <para>只在 WebGL 真机编译进来；编辑器与其它平台整个 WX 分支被裁掉，行为退化为
    /// <see cref="UnitySafeAreaSource"/>。任何 WX 调用异常都吞掉并降级，绝不影响游戏流程。</para>
    /// </summary>
    public sealed class WxSafeAreaSource : ISafeAreaSource
    {
        private readonly UnitySafeAreaSource _fallback = new UnitySafeAreaSource();

        public Rect SafeArea
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                if (TryGetWindow(out var window))
                    return ToUnityRect(window.safeArea.left, window.safeArea.top,
                        window.safeArea.right, window.safeArea.bottom,
                        window.windowWidth, window.windowHeight);
#endif
                return _fallback.SafeArea;
            }
        }

        public bool TryGetMenuButton(out Rect rect)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (TryGetWindow(out var window))
            {
                try
                {
                    var capsule = WeChatWASM.WX.GetMenuButtonBoundingClientRect();
                    rect = ToUnityRect(capsule.left, capsule.top, capsule.right, capsule.bottom,
                        window.windowWidth, window.windowHeight);

                    if (rect.width > 0f && rect.height > 0f)
                        return true;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[WxSafeAreaSource] 读取微信胶囊失败，按无胶囊处理：{e.Message}");
                }
            }
#endif
            rect = default;
            return false;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private static bool TryGetWindow(out WeChatWASM.WindowInfo window)
        {
            try
            {
                window = WeChatWASM.WX.GetWindowInfo();
                return window.windowWidth > 0f && window.windowHeight > 0f;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[WxSafeAreaSource] 读取微信窗口信息失败，回退 Unity 安全区：{e.Message}");
                window = default;
                return false;
            }
        }
#endif

        /// <summary>微信（左上原点、逻辑像素）→ Unity（左下原点、设备像素）。</summary>
        private static Rect ToUnityRect(float left, float top, float right, float bottom,
            float windowWidth, float windowHeight)
        {
            var scaleX = windowWidth > 0f ? Screen.width / windowWidth : 1f;
            var scaleY = windowHeight > 0f ? Screen.height / windowHeight : 1f;

            var width = Mathf.Max(0f, right - left) * scaleX;
            var height = Mathf.Max(0f, bottom - top) * scaleY;
            var x = left * scaleX;
            var y = Screen.height - bottom * scaleY; // bottom 是「距屏幕顶部」的距离

            return new Rect(x, y, width, height);
        }
    }
}
