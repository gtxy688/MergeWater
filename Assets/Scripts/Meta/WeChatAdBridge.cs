using WeChatWASM;

namespace MergeWater.Meta
{
    /// <summary>
    /// 微信广告 SDK 的**唯一**创建入口（2026-09-14）。
    ///
    /// <para>为什么要单独一个文件：SDK 的静态类 <c>WeChatWASM.WX</c> 只存在于运行时程序集
    /// `wx-runtime.dll`（该 DLL 标了 `Exclude Editor: 1`），编辑器里用的是 `wx-runtime-editor.dll`，
    /// 里面**没有** `WX` 这个类（但 `WXRewardedVideoAd` / `WXBaseAd` / `WXCreateRewardedVideoAdParam`
    /// 都在，所以广告实例的 `Load/Show/OnClose/OnError` 能参与编辑器编译与类型检查）。</para>
    ///
    /// <para>因此整个适配器里**只有这一行**无法被编辑器编译验证，被刻意隔离在本方法内：
    /// 改这里之后必须跑一次真实 WebGL / 微信导出（2026-09-13 的 CS1503 就是同类问题漏到构建期才炸）。</para>
    /// </summary>
    internal static class WeChatAdBridge
    {
        /// <summary>创建激励视频实例；非微信运行时返回 null（调用方已用 <c>IsAvailable</c> 拦过）。</summary>
        public static WXRewardedVideoAd CreateRewardedVideo(string adUnitId)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return WX.CreateRewardedVideoAd(new WXCreateRewardedVideoAdParam { adUnitId = adUnitId });
#else
            return null;
#endif
        }
    }
}
