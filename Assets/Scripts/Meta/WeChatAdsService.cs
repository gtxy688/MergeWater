using System;
using MergeWater.Core;
using UnityEngine;
using WeChatWASM;

namespace MergeWater.Meta
{
    /// <summary>
    /// 真实微信激励视频适配器（2026-09-14 新增；**当前未启用**——需求方尚未开通流量主、没有 adUnitId）。
    ///
    /// <para>只在微信小游戏（WebGL 真机）里可用：编辑器与其它平台 <see cref="IsAvailable"/> 恒为 false，
    /// <see cref="AdsAdapterFactory"/> 会自动退回 Mock 广告，业务层不受影响。</para>
    ///
    /// <para>结果映射与 Mock 完全一致：<c>OnClose.isEnded == true</c> → <see cref="RewardedResult.Completed"/>
    /// （发奖/复活）；<c>false</c> → <see cref="RewardedResult.Skipped"/>（用户中途关闭、不发奖）；
    /// 拉取或播放失败 → <see cref="RewardedResult.Failed"/>（UI 提示「暂无可用广告，请稍后再试」）。</para>
    ///
    /// <para>API 签名是用 `ikdasm` dump `wx-runtime-editor.dll` 核对过的：
    /// `WXRewardedVideoAd : WXBaseAd` 提供 `Load(success, failed)` / `Show(success, failed)` /
    /// `OnError(action)` / `OnClose(action)`；创建参数为 `WXCreateRewardedVideoAdParam{ adUnitId }`。</para>
    /// </summary>
    public sealed class WeChatAdsService : IAdsService
    {
        /// <summary>微信广告位 id（形如 `adunit-xxxxxxxxxxxxxxxx`）。为空即视为不可用。</summary>
        public string RewardedAdUnitId { get; set; }

        public bool IsInitialized { get; private set; }

        public bool IsShowing { get; private set; }

        /// <summary>是否可用：必须有 adUnitId，且运行在微信小游戏里。</summary>
        public bool IsAvailable => !string.IsNullOrEmpty(RewardedAdUnitId) && IsWeChatRuntime;

        /// <summary>
        /// 微信小游戏运行时：WebGL 平台且不在编辑器里。
        /// 编辑器里虽然能编译广告类型（editor 版 DLL），但调不通 JS 桥，所以必须排除。
        /// </summary>
        public static bool IsWeChatRuntime =>
            Application.platform == RuntimePlatform.WebGLPlayer && !Application.isEditor;

        private WXRewardedVideoAd _ad;
        private Action<RewardedResult> _pending;

        /// <summary>冷启动失败次数（诊断用：真机上广告创建/预拉取失败不会崩，只会降级）。</summary>
        public int CreateFailureCount { get; private set; }

        public void Initialize()
        {
            if (!IsAvailable)
            {
                IsInitialized = false;
                return;
            }

            try
            {
                _ad = WeChatAdBridge.CreateRewardedVideo(RewardedAdUnitId);
                if (_ad == null)
                {
                    CreateFailureCount++;
                    IsInitialized = false;
                    return;
                }

                _ad.OnClose(OnAdClose);
                _ad.OnError(OnAdError);
                _ad.Load(null, error =>
                    Debug.LogWarning($"[WeChatAdsService] 激励视频预拉取失败（errCode={error?.errCode}），" +
                                     "播放时会再试一次"));
                IsInitialized = true;
            }
            catch (Exception e)
            {
                // 预期内的失败（真机无广告位/未开通流量主/JS 桥异常）：降级为不可用，不抛给业务层。
                CreateFailureCount++;
                Debug.LogWarning($"[WeChatAdsService] 创建激励视频失败，降级为不可用：{e.Message}");
                _ad = null;
                IsInitialized = false;
            }
        }

        public void ShowRewarded(AdPlacement placement, Action<RewardedResult> onComplete)
        {
            // 重入保护：播放中再次请求直接拒绝（连续点击 / 结算页连点复活）。
            if (IsShowing)
            {
                onComplete?.Invoke(RewardedResult.Unavailable);
                return;
            }

            if (!IsAvailable || !IsInitialized || _ad == null)
            {
                onComplete?.Invoke(RewardedResult.Unavailable);
                return;
            }

            _pending = onComplete;
            IsShowing = true;

            try
            {
                // Show 失败通常是「还没拉取到广告」：按官方建议补一次 Load 再 Show，仍失败就按 Failed 收尾。
                _ad.Show(null, _ => _ad.Load(null, _ => _ad.Show(null, retryError => Fail($"show 重试失败 errCode={retryError?.errCode}"))));
            }
            catch (Exception e)
            {
                Fail(e.Message);
            }
        }

        /// <summary>插屏未接入真实广告：MVP 只接激励视频，避免真机上误伤体验（Mock 模式下仍可测插屏节奏）。</summary>
        public void ShowInterstitial(Action<InterstitialResult> onComplete) =>
            onComplete?.Invoke(InterstitialResult.Unavailable);

        private void OnAdClose(WXRewardedVideoAdOnCloseResponse response)
        {
            // isEnded 是唯一可信的发奖依据：中途关闭时为 false（不发奖）。
            var watchedToEnd = response != null && response.isEnded;
            Complete(watchedToEnd ? RewardedResult.Completed : RewardedResult.Skipped);
        }

        private void OnAdError(WXADErrorResponse error) => Fail($"播放出错 errCode={error?.errCode}");

        private void Fail(string reason)
        {
            Debug.LogWarning($"[WeChatAdsService] 激励视频失败：{reason}");
            Complete(RewardedResult.Failed);
        }

        private void Complete(RewardedResult result)
        {
            var pending = _pending;
            _pending = null;
            IsShowing = false;
            pending?.Invoke(result);
        }
    }
}
