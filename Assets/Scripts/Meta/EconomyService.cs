using System;
using MergeWater.Core;
using UnityEngine;

namespace MergeWater.Meta
{
    /// <summary>
    /// 局外经济与广告放行的统一入口（M6）。UI/M7 只调用这里，不直接改存档。
    /// 所有变更在成功后立即落盘，保证重启保持（R25、V2.13–V2.16）。
    /// </summary>
    public sealed class EconomyService
    {
        private readonly SaveService _save;
        private readonly IClock _clock;
        private readonly GameBalance _balance;
        private readonly IAdsService _ads;
        private readonly MetaSettings _settings;
        private readonly DailyLimitService _daily;
        private readonly InventoryService _inventory;
        private readonly LeaderboardService _leaderboard;

        public EconomyService(SaveService save, IClock clock, GameBalance balance, IAdsService ads,
            MetaSettings settings)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _ads = ads;
            _settings = settings;

            _daily = new DailyLimitService(save.Data, clock);
            _inventory = new InventoryService(save.Data, _daily);
            _leaderboard = new LeaderboardService(save.Data);
        }

        public InventoryService Inventory => _inventory;

        public DailyLimitService Daily => _daily;

        public LeaderboardService Leaderboard => _leaderboard;

        public bool AdsAvailable => _ads != null && _ads.IsAvailable;

        public bool OfflineGrantAll => _settings == null || _settings.OfflineGrantAll;

        public bool InterstitialEnabled => _settings == null || _settings.InterstitialEnabled;

        public int ItemCount(ItemKind kind) => _inventory.Count(kind);

        public int ItemRemainingToday(ItemKind kind) => _inventory.RemainingToday(kind, _balance);

        public int GiftRemainingToday => _inventory.GiftRemainingToday(_balance);

        // ── 复活 ─────────────────────────────────────────────────────

        public AdDecisionResult CanRevive() =>
            AdsPlacementRules.CanShowRevive(_save.Data, _clock, _balance, AdsAvailable, OfflineGrantAll);

        /// <summary>复活广告播放完成后记账（V2.12 的 90s 全局冷却起点）。</summary>
        public void MarkReviveAdShown()
        {
            _save.Data.lastReviveAdUtcTicks = _clock.Now.ToUniversalTime().Ticks;
            _save.Save();
        }

        /// <summary>按 <paramref name="placeAd"/> 决定是否真的播放广告；离线时直接完成。</summary>
        public void RequestRevive(Action<AdDecisionResult, RewardedResult> onComplete)
        {
            var decision = CanRevive();
            if (!decision.IsAllowed)
            {
                onComplete?.Invoke(decision, RewardedResult.Failed);
                return;
            }

            if (!AdsAvailable)
            {
                onComplete?.Invoke(decision, RewardedResult.Completed);
                return;
            }

            _ads.ShowRewarded(AdPlacement.Revive, result =>
            {
                if (result == RewardedResult.Completed)
                    MarkReviveAdShown();

                onComplete?.Invoke(decision, result);
            });
        }

        // ── 道具领取（看广告 / 分享助力） ─────────────────────────────

        public AdDecisionResult CanGrantItem(ItemKind kind) =>
            AdsPlacementRules.CanGrantItem(kind, _daily, _balance, AdsAvailable, OfflineGrantAll);

        /// <summary>看激励视频领取一次道具（撤销/炸弹/锤子/摇一摇）。</summary>
        public void RequestItemGrant(ItemKind kind, Action<GrantResult, AdDecisionResult> onComplete)
        {
            var decision = CanGrantItem(kind);
            if (!decision.IsAllowed)
            {
                onComplete?.Invoke(GrantResult.DailyCapReached, decision);
                return;
            }

            if (!AdsAvailable)
            {
                var offlineResult = _inventory.Grant(kind, _balance);
                Persist();
                onComplete?.Invoke(offlineResult, decision);
                return;
            }

            _ads.ShowRewarded(AdPlacement.ItemGrant, result =>
            {
                if (result != RewardedResult.Completed)
                {
                    onComplete?.Invoke(GrantResult.Failed, decision);
                    return;
                }

                var grant = _inventory.Grant(kind, _balance);
                Persist();
                onComplete?.Invoke(grant, decision);
            });
        }

        /// <summary>分享助力：受 60s 冷却约束，成功即记一次领取。</summary>
        public GrantResult GrantItemByShare(ItemKind kind, out AdDecisionResult shareDecision)
        {
            shareDecision = AdsPlacementRules.CanShare(_save.Data, _clock, _balance);
            if (!shareDecision.IsAllowed)
                return GrantResult.Failed;

            if (_daily.Remaining(kind, _balance) <= 0)
            {
                shareDecision = AdDecisionResult.Denied(AdDecision.DailyCapReached,
                    AdsPlacementRules.ReasonDailyCap);
                return GrantResult.DailyCapReached;
            }

            _save.Data.lastShareUtcTicks = _clock.Now.ToUniversalTime().Ticks;
            var grant = _inventory.Grant(kind, _balance);
            Persist();
            return grant;
        }

        // ── 摇一摇（R14：仅看激励视频触发，不进入背包） ──────────────

        public AdDecisionResult CanShake() =>
            AdsPlacementRules.CanGrantItem(ItemKind.Shake, _daily, _balance, AdsAvailable, OfflineGrantAll);

        /// <summary>看激励视频触发一次摇一摇；成功只记录当日次数，不发放库存道具。</summary>
        public void RequestShakeAd(Action<bool, AdDecisionResult> onComplete)
        {
            var decision = CanShake();
            if (!decision.IsAllowed)
            {
                onComplete?.Invoke(false, decision);
                return;
            }

            void Finish()
            {
                _daily.RecordGrant(ItemKind.Shake);
                Persist();
                onComplete?.Invoke(true, decision);
            }

            if (!AdsAvailable)
            {
                Finish();
                return;
            }

            _ads.ShowRewarded(AdPlacement.Shake, result =>
            {
                if (result == RewardedResult.Completed)
                    Finish();
                else
                    onComplete?.Invoke(false, decision);
            });
        }

        // ── 大礼包 ───────────────────────────────────────────────────

        public AdDecisionResult CanGrantGift() =>
            AdsPlacementRules.CanGrantGift(_daily, _balance, AdsAvailable, OfflineGrantAll);

        public void RequestGiftGrant(Action<GrantResult, AdDecisionResult> onComplete)
        {
            var decision = CanGrantGift();
            if (!decision.IsAllowed)
            {
                onComplete?.Invoke(GrantResult.DailyCapReached, decision);
                return;
            }

            if (!AdsAvailable)
            {
                var offline = _inventory.GrantGiftBundle(_balance);
                Persist();
                onComplete?.Invoke(offline, decision);
                return;
            }

            _ads.ShowRewarded(AdPlacement.Gift, result =>
            {
                if (result != RewardedResult.Completed)
                {
                    onComplete?.Invoke(GrantResult.Failed, decision);
                    return;
                }

                var grant = _inventory.GrantGiftBundle(_balance);
                Persist();
                onComplete?.Invoke(grant, decision);
            });
        }

        // ── 使用与一次性奖励 ─────────────────────────────────────────

        public SpendResult SpendItem(ItemKind kind)
        {
            var result = _inventory.Spend(kind);
            if (result == SpendResult.Spent)
                Persist();

            return result;
        }

        public GrantResult GrantMilestoneReward(ItemKind kind)
        {
            var result = _inventory.GrantUnlimited(kind);
            if (result == GrantResult.Granted)
                Persist();

            return result;
        }

        // ── 插屏与结算 ───────────────────────────────────────────────

        public AdDecisionResult CanShowInterstitial() =>
            AdsPlacementRules.CanShowInterstitial(_save.Data, _balance, InterstitialEnabled, AdsAvailable);

        public void ShowInterstitial(Action<InterstitialResult> onComplete)
        {
            var decision = CanShowInterstitial();
            if (!decision.IsAllowed)
            {
                onComplete?.Invoke(InterstitialResult.Skipped);
                return;
            }

            _ads.ShowInterstitial(result =>
            {
                if (result == InterstitialResult.Shown)
                {
                    _save.Data.interstitialLastShownGame = _save.Data.gamesPlayed;
                    Persist();
                }

                onComplete?.Invoke(result);
            });
        }

        /// <summary>一局结束：累计局数、更新最高分与连击、写入本地排行。</summary>
        public void OnRoundFinished(int score, int bestCombo)
        {
            var data = _save.Data;
            data.gamesPlayed++;
            data.bestScore = Mathf.Max(data.bestScore, Mathf.Max(0, score));
            data.bestCombo = Mathf.Max(data.bestCombo, Mathf.Max(0, bestCombo));

            if (score > 0)
                _leaderboard.Submit(LeaderboardService.SelfName, score);

            Persist();
        }

        public void RecordTutorialProgress(int roundsSeen, bool dropHintSeen, bool mergeHighlightSeen)
        {
            var data = _save.Data;
            data.tutorialRoundsSeen = Mathf.Max(data.tutorialRoundsSeen, roundsSeen);
            data.tutorialDropHintSeen |= dropHintSeen;
            data.tutorialMergeHighlightSeen |= mergeHighlightSeen;
            Persist();
        }

        public void Persist() => _save.Save();
    }
}
