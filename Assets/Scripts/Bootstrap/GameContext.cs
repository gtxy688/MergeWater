using System;
using MergeWater.Aim;
using MergeWater.Core;
using MergeWater.Field;
using MergeWater.Presentation;
using MergeWater.Session;
using UnityEngine;

namespace MergeWater.Bootstrap
{
    /// <summary>
    /// 组合根（M7）：构造并持有全部服务与模块，负责初始化顺序、隐私门控、重开一局的生命周期，
    /// 以及投放/道具/广告/结算的编排。所有跨模块接线只在这里汇合。
    /// </summary>
    public sealed class GameContext : IDisposable
    {
        private readonly Transform _host;
        private readonly Action<string> _notify;
        private int _roundIndex;

        public GameContext(
            Transform host,
            GameBalance balance,
            GameField field,
            AimController aim,
            HudBinder hud,
            PanelController panels,
            AudioDirector audio,
            FeedbackDirector feedback,
            TutorialDirector tutorial,
            ItemUseController items,
            IClock clock,
            ISaveStore saveStore,
            Meta.MetaSettings metaSettings,
            Meta.IAnalyticsSink analyticsSink = null,
            Action<string> notify = null)
        {
            _host = host;
            _notify = notify ?? (_ => { });

            Balance = balance ?? GameBalance.CreateDefault();
            Field = field;
            Aim = aim;
            Hud = hud;
            Panels = panels;
            Audio = audio;
            Feedback = feedback;
            Tutorial = tutorial;
            Items = items;

            Clock = clock ?? new SystemClock();
            Save = new Meta.SaveService(saveStore ?? new Meta.InMemorySaveStore(), Clock);
            Save.Load();

            Privacy = new Meta.PrivacyGate(Save);
            Analytics = new Meta.AnalyticsService(analyticsSink ?? new Meta.UnityDebugSink(), Clock);
            Ads = new Meta.AdsServiceProxy();
            SettingsService = new Meta.SettingsService(Save);
            Economy = new Meta.EconomyService(Save, Clock, Balance, Ads, metaSettings);
            Share = new Meta.ShareService(Save.Data, Save, Clock, Balance);
            Leaderboard = Economy.Leaderboard;

            SettingsService.Changed += ApplySettingsToAudio;
            ApplySettingsToAudio();

            Items?.Initialize(this);
            Tutorial?.Initialize(this);

            Aim.DropRequested += OnDropRequested;
            Aim.ItemTargetRequested += OnItemTargetRequested;
        }

        public GameBalance Balance { get; }
        public IClock Clock { get; }
        public Meta.SaveService Save { get; }
        public Meta.PrivacyGate Privacy { get; }
        public Meta.AnalyticsService Analytics { get; }
        public Meta.AdsServiceProxy Ads { get; }
        public Meta.EconomyService Economy { get; }
        public Meta.SettingsService SettingsService { get; }
        public Meta.ShareService Share { get; }
        public Meta.LeaderboardService Leaderboard { get; }

        public GameField Field { get; }
        public AimController Aim { get; }
        public HudBinder Hud { get; }
        public PanelController Panels { get; }
        public AudioDirector Audio { get; }
        public FeedbackDirector Feedback { get; }
        public TutorialDirector Tutorial { get; }
        public ItemUseController Items { get; }

        public RoundSession Session { get; private set; }

        public bool IsRoundActive => Session != null;

        /// <summary>本局是否还有复活机会（每局次数 + 广告冷却双重校验）。</summary>
        public bool CanReviveNow => Session != null && Session.CanRevive && Economy.CanRevive().IsAllowed;

        public void Notify(string message) => _notify(message ?? string.Empty);

        /// <summary>隐私同意后才初始化埋点与广告（R20）。</summary>
        public void ApplyConsent()
        {
            if (Ads.Inner is Meta.MockAdsService)
            {
                Analytics.Initialize();
                return;
            }

            var adsHost = new GameObject("AdsAdapter");
            if (_host != null)
                adsHost.transform.SetParent(_host, false);

            var mock = adsHost.AddComponent<Meta.MockAdsService>();
            mock.Initialize();
            Ads.Inner = mock;

            Analytics.Initialize();
        }

        public RoundSession NewRound()
        {
            if (!Privacy.IsAccepted)
            {
                Notify("请先同意隐私政策");
                return null;
            }

            Hud?.Unbind();
            Panels?.HideAll();

            // 旧的局必须解绑场地事件，否则重开后两局会同时计分并重复写存档。
            Session?.Dispose();
            Session = null;

            var session = new RoundSession(
                Balance,
                Field,
                Analytics,
                Save.Data.bestScore,
                NextSeed(),
                onBestScoreChanged: OnBestScoreChanged,
                onMilestoneReward: OnMilestoneReward);

            Session = session;

            Aim.SetDropBounds(-Balance.FieldHalfWidth, Balance.FieldHalfWidth);
            Aim.SetDropGeometry(Balance.DropSpawnY, Balance.FieldFloorY, -9.81f, Balance.AimPreviewMaxSeconds);
            Aim.SetInteractable(false);

            if (Hud != null)
                Hud.Bind(session, Aim, Balance, Save.Data.bestScore);

            session.StartRound();
            SyncAimRadius();

            Tutorial?.OnRoundStarted(session, Save.Data.tutorialRoundsSeen);
            return session;
        }

        /// <summary>主动结束本局（结算页的「结束」入口，也用于调试）。</summary>
        public void EndRound()
        {
            Session?.EndRound();
        }

        /// <summary>每帧由 <see cref="GameBootstrapper"/> 驱动。</summary>
        public void Tick(float deltaSeconds)
        {
            Session?.Tick(deltaSeconds);
            SyncAimRadius();
        }

        public void SetInputBlocked(bool blocked)
        {
            if (Aim == null || Session == null)
            {
                Aim?.SetInteractable(false);
                return;
            }

            // Ready 阶段必须可交互，否则玩家无法完成第一次投放；Playing 才允许投放与道具。
            var playable = Session.Phase == RoundPhase.Ready || Session.Phase == RoundPhase.Playing;
            Aim.SetInteractable(!blocked && playable);
        }

        /// <summary>结算流程：写入最高分/排行/局数，并按 V2.16 决定插屏。</summary>
        public void CompleteRound()
        {
            if (Session == null)
                return;

            var bestCombo = Save.Data.bestCombo;
            if (Session.Snapshot.Combo > bestCombo)
                bestCombo = Session.Snapshot.Combo;

            Economy.OnRoundFinished(Session.Score, bestCombo);

            var decision = Economy.CanShowInterstitial();
            if (!decision.IsAllowed)
                return;

            Panels?.SetAdOverlay(true, "插屏广告（测试位）");
            Economy.ShowInterstitial(_ => Panels?.SetAdOverlay(false, null));
        }

        /// <summary>
        /// 复活编排：先做每局次数与广告冷却校验，再播放一次广告，最后才应用复活状态。
        /// 广告只播放一次（每局次数在 <see cref="RoundSession"/> 内校验）。
        /// </summary>
        public void RequestRevive(Action<bool, string> onResolved = null)
        {
            if (Session == null)
            {
                onResolved?.Invoke(false, "当前没有进行中的对局");
                return;
            }

            if (!Session.CanRevive)
            {
                Notify("本局复活次数已用完");
                onResolved?.Invoke(false, "本局复活次数已用完");
                return;
            }

            var decision = Economy.CanRevive();
            if (!decision.IsAllowed)
            {
                Notify(decision.Reason);
                onResolved?.Invoke(false, decision.Reason);
                return;
            }

            if (Ads.IsAvailable)
                Panels?.SetAdOverlay(true, "激励视频（测试位）：复活");

            Economy.RequestRevive((_, result) =>
            {
                Panels?.SetAdOverlay(false, null);

                if (result != RewardedResult.Completed)
                {
                    Notify("广告未完成，复活失败");
                    onResolved?.Invoke(false, "广告未完成");
                    return;
                }

                var outcome = Session.ApplyRevive();
                if (outcome == ReviveOutcome.Applied)
                {
                    Feedback?.PlayRevive();
                    Panels?.Hide(PanelId.Settlement);
                    RefreshBadges();
                    onResolved?.Invoke(true, null);
                }
                else
                {
                    Feedback?.PlayReviveFailed();
                    Notify($"复活失败：{outcome}");
                    onResolved?.Invoke(false, outcome.ToString());
                }
            });
        }

        /// <summary>看激励视频领取一个道具；<paramref name="onGranted"/> 用于「领取后立即使用」的连续操作。</summary>
        public void RequestItemGrant(ItemKind kind, Action onGranted = null)
        {
            var decision = Economy.CanGrantItem(kind);
            if (!decision.IsAllowed)
            {
                Notify(decision.Reason);
                return;
            }

            if (Ads.IsAvailable)
                Panels?.SetAdOverlay(true, $"激励视频（测试位）：领取{kind}");

            Economy.RequestItemGrant(kind, (grant, _) =>
            {
                Panels?.SetAdOverlay(false, null);

                if (grant == GrantResult.Granted)
                {
                    Feedback?.PlayClaim();
                    Notify($"获得 1 个{kind}");
                    RefreshBadges();
                    onGranted?.Invoke();
                }
                else
                {
                    Notify("领取失败，请稍后再试");
                }
            });
        }

        public void RequestShake()
        {
            var decision = Economy.CanShake();
            if (!decision.IsAllowed)
            {
                Notify(decision.Reason);
                return;
            }

            if (Ads.IsAvailable)
                Panels?.SetAdOverlay(true, "激励视频（测试位）：摇一摇");

            Economy.RequestShakeAd((granted, reason) =>
            {
                Panels?.SetAdOverlay(false, null);

                if (!granted)
                {
                    Notify(reason.Reason);
                    return;
                }

                Field.ApplyShakeShuffle(Balance.ShakeImpulse, Environment.TickCount);
                Feedback?.PlayItemUse(ItemKind.Shake);
                Notify("摇一摇：水果重新落位");
                RefreshBadges();
            });
        }

        public void RequestGift()
        {
            var decision = Economy.CanGrantGift();
            if (!decision.IsAllowed)
            {
                Notify(decision.Reason);
                return;
            }

            if (Ads.IsAvailable)
                Panels?.SetAdOverlay(true, "激励视频（测试位）：大礼包");

            Economy.RequestGiftGrant((grant, _) =>
            {
                Panels?.SetAdOverlay(false, null);

                if (grant == GrantResult.Granted)
                {
                    Feedback?.PlayClaim();
                    Notify("大礼包：撤销 / 炸弹 / 锤子 各 +1");
                    RefreshBadges();
                }
                else
                {
                    Notify("大礼包今日已领取");
                }
            });
        }

        public void RequestShare()
        {
            var score = Session?.Score ?? 0;
            var result = Share.ShareChallenge(score, Save.Data.bestCombo, out var text);

            if (result == ShareResult.Shared)
                Analytics.Track(AnalyticsEventNames.ShareStart, AnalyticsParam.Int("score", score));

            Notify(text);
        }

        public void RecordSettlementGuides()
        {
            var rounds = Save.Data.tutorialRoundsSeen + 1;
            Economy.RecordTutorialProgress(rounds, Save.Data.tutorialDropHintSeen,
                Save.Data.tutorialMergeHighlightSeen);
        }

        public void MarkTutorialFlags(bool dropHintSeen, bool mergeHighlightSeen)
        {
            Economy.RecordTutorialProgress(Save.Data.tutorialRoundsSeen, dropHintSeen, mergeHighlightSeen);
        }

        public void RefreshBadges()
        {
            if (Panels == null)
                return;

            Panels.SetBadge(BadgeId.Undo, Economy.ItemRemainingToday(ItemKind.Undo) > 0);
            Panels.SetBadge(BadgeId.Bomb, Economy.ItemRemainingToday(ItemKind.Bomb) > 0);
            Panels.SetBadge(BadgeId.Hammer, Economy.ItemRemainingToday(ItemKind.Hammer) > 0);
            Panels.SetBadge(BadgeId.Shake, Economy.ItemRemainingToday(ItemKind.Shake) > 0);
            Panels.SetBadge(BadgeId.Gift, Economy.GiftRemainingToday > 0);
            Panels.SetBadge(BadgeId.Leaderboard, Leaderboard.IsSelfBeaten);
        }

        public void Dispose()
        {
            SettingsService.Changed -= ApplySettingsToAudio;

            if (Aim != null)
            {
                Aim.DropRequested -= OnDropRequested;
                Aim.ItemTargetRequested -= OnItemTargetRequested;
            }

            Hud?.Unbind();
            Session?.Dispose();
            Save?.Save();
            Session = null;
        }

        private void OnDropRequested(float x)
        {
            // Ready 阶段的第一次投放必须放行，否则玩家无法开始对局。
            if (Session == null || !Session.CanAcceptDrop)
                return;

            if (!Session.ReleaseDrop(x))
                return;

            Feedback?.PlayDrop(new Vector2(x, Balance.DropSpawnY));
            Tutorial?.OnDropPerformed(Session);
            SyncAimRadius();
        }

        private void OnItemTargetRequested(ItemKind kind, Vector2 point)
        {
            Items?.ApplyTargetedItem(kind, point);
        }

        private void OnBestScoreChanged(int bestScore)
        {
            Save.Data.bestScore = Mathf.Max(Save.Data.bestScore, bestScore);
            Leaderboard.Submit(Meta.LeaderboardService.SelfName, bestScore);
            Save.Save();
        }

        private void OnMilestoneReward(StageMilestone milestone)
        {
            var result = Economy.GrantMilestoneReward(milestone.Reward);
            if (result == GrantResult.Granted)
                RefreshBadges();
        }

        private void ApplySettingsToAudio()
        {
            if (Audio == null)
                return;

            Audio.SetSfxEnabled(SettingsService.SfxEnabled);
            Audio.SetMusicEnabled(SettingsService.MusicEnabled);
            Audio.SetVibrateEnabled(SettingsService.VibrateEnabled);

            if (SettingsService.MusicEnabled)
                Audio.PlayMusic();
        }

        private void SyncAimRadius()
        {
            if (Session == null || Aim == null)
                return;

            var tier = Balance.GetTier(Session.CurrentLevel);
            if (tier.IsValid)
                Aim.SetDropRadius(tier.Radius);
        }

        private int NextSeed()
        {
            _roundIndex++;
            return unchecked(Environment.TickCount * 31 + _roundIndex * 7919);
        }
    }
}
