using System;
using MergeWater.Core;

namespace MergeWater.Meta
{
    /// <summary>
    /// 广告位放行规则（V2.12–V2.16、V2.19）。纯逻辑，不依赖具体广告适配器。
    /// 离线（适配器不可用）且允许离线领取时按决策 D6 放行；此时并没有真正播放广告，
    /// 因此不写广告冷却（每日上限仍然照常消耗，避免无限囤积）。
    /// </summary>
    public static class AdsPlacementRules
    {
        public const string ReasonCooldown = "冷却中，请稍后再试";
        public const string ReasonDailyCap = "今日领取次数已用完";
        public const string ReasonToggleOff = "该广告位已关闭";
        public const string ReasonUnavailable = "广告暂不可用";
        public const string ReasonNotNow = "当前不可观看";

        /// <summary>复活：每局次数由 RoundSession 校验，这里只校验全局冷却（V2.12）。</summary>
        public static AdDecisionResult CanShowRevive(SaveData data, IClock clock, GameBalance balance,
            bool adsAvailable, bool offlineGrantAll)
        {
            if (data == null || clock == null || balance == null)
                return AdDecisionResult.Denied(AdDecision.Unavailable, ReasonUnavailable);

            if (!adsAvailable)
            {
                return offlineGrantAll
                    ? AdDecisionResult.Allowed()
                    : AdDecisionResult.Denied(AdDecision.Unavailable, ReasonUnavailable);
            }

            if (IsOnCooldown(data.lastReviveAdUtcTicks, clock.Now, balance.ReviveCooldownSeconds))
                return AdDecisionResult.Denied(AdDecision.CooldownActive, ReasonCooldown);

            return AdDecisionResult.Allowed();
        }

        /// <summary>撤销/炸弹/锤子/摇一摇的领取：先看每日上限，再看适配器可用性。</summary>
        public static AdDecisionResult CanGrantItem(ItemKind kind, DailyLimitService daily, GameBalance balance,
            bool adsAvailable, bool offlineGrantAll)
        {
            if (daily == null || balance == null)
                return AdDecisionResult.Denied(AdDecision.Unavailable, ReasonUnavailable);

            if (daily.Remaining(kind, balance) <= 0)
                return AdDecisionResult.Denied(AdDecision.DailyCapReached, ReasonDailyCap);

            if (!adsAvailable && !offlineGrantAll)
                return AdDecisionResult.Denied(AdDecision.Unavailable, ReasonUnavailable);

            return AdDecisionResult.Allowed();
        }

        /// <summary>大礼包：每日 1 次（V2.15）。</summary>
        public static AdDecisionResult CanGrantGift(DailyLimitService daily, GameBalance balance,
            bool adsAvailable, bool offlineGrantAll)
        {
            if (daily == null || balance == null)
                return AdDecisionResult.Denied(AdDecision.Unavailable, ReasonUnavailable);

            if (daily.GiftRemaining(balance) <= 0)
                return AdDecisionResult.Denied(AdDecision.DailyCapReached, ReasonDailyCap);

            if (!adsAvailable && !offlineGrantAll)
                return AdDecisionResult.Denied(AdDecision.Unavailable, ReasonUnavailable);

            return AdDecisionResult.Allowed();
        }

        /// <summary>插屏：仅结算页，每 3 局至多 1 次且可远程开关（V2.16）。</summary>
        public static AdDecisionResult CanShowInterstitial(SaveData data, GameBalance balance,
            bool interstitialEnabled, bool adsAvailable)
        {
            if (data == null || balance == null)
                return AdDecisionResult.Denied(AdDecision.Unavailable, ReasonUnavailable);

            if (!interstitialEnabled)
                return AdDecisionResult.Denied(AdDecision.DeniedByToggle, ReasonToggleOff);

            if (!adsAvailable)
                return AdDecisionResult.Denied(AdDecision.Unavailable, ReasonUnavailable);

            var interval = Math.Max(1, balance.InterstitialEveryNGames);
            if (data.gamesPlayed - data.interstitialLastShownGame < interval)
                return AdDecisionResult.Denied(AdDecision.NotAllowedNow, ReasonNotNow);

            return AdDecisionResult.Allowed();
        }

        /// <summary>分享助力：发起即记录 + 60s 冷却（V2.19）。</summary>
        public static AdDecisionResult CanShare(SaveData data, IClock clock, GameBalance balance)
        {
            if (data == null || clock == null || balance == null)
                return AdDecisionResult.Denied(AdDecision.Unavailable, ReasonUnavailable);

            if (IsOnCooldown(data.lastShareUtcTicks, clock.Now, balance.ShareCooldownSeconds))
                return AdDecisionResult.Denied(AdDecision.CooldownActive, ReasonCooldown);

            return AdDecisionResult.Allowed();
        }

        public static bool IsOnCooldown(long lastUtcTicks, DateTime now, double cooldownSeconds)
        {
            if (lastUtcTicks <= 0 || cooldownSeconds <= 0)
                return false;

            var last = new DateTime(lastUtcTicks, DateTimeKind.Utc);
            var elapsed = (now.ToUniversalTime() - last).TotalSeconds;
            return elapsed < cooldownSeconds;
        }
    }
}
