using System;
using System.Collections;
using UnityEngine;

namespace MergeWater.Meta
{
    /// <summary>
    /// 未同意隐私或无需广告时的空实现：明确声明不可用，让上层按 D6 走离线降级。
    /// </summary>
    public sealed class NullAdsService : MergeWater.Core.IAdsService
    {
        public bool IsAvailable => false;

        public bool IsInitialized => false;

        public bool IsShowing => false;

        public void Initialize()
        {
        }

        public void ShowRewarded(MergeWater.Core.AdPlacement placement,
            Action<MergeWater.Core.RewardedResult> onComplete) =>
            onComplete?.Invoke(MergeWater.Core.RewardedResult.Unavailable);

        public void ShowInterstitial(Action<MergeWater.Core.InterstitialResult> onComplete) =>
            onComplete?.Invoke(MergeWater.Core.InterstitialResult.Unavailable);
    }

    /// <summary>
    /// 可替换代理：隐私同意前持有 <see cref="NullAdsService"/>，同意后由 M7 换成真实/测试适配器，
    /// 从而让已构造好的服务无需重建（架构决策 A4）。
    /// </summary>
    public sealed class AdsServiceProxy : MergeWater.Core.IAdsService
    {
        public MergeWater.Core.IAdsService Inner { get; set; } = new NullAdsService();

        public bool IsAvailable => Inner != null && Inner.IsAvailable;

        public bool IsInitialized => Inner != null && Inner.IsInitialized;

        public bool IsShowing => Inner != null && Inner.IsShowing;

        public void Initialize() => Inner?.Initialize();

        public void ShowRewarded(MergeWater.Core.AdPlacement placement,
            Action<MergeWater.Core.RewardedResult> onComplete)
        {
            if (Inner == null)
            {
                onComplete?.Invoke(MergeWater.Core.RewardedResult.Unavailable);
                return;
            }

            Inner.ShowRewarded(placement, onComplete);
        }

        public void ShowInterstitial(Action<MergeWater.Core.InterstitialResult> onComplete)
        {
            if (Inner == null)
            {
                onComplete?.Invoke(MergeWater.Core.InterstitialResult.Unavailable);
                return;
            }

            Inner.ShowInterstitial(onComplete);
        }
    }

    /// <summary>
    /// 测试广告适配器：用一段真实等待模拟激励视频播放，期间 <see cref="IsShowing"/> 为真，
    /// 供 M7 冻结输入。真实 SDK 接入时用同样接口替换即可。
    /// </summary>
    public sealed class MockAdsService : MonoBehaviour, MergeWater.Core.IAdsService
    {
        [SerializeField] private float rewardedSeconds = 0.6f;
        [SerializeField] private float interstitialSeconds = 0.4f;
        [SerializeField] private bool alwaysComplete = true;

        public bool IsAvailable => true;

        public bool IsInitialized { get; private set; }

        public bool IsShowing { get; private set; }

        /// <summary>测试与编辑器调参用：调整模拟广告的行为。</summary>
        public void Configure(bool completes, float rewardedDelaySeconds = -1f, float interstitialDelaySeconds = -1f)
        {
            alwaysComplete = completes;

            if (rewardedDelaySeconds >= 0f)
                rewardedSeconds = rewardedDelaySeconds;

            if (interstitialDelaySeconds >= 0f)
                interstitialSeconds = interstitialDelaySeconds;
        }

        public int RewardedShown { get; private set; }

        public int InterstitialShown { get; private set; }

        public void Initialize() => IsInitialized = true;

        public void ShowRewarded(MergeWater.Core.AdPlacement placement,
            Action<MergeWater.Core.RewardedResult> onComplete)
        {
            if (!IsInitialized)
            {
                onComplete?.Invoke(MergeWater.Core.RewardedResult.Failed);
                return;
            }

            StartCoroutine(RunRewarded(rewardedSeconds, onComplete));
        }

        public void ShowInterstitial(Action<MergeWater.Core.InterstitialResult> onComplete)
        {
            if (!IsInitialized)
            {
                onComplete?.Invoke(MergeWater.Core.InterstitialResult.Failed);
                return;
            }

            StartCoroutine(RunInterstitial(interstitialSeconds, onComplete));
        }

        private IEnumerator RunRewarded(float seconds, Action<MergeWater.Core.RewardedResult> onComplete)
        {
            IsShowing = true;
            RewardedShown++;

            if (seconds > 0f)
                yield return new WaitForSecondsRealtime(seconds);

            IsShowing = false;
            onComplete?.Invoke(alwaysComplete
                ? MergeWater.Core.RewardedResult.Completed
                : MergeWater.Core.RewardedResult.Skipped);
        }

        private IEnumerator RunInterstitial(float seconds, Action<MergeWater.Core.InterstitialResult> onComplete)
        {
            IsShowing = true;
            InterstitialShown++;

            if (seconds > 0f)
                yield return new WaitForSecondsRealtime(seconds);

            IsShowing = false;
            onComplete?.Invoke(MergeWater.Core.InterstitialResult.Shown);
        }
    }
}
