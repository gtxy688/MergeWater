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
        private readonly Meta.AdsRuntimeConfig _adsConfig;
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
            Action<string> notify = null,
            Meta.AdsRuntimeConfig adsConfig = null)
        {
            _host = host;
            _notify = notify ?? (_ => { });
            _adsConfig = adsConfig ?? new Meta.AdsRuntimeConfig();

            Balance = balance ?? GameBalance.CreateDefault();
            Field = field;
            Aim = aim;
            Hud = hud;

            // 安全区（刘海/灵动岛/微信右上角胶囊）：微信真机读 WX 接口，其余平台自动回退 Screen.safeArea。
            // 顶栏按**实测**遮挡下移——设计稿的「假定胶囊区」在带刘海的机器上不够用
            //（2026-09-13 需求方真机截图：分数被刘海挡住、右上齿轮与胶囊重叠）。
            Hud?.SetSafeAreaSource(new Meta.WxSafeAreaSource());
            Panels = panels;
            Audio = audio;
            Feedback = feedback;
            Tutorial = tutorial;
            Items = items;

            Clock = clock ?? new SystemClock();
            Save = new Meta.SaveService(saveStore ?? new Meta.InMemorySaveStore(), Clock);
            Save.Load();

            Privacy = new Meta.PrivacyGate(Save);
            // 默认不往 Console 写埋点（2026-09-13 需求方要求清掉运行期调试信息，见 NullAnalyticsSink）；
            // 需要看埋点日志时由 M7 注入 UnityDebugSink。
            Analytics = new Meta.AnalyticsService(analyticsSink ?? new Meta.NullAnalyticsSink(), Clock);
            Ads = new Meta.AdsServiceProxy();
            SettingsService = new Meta.SettingsService(Save);
            Economy = new Meta.EconomyService(Save, Clock, Balance, Ads, metaSettings);
            Share = new Meta.ShareService(Save.Data, Save, Clock, Balance);
            Leaderboard = Economy.Leaderboard;

            SettingsService.Changed += ApplySettingsToAudio;
            ApplySettingsToAudio();

            Feedback?.SetBalance(Balance);
            Items?.Initialize(this);
            Tutorial?.Initialize(this);

            // 运行时注入（不依赖编辑器期装配）：让预览停在堆叠表面上。
            Aim.SetObstacleProvider(Field);

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

        /// <summary>
        /// 广告非成功结果的**统一提示**（2026-09-14 需求方要求六个入口文案一致）：
        /// 「用户中途关闭」是自己的选择，用温和措辞；失败 / 无广告才让玩家「稍后再试」。
        ///
        /// <para>措辞只能用**已烘进字体**的字（硬约束 9）：`Assets/Fonts/ChineseUI SDF.asset` 里没有
        /// 「消」字，Static 模式下会渲染成空白，所以这里用「未看完广告」而不是「已取消观看广告」；
        /// 想换成别的措辞，先重跑 `MergeWater/Font/1. 收集字符` + `/2. 烘焙中文 TMP 字体资产`。</para>
        /// </summary>
        public static string AdFailureMessage(RewardedResult result) =>
            result == RewardedResult.Skipped ? "未看完广告" : "暂无可用广告，请稍后再试";

        /// <summary>
        /// 隐私同意后才初始化埋点与广告（R20）。适配器由 <see cref="Meta.AdsAdapterFactory"/> 按模式创建：
        /// Mock（默认，无需广告位）或真实微信激励视频（需要 adUnitId + 微信小游戏真机；否则自动退回 Mock）。
        /// </summary>
        public void ApplyConsent()
        {
            // 已经注入过适配器就不覆盖：测试注入的 Mock、或上一次同意时已建好的那个。
            if (!(Ads.Inner is Meta.NullAdsService))
            {
                Ads.Initialize();
                Analytics.Initialize();
                return;
            }

            var adsHost = new GameObject("AdsAdapter");
            if (_host != null)
                adsHost.transform.SetParent(_host, false);

            Ads.Inner = Meta.AdsAdapterFactory.Create(_adsConfig.Mode, _adsConfig.WeChatAdUnitId,
                _adsConfig.MockResult, _adsConfig.MockRewardedSeconds, adsHost.transform);
            Ads.Initialize();

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

            Aim.SetDropBounds(-Field.PlayHalfWidth, Field.PlayHalfWidth);
            Aim.SetDropGeometry(Balance.DropSpawnY, Field.PlayFloorY, -9.81f, Balance.AimPreviewMaxSeconds);
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
                    // 中途关闭 / 失败 / 无广告：走统一文案（2026-09-14）
                    var reason = AdFailureMessage(result);
                    Notify(reason);
                    onResolved?.Invoke(false, reason);
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

            Economy.RequestItemGrant(kind, (grant, _, ad) =>
            {
                Panels?.SetAdOverlay(false, null);

                if (grant == GrantResult.Granted)
                {
                    Feedback?.PlayClaim();
                    Notify($"获得 1 个{kind}");
                    RefreshBadges();
                    onGranted?.Invoke();
                }
                else if (ad != RewardedResult.Completed)
                {
                    // 取消 / 无广告：与复活同一套文案
                    Notify(AdFailureMessage(ad));
                }
                else
                {
                    // 广告确实看完了，是每日上限或库存拒绝的
                    Notify(grant == GrantResult.DailyCapReached ? "今日领取次数已用完" : "领取失败，请稍后再试");
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

            Economy.RequestShakeAd((granted, reason, ad) =>
            {
                Panels?.SetAdOverlay(false, null);

                if (!granted)
                {
                    // 广告没看完 → 统一文案；广告看完了但被放行规则拒绝 → 说清原因
                    Notify(ad != RewardedResult.Completed ? AdFailureMessage(ad) : reason.Reason);
                    return;
                }

                Field.ApplyShakeShuffle(Balance.ShakeImpulse, Environment.TickCount);
                Feedback?.PlayItemUse(ItemKind.Shake);
                Notify("摇一摇：行星重新落位");
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

            Economy.RequestGiftGrant((grant, _, ad) =>
            {
                Panels?.SetAdOverlay(false, null);

                if (grant == GrantResult.Granted)
                {
                    Feedback?.PlayClaim();
                    Notify("大礼包：撤销 / 炸弹 / 锤子 各 +1");
                    RefreshBadges();
                }
                else if (ad != RewardedResult.Completed)
                {
                    Notify(AdFailureMessage(ad));
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
            // 需求方（2026-09-12）：连续合成不再发放奖励——道具只能通过（激励视频）广告获取，
            // 因此这里不授予任何物品；里程碑事件仍会发布，供埋点/阶段推进使用。
        }

        private void ApplySettingsToAudio()
        {
            if (Audio == null)
                return;

            Audio.SetSfxEnabled(SettingsService.SfxEnabled);
            Audio.SetMusicEnabled(SettingsService.MusicEnabled);
            Audio.SetVibrateEnabled(SettingsService.VibrateEnabled);

            // 音量条与开关正交：开关关掉仍然保留音量值，下次打开沿用。
            Audio.SetSfxVolume(SettingsService.SfxVolume);
            Audio.SetMusicVolume(SettingsService.MusicVolume);

            // 音乐：这里只保证「在放」——PlayMusic 是**幂等**的，不会把正在播的曲子重头开始；
            // 只有「关闭音乐后再打开」（SetMusicEnabled(false) → (true)）才会从头播放（需求方 2026-09-14）。
            // 注意本方法挂在 SettingsService.Changed 上，拖音量条时会被**逐帧**调用。
            if (SettingsService.MusicEnabled)
                Audio.PlayMusic();
        }

        private void SyncAimRadius()
        {
            if (Session == null || Aim == null)
                return;

            var level = Session.CurrentLevel;
            var tier = Balance.GetTier(level);
            if (!tier.IsValid)
                return;

            Aim.SetDropRadius(tier.Radius);

            // 预览的下落时间必须按该等级的实际重力算（V2.28 重力倍率随等级变化），
            // 否则预测线要么提前截断、要么画过地面。
            var effectiveGravity = Physics2D.gravity.y * Balance.GetGravityScale(level);
            Aim.SetDropGeometry(Balance.DropSpawnY, Field != null ? Field.PlayFloorY : Balance.FieldFloorY,
                effectiveGravity, Balance.AimPreviewMaxSeconds);
        }

        /// <summary>
        /// 方案 B「屏幕即边框」：按相机可视范围设置场地边界（左右墙贴屏幕左右边缘、地面按
        /// <c>GameBalance.FloorScreenInset</c> 从屏幕底边抬起一段），并让危险线与投放范围跟着走。
        /// 地面不贴死屏幕底：否则水果落地后紧贴最后一行像素，观感上像被下边缘切掉（V2.42）。
        /// 没有正交相机时保持数值表的设计值。
        /// </summary>
        public void SetPlayAreaFromCamera(Camera camera)
        {
            SetPlayAreaFromCamera(camera, null);
        }

        /// <summary>
        /// 同上，但可临时覆盖地面抬升量（<paramref name="floorInsetOverride"/> 有值时优先）。
        /// 仅供**编辑器调参工具**在 Play 模式下实时预览手感用，不改变运行时权威数值的来源
        /// （权威值仍是 <c>GameBalance.FloorScreenInset</c>）；传 null 即恢复常规行为。
        /// </summary>
        public void SetPlayAreaFromCamera(Camera camera, float? floorInsetOverride)
        {
            if (camera == null || !camera.orthographic || Field == null)
                return;

            var halfHeight = camera.orthographicSize;
            var halfWidth = halfHeight * camera.aspect;
            var screenBottom = camera.transform.position.y - halfHeight;
            var inset = floorInsetOverride ?? Balance.FloorScreenInset;
            var floorY = screenBottom + Mathf.Max(0f, inset);

            Field.SetPlayArea(halfWidth, floorY);
            Hud?.SetPlayAreaHalfWidth(halfWidth);

            if (Session != null)
            {
                Aim?.SetDropBounds(-Field.PlayHalfWidth, Field.PlayHalfWidth);
                Aim?.SetDropGeometry(Balance.DropSpawnY, Field.PlayFloorY, -9.81f, Balance.AimPreviewMaxSeconds);
            }
        }

        private int NextSeed()
        {
            _roundIndex++;
            return unchecked(Environment.TickCount * 31 + _roundIndex * 7919);
        }
    }
}
