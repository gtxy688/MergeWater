using MergeWater.Core;
using UnityEngine;

namespace MergeWater.Meta
{
    /// <summary>
    /// 广告适配器的**唯一开关**（2026-09-14）：按 <see cref="AdsMode"/> 决定给业务层注入 Mock 还是真微信适配器。
    ///
    /// <para>业务层只认 <see cref="IAdsService"/>（硬约束 4），所以「以后换真广告」= 在场景的
    /// `GameBootstrapper` 上把 `adsMode` 改成 <see cref="AdsMode.WeChat"/> + 填 `adunit-xxxxxxxx`，
    /// **EconomyService / 复活流程 / UI 一行都不用改**。</para>
    ///
    /// <para>容错：WeChat 模式在「非微信运行时（编辑器等）」或「adUnitId 为空」时**自动退回 Mock**并告警，
    /// 这样编辑器里照样能把 Success / Cancel / Error 三条分支和复活流程全部走通。</para>
    /// </summary>
    public static class AdsAdapterFactory
    {
        /// <summary>Mock 广告默认播放时长（需求方要求 1~2 秒，不能点击即完成）。</summary>
        public const float DefaultMockRewardedSeconds = 1.5f;
        /// <summary>按模式创建适配器（必要时自动降级为 Mock）。</summary>
        public static IAdsService Create(AdsMode mode, string weChatAdUnitId, MockAdResult mockResult,
            float mockRewardedSeconds, Transform host)
        {
            if (mode == AdsMode.WeChat)
            {
                if (!WeChatAdsService.IsWeChatRuntime)
                {
                    Debug.LogWarning("[AdsAdapterFactory] adsMode = WeChat，但当前不是微信小游戏运行时" +
                                     "（编辑器 / 非 WebGL），已自动退回 Mock 广告。");
                }
                else if (string.IsNullOrEmpty(weChatAdUnitId))
                {
                    Debug.LogWarning("[AdsAdapterFactory] adsMode = WeChat 但 adUnitId 为空，已自动退回 Mock 广告。" +
                                     "开通流量主后把 adunit-xxxxxxxx 填进 GameBootstrapper.weChatRewardedAdUnitId。");
                }
                else
                {
                    return new WeChatAdsService { RewardedAdUnitId = weChatAdUnitId };
                }
            }

            return CreateMock(host, mockResult, mockRewardedSeconds);
        }

        /// <summary>
        /// Mock 适配器需要一个 MonoBehaviour 承载协程，因此挂在 <paramref name="host"/> 上
        /// （宿主为空时自建一个 `AdsAdapter` 对象）。重复调用会复用已有组件，不会叠加。
        /// </summary>
        public static MockAdsService CreateMock(Transform host, MockAdResult result, float rewardedSeconds)
        {
            var target = host != null ? host.gameObject : new GameObject("AdsAdapter");

            var mock = target.GetComponent<MockAdsService>();
            if (mock == null)
                mock = target.AddComponent<MockAdsService>();

            mock.Configure(result, Mathf.Max(0f, rewardedSeconds));
            return mock;
        }
    }

    /// <summary>
    /// 广告模式的运行期配置（2026-09-14）：由 <c>GameBootstrapper</c> 从 Inspector 读出来交给 M7，
    /// M7 只负责拿它建适配器，**不认识具体平台 SDK**。默认值 = Mock + Success + 1.5 秒。
    /// </summary>
    public sealed class AdsRuntimeConfig
    {
        public AdsMode Mode { get; set; } = AdsMode.Mock;

        /// <summary>真实微信广告位 id（`adunit-xxxxxxxx`）；只在 <see cref="Mode"/> = WeChat 时使用。</summary>
        public string WeChatAdUnitId { get; set; } = string.Empty;

        public MockAdResult MockResult { get; set; } = MockAdResult.Success;

        public float MockRewardedSeconds { get; set; } = AdsAdapterFactory.DefaultMockRewardedSeconds;
    }
}
