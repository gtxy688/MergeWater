using System;
using System.Collections.Generic;
using MergeWater.Aim;
using MergeWater.Core;
using MergeWater.Field;
using MergeWater.Presentation;
using UnityEngine;

namespace MergeWater.Bootstrap
{
    /// <summary>
    /// 场景入口与组合根宿主。初始化顺序：加载存档 → 隐私判定（同意前零上报零广告）→ 同意后
    /// 初始化埋点与广告 → 直接进入对局（R20、R26，无独立开始页）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameBootstrapper : MonoBehaviour
    {
        [SerializeField] private GameField field;
        [SerializeField] private AimController aim;
        [SerializeField] private HudBinder hud;
        [SerializeField] private PanelController panels;
        [SerializeField] private AudioDirector audio;
        [SerializeField] private FeedbackDirector feedback;
        [SerializeField] private TutorialDirector tutorial;
        [SerializeField] private ItemUseController items;
        [SerializeField] private GameBalanceAsset balanceAsset;
        [SerializeField] private Meta.MetaSettings metaSettings;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private bool buildPresentationIfMissing = true;

        private HudView _view;
        private int _leaderboardPage = 1;

        /// <summary>测试接缝：为空时使用 <see cref="Meta.FileSaveStore"/>（persistentDataPath）。</summary>
        public ISaveStore SaveStoreOverride { get; set; }

        /// <summary>测试接缝：为空时使用 <see cref="SystemClock"/>。</summary>
        public IClock ClockOverride { get; set; }

        /// <summary>测试接缝：为空时使用 <see cref="Meta.UnityDebugSink"/>。</summary>
        public Meta.IAnalyticsSink AnalyticsSinkOverride { get; set; }

        public GameContext Context { get; private set; }

        public HudView View => _view;

        public event Action<GameContext> ContextReady;

        private void Awake()
        {
            ResolveOrBuildPresentation();
            ResolveReferences();

            Context = BuildContext();

            if (tutorial != null)
                tutorial.Configure(panels, _view != null ? _view.tutorialArrow : null);

            HookPanels();
            ContextReady?.Invoke(Context);
        }

        private void Start()
        {
            if (Context == null)
                return;

            panels?.SetVersion(Application.version);

            if (!Context.Privacy.IsAccepted)
            {
                panels?.Show(PanelId.Privacy);
                Context.SetInputBlocked(true);
                return;
            }

            BeginAfterConsent();
        }

        private void Update()
        {
            if (Context == null)
                return;

            Context.Tick(Time.deltaTime);

            var adShowing = Context.Ads.IsShowing;
            var panelOpen = panels != null && panels.CurrentPanel != PanelId.None;
            Context.SetInputBlocked(adShowing || panelOpen);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
                Context?.Save.Save();
        }

        private void OnDestroy()
        {
            Context?.Dispose();
            Context = null;
        }

        // ── 装配 ─────────────────────────────────────────────────────

        /// <summary>场景构建工具用：一次性注入全部序列化引用。</summary>
        public void ConfigureReferences(GameField gameField, AimController aimController, HudBinder hudBinder,
            PanelController panelController, AudioDirector audioDirector, FeedbackDirector feedbackDirector,
            TutorialDirector tutorialDirector, ItemUseController itemUseController,
            GameBalanceAsset balance, Meta.MetaSettings settings, Camera camera)
        {
            field = gameField;
            aim = aimController;
            hud = hudBinder;
            panels = panelController;
            audio = audioDirector;
            feedback = feedbackDirector;
            tutorial = tutorialDirector;
            items = itemUseController;
            balanceAsset = balance;
            metaSettings = settings;
            targetCamera = camera;
            _view = hudBinder != null ? hudBinder.GetComponent<HudView>() : GetComponentInChildren<HudView>(true);
        }

        private void ResolveReferences()
        {
            if (field == null)
                field = GetComponentInChildren<GameField>(true);

            if (aim == null)
                aim = GetComponent<AimController>();

            if (aim == null)
                aim = GetComponentInChildren<AimController>(true);

            if (items == null)
                items = GetComponent<ItemUseController>();

            if (items == null)
                items = GetComponentInChildren<ItemUseController>(true);

            if (tutorial == null)
                tutorial = GetComponentInChildren<TutorialDirector>(true);

            if (targetCamera == null)
                targetCamera = Camera.main;
        }

        private void ResolveOrBuildPresentation()
        {
            _view = GetComponentInChildren<HudView>(true);
            if (panels == null)
                panels = GetComponentInChildren<PanelController>(true);
            if (hud == null)
                hud = GetComponentInChildren<HudBinder>(true);
            if (feedback == null)
                feedback = GetComponentInChildren<FeedbackDirector>(true);
            if (audio == null)
                audio = GetComponentInChildren<AudioDirector>(true);

            var missing = panels == null || hud == null || feedback == null || audio == null;
            if (!missing || !buildPresentationIfMissing)
                return;

            var circle = Resources.Load<Sprite>("Placeholder/fruit_circle");
            var arrow = Resources.Load<Sprite>("Placeholder/ui_arrow");
            _view = HudBuilder.Build(transform, circle, arrow, out _);

            panels = GetComponentInChildren<PanelController>(true);
            hud = GetComponentInChildren<HudBinder>(true);
            feedback = GetComponentInChildren<FeedbackDirector>(true);
            audio = GetComponentInChildren<AudioDirector>(true);
        }

        private GameContext BuildContext()
        {
            var balance = balanceAsset != null ? balanceAsset.ToBalance() : GameBalance.CreateDefault();

            if (field == null)
            {
                Debug.LogError("[GameBootstrapper] 场景缺少 GameField 引用，无法初始化对局。");
                return null;
            }

            field.Configure(balance, Resources.Load<Sprite>("Placeholder/fruit_circle"), null);

            if (aim != null)
                aim.SetCamera(targetCamera != null ? targetCamera : Camera.main);

            var settings = metaSettings;

            return new GameContext(
                transform,
                balance,
                field,
                aim,
                hud,
                panels,
                audio,
                feedback,
                tutorial,
                items,
                ClockOverride ?? (IClock)new SystemClock(),
                SaveStoreOverride ?? (ISaveStore)new Meta.FileSaveStore(),
                settings,
                AnalyticsSinkOverride,
                message => panels?.ShowToast(message));
        }

        private void HookPanels()
        {
            if (panels == null)
                return;

            panels.PrivacyAcceptClicked += OnAcceptPrivacy;
            panels.PrivacyDeclineClicked += () => Context.Notify("需要同意隐私政策才能开始游戏");
            panels.RetryClicked += OnRetry;
            panels.ReviveClicked += OnRevive;
            panels.ShareClicked += OnShare;
            panels.SettingsClicked += OnSettings;
            panels.LeaderboardClicked += () => ShowLeaderboard(1);
            panels.LeaderboardCloseClicked += () => panels.Hide(PanelId.Leaderboard);
            panels.LeaderboardNextPageClicked += () => ShowLeaderboard(_leaderboardPage + 1);
            panels.ItemEntryClicked += OnItemEntryClicked;
            panels.ClearCacheClicked += OnClearCache;
            panels.PrivacyPolicyClicked += () => Context.Notify("隐私政策：MVP 为离线 Demo，不收集可识别个人信息");
            panels.UserAgreementClicked += () => Context.Notify("用户协议：Demo 版本，仅用于演示与作品集");
            panels.AntiAddictionClicked += () => Context.Notify("实名 / 防沉迷：正式上线时在此接入对应入口");

            panels.SfxToggled += value =>
            {
                Context.SettingsService.SetSfx(value);
                TrackSetting("sfx", value);
            };
            panels.MusicToggled += value =>
            {
                Context.SettingsService.SetMusic(value);
                TrackSetting("music", value);
            };
            panels.VibrateToggled += value =>
            {
                Context.SettingsService.SetVibrate(value);
                TrackSetting("vibrate", value);
            };
        }

        // ── 流程 ─────────────────────────────────────────────────────

        private void OnAcceptPrivacy()
        {
            Context.Privacy.Accept();
            BeginAfterConsent();
        }

        private void BeginAfterConsent()
        {
            Context.ApplyConsent();
            panels?.HideAll();
            panels?.SetToggleStates(Context.SettingsService.SfxEnabled, Context.SettingsService.MusicEnabled,
                Context.SettingsService.VibrateEnabled);

            BeginRound();
            Context.RefreshBadges();
            Context.Notify("按住屏幕拖动选落点，松手投放");
        }

        private void BeginRound()
        {
            var session = Context.NewRound();
            if (session != null)
                session.Events.PhaseChanged += OnPhaseChanged;
        }

        private void OnPhaseChanged(RoundPhase from, RoundPhase to)
        {
            if (to != RoundPhase.Reviving && to != RoundPhase.GameOver)
                return;

            tutorial?.OnSettlementShown(Context.Save.Data.tutorialRoundsSeen);
        }

        private void OnRetry()
        {
            Context.Feedback?.PlayButton();
            Context.CompleteRound();
            Context.RecordSettlementGuides();
            BeginRound();
            Context.RefreshBadges();
        }

        private void OnRevive()
        {
            Context.Feedback?.PlayButton();
            Context.RequestRevive();
        }

        private void OnShare()
        {
            Context.Feedback?.PlayButton();
            Context.RequestShare();
        }

        private void OnSettings()
        {
            Context.Feedback?.PlayButton();
            panels.SetToggleStates(Context.SettingsService.SfxEnabled, Context.SettingsService.MusicEnabled,
                Context.SettingsService.VibrateEnabled);
            panels.Show(PanelId.Settings);
        }

        private void OnItemEntryClicked(ItemKind kind)
        {
            Context.Feedback?.PlayButton();
            Context.Items?.OnEntryClicked(kind);
        }

        private void OnClearCache()
        {
            Context.SettingsService.ClearCache();
            panels.HideAll();
            panels.Show(PanelId.Privacy);
            Context.Notify("已清除本地缓存，请重新确认隐私政策");
        }

        private void ShowLeaderboard(int page)
        {
            _leaderboardPage = Math.Max(1, page);
            IReadOnlyList<LeaderboardEntry> entries = Context.Leaderboard.GetTop(20, _leaderboardPage);

            panels.SetLeaderboard(entries, _leaderboardPage);
            panels.Show(PanelId.Leaderboard);

            Context.Analytics.Track(AnalyticsEventNames.LeaderboardView,
                AnalyticsParam.Int("page", _leaderboardPage));
            Context.RefreshBadges();
        }

        private void TrackSetting(string key, bool value)
        {
            Context.Analytics.Track(AnalyticsEventNames.SettingChanged,
                AnalyticsParam.Text("key", key), AnalyticsParam.Text("value", value ? "on" : "off"));
        }
    }
}
