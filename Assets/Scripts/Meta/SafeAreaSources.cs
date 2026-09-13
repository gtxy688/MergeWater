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
    ///
    /// <para><b>数值字段是 double，必须显式转 float</b>：`wx-runtime.dll` 里
    /// <c>WindowInfo</c> 与 <c>SafeArea</c> 的数值字段（windowWidth/windowHeight、left/top/right/bottom…）
    /// **全部是 <c>double</c>**（JS 数值桥接过来的原样类型），传给本类 <c>float</c> 形参时少一个
    /// <c>(float)</c> 就是一次 <c>CS1503: cannot convert from 'double' to 'float'</c>（2026-09-13 实际发生）。
    /// 因此在 WebGL / 微信构建里报这个错时，先怀疑这里缺转换，别去改形参类型。</para>
    ///
    /// <para><b>本类只在 WebGL 构建里编译</b>：WX 分支整段在 <c>#if UNITY_WEBGL &amp;&amp; !UNITY_EDITOR</c> 内，
    /// 编辑器编译（含 EditMode / PlayMode 测试）**不会检查这段代码**——改了这里必须跑一次
    /// WebGL / 微信小游戏构建才算验证过，编辑器不报错不代表它是对的。</para>
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
                    return ToUnityRect((float)window.safeArea.left, (float)window.safeArea.top,
                        (float)window.safeArea.right, (float)window.safeArea.bottom,
                        (float)window.windowWidth, (float)window.windowHeight);
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
                    rect = ToUnityRect((float)capsule.left, (float)capsule.top,
                        (float)capsule.right, (float)capsule.bottom,
                        (float)window.windowWidth, (float)window.windowHeight);

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
