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

    /// <summary>Mock 广告的三种测试结果（Inspector 可切换，2026-09-14）。</summary>
    public enum MockAdResult
    {
        /// <summary>完整播完 → <see cref="MergeWater.Core.RewardedResult.Completed"/>（发奖 / 复活）。</summary>
        Success = 0,

        /// <summary>用户中途关闭 → <see cref="MergeWater.Core.RewardedResult.Skipped"/>（不发奖）。</summary>
        Cancel = 1,

        /// <summary>拉取或播放失败 → <see cref="MergeWater.Core.RewardedResult.Failed"/>（提示「暂无可用广告」）。</summary>
        Error = 2
    }

    /// <summary>
    /// 测试广告适配器：用一段真实等待模拟激励视频播放，期间 <see cref="IsShowing"/> 为真，
    /// 供 M7 冻结输入。真实 SDK 接入时用同样接口替换即可（见 <see cref="WeChatAdsService"/>）。
    ///
    /// <para>2026-09-14 增加：可切换的三种结果（<see cref="MockAdResult"/>）、1.5 秒默认时长、
    /// **重入保护**（播放中再次请求直接拒绝）与**销毁兜底**（对象在播放中被销毁时补一次 Failed 回调，
    /// 避免业务层永远等不到回调而卡在结算页）。</para>
    /// </summary>
    public sealed class MockAdsService : MonoBehaviour, MergeWater.Core.IAdsService
    {
        [Tooltip("模拟的激励视频结果：Success = 完整播完 / Cancel = 中途关闭 / Error = 拉取或播放失败")]
        [SerializeField] private MockAdResult rewardedResult = MockAdResult.Success;

        [Tooltip("模拟的播放时长（秒）。需求方要求 1~2 秒，不要做成点击即完成")]
        [SerializeField] private float rewardedSeconds = 1.5f;

        [SerializeField] private float interstitialSeconds = 0.4f;

        public bool IsAvailable => true;

        public bool IsInitialized { get; private set; }

        public bool IsShowing { get; private set; }

        /// <summary>当前配置的模拟结果与时长（只读，便于测试与真机排查）。</summary>
        public MockAdResult RewardedResult => rewardedResult;

        public float RewardedSeconds => rewardedSeconds;

        /// <summary>
        /// 测试与编辑器调参用：指定本次模拟的结果与时长（时长 <c>&lt; 0</c> 表示不改）。
        /// 测试里通常传 <c>0f</c> 让广告瞬间结束，避免等真实秒数。
        /// </summary>
        public void Configure(MockAdResult result, float rewardedDelaySeconds = -1f,
            float interstitialDelaySeconds = -1f)
        {
            rewardedResult = result;

            if (rewardedDelaySeconds >= 0f)
                rewardedSeconds = rewardedDelaySeconds;

            if (interstitialDelaySeconds >= 0f)
                interstitialSeconds = interstitialDelaySeconds;
        }

        /// <summary>模拟结果 → 业务层结果（唯一映射处，编辑器可单测）。</summary>
        public static MergeWater.Core.RewardedResult ToRewardedResult(MockAdResult result)
        {
            switch (result)
            {
                case MockAdResult.Success:
                    return MergeWater.Core.RewardedResult.Completed;
                case MockAdResult.Cancel:
                    return MergeWater.Core.RewardedResult.Skipped;
                default:
                    return MergeWater.Core.RewardedResult.Failed;
            }
        }

        public int RewardedShown { get; private set; }

        public int InterstitialShown { get; private set; }

        private Action<MergeWater.Core.RewardedResult> _pendingRewarded;

        public void Initialize() => IsInitialized = true;

        public void ShowRewarded(MergeWater.Core.AdPlacement placement,
            Action<MergeWater.Core.RewardedResult> onComplete)
        {
            if (!IsInitialized)
            {
                onComplete?.Invoke(MergeWater.Core.RewardedResult.Failed);
                return;
            }

            // 重入保护：播放中再次请求直接拒绝（连续点击广告按钮 / 结算页连点复活）。
            if (IsShowing)
            {
                onComplete?.Invoke(MergeWater.Core.RewardedResult.Unavailable);
                return;
            }

            _pendingRewarded = onComplete;
            StartCoroutine(RunRewarded(rewardedSeconds, onComplete));
        }

        public void ShowInterstitial(Action<MergeWater.Core.InterstitialResult> onComplete)
        {
            if (!IsInitialized)
            {
                onComplete?.Invoke(MergeWater.Core.InterstitialResult.Failed);
                return;
            }

            if (IsShowing)
            {
                onComplete?.Invoke(MergeWater.Core.InterstitialResult.Unavailable);
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
            _pendingRewarded = null;
            onComplete?.Invoke(ToRewardedResult(rewardedResult));
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

        /// <summary>
        /// 播放中被销毁（切场景 / 重开一局 / 宿主对象没了）时，回调绝不能吞掉——业务层会一直等，
        /// 表现为结算页按钮卡死。这里用最后已知结果补一次回调（失败语义，不发奖）。
        /// </summary>
        private void OnDestroy()
        {
            var pending = _pendingRewarded;
            if (pending == null)
                return;

            _pendingRewarded = null;
            IsShowing = false;
            pending.Invoke(MergeWater.Core.RewardedResult.Failed);
        }
    }
}
