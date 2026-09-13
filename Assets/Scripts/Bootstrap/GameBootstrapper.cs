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
        // new：字段名 audio 会遮蔽已废弃的 Component.audio，显式声明隐藏以消除 CS0108 警告。
        [SerializeField] private new AudioDirector audio;
        [SerializeField] private FeedbackDirector feedback;
        [SerializeField] private TutorialDirector tutorial;
        [SerializeField] private ItemUseController items;
        [SerializeField] private GameBalanceAsset balanceAsset;
        [SerializeField] private Meta.MetaSettings metaSettings;
        [SerializeField] private Camera targetCamera;

        // Demo 版先不做隐私门控（需求方确认）；正式发布前必须改回 true，走 R20 的同意流程。
        [SerializeField] private bool requirePrivacyConsent;

        private HudView _view;

        private int _leaderboardPage = 1;

        private float _loadingElapsed;

        private bool _loading;

        private bool _loadingReady;

        private float _loadingReadyAt;


        /// <summary>进度满后多久没点击就兜底自动开始（防止玩家/自动化卡在加载页）。</summary>

        private const float AutoStartGraceSeconds = 12f;


        /// <summary>
        /// 加载进度单帧最多计入的秒数。进度按真实帧时间累计，但编辑器/低配设备上单帧
        /// deltaTime 可能达到 0.5~3 s（首帧初始化、GC 停顿、失焦后恢复），2 s 的最短加载
        /// 会被一两帧走完——玩家看到的就是「进度条永远是 100%、不会动」。
        /// 逐帧封顶后，进度条至少有 <c>LoadingMinSeconds / MaxLoadingStepSeconds</c> 帧可见推进。
        /// </summary>
        private const float MaxLoadingStepSeconds = 0.15f;


        /// <summary>加载页进度满后是否需要点击才开始（默认 true：保证玩家一定看到加载页；测试接缝可关）。</summary>

        public bool RequireTapToStart { get; set; } = true;


        /// <summary>是否要求隐私同意后才开局（正式发布为 true；Demo 默认 false，先不做门控）。</summary>

        public bool RequirePrivacyConsent { get => requirePrivacyConsent; set => requirePrivacyConsent = value; }

        /// <summary>测试接缝：为空时使用 <see cref="Meta.FileSaveStore"/>（persistentDataPath）。</summary>
        public ISaveStore SaveStoreOverride { get; set; }

        /// <summary>测试接缝：为空时使用 <see cref="SystemClock"/>。</summary>
        public IClock ClockOverride { get; set; }

        /// <summary>测试接缝：为空时使用 <see cref="Meta.UnityDebugSink"/>。</summary>
        public Meta.IAnalyticsSink AnalyticsSinkOverride { get; set; }

        /// <summary>测试接缝：加载页最短展示时长覆盖（≥0 生效；0 表示下一帧即完成加载）。</summary>
        public float LoadingMinSecondsOverride { get; set; } = -1f;

        public GameContext Context { get; private set; }

        public HudView View => _view;

        public event Action<GameContext> ContextReady;

        private void Awake()
        {
            ResolveOrBuildPresentation();
            ResolveReferences();

            Context = BuildContext();

            // 震屏目标与手感数值在运行时确定（场景构建期拿不到最终相机与数值）。
            if (Context != null)
            {
                var shakeCamera = targetCamera != null ? targetCamera : Camera.main;
                Context.Feedback?.SetShakeTarget(shakeCamera != null ? shakeCamera.transform : null);
                Context.Feedback?.SetBalance(Context.Balance);

                // 方案 B「屏幕即边框」：场地边界按相机可视范围确定（尺寸/宽高比在构建期还不确定）。
                Context.SetPlayAreaFromCamera(shakeCamera);
            }

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
            BeginEntryFlow();
        }

        /// <summary>
        /// 入口流程：加载页（最短 <c>GameBalance.LoadingMinSeconds</c>）→ 点击开始 → 隐私门控（可选）→ 直接开局（R26）。
        /// 测试可重复调用以复现不同的门控配置。
        /// </summary>
        public void BeginEntryFlow()
        {
            _loadingElapsed = 0f;
            _loading = true;
            _loadingReady = false;
            _loadingReadyAt = 0f;

            Context.SetInputBlocked(true);
            panels?.Show(PanelId.Loading);
            panels?.SetLoadingProgress(0f);
        }

        /// <summary>
        /// 玩家点击「开始」（或测试注入）。加载页进度满后停留在此状态，
        /// 直到点击或超过 <see cref="AutoStartGraceSeconds"/> 兜底自动开始。
        /// </summary>
        public void NotifyStartTapped()
        {
            if (!_loading || !_loadingReady)
                return;

            _loading = false;
            FinishEntryFlow();
        }

        private void Update()
        {
            if (Context == null)
                return;

            if (_loading)
            {
                AdvanceLoading(Time.unscaledDeltaTime);

                // 进度满后等玩家点击：需求方反馈「按 Play 后根本没看到加载页」，
                // 因此不再自动跳过，必须由玩家确认（12 秒兜底防卡死）。
                if (_loading && _loadingReady && (WasStartTapped() || !RequireTapToStart))
                    NotifyStartTapped();

                Context.SetInputBlocked(true);
                return;
            }

            Context.Tick(Time.deltaTime);

            var adShowing = Context.Ads.IsShowing;
            var panelOpen = panels != null && panels.CurrentPanel != PanelId.None;
            Context.SetInputBlocked(adShowing || panelOpen);
        }

        private void AdvanceLoading(float deltaSeconds)
        {
            var minSeconds = LoadingMinSecondsOverride >= 0f
                ? Mathf.Max(0.01f, LoadingMinSecondsOverride)
                : Mathf.Max(0.01f, Context.Balance.LoadingMinSeconds);

            // 单帧卡顿（首帧初始化 / GC / 失焦恢复）不得让进度条一帧从 0 跳到 100%：
            // 真实 deltaTime 超过封顶值时只按封顶值推进，保证进度条看得见地在走。
            _loadingElapsed += Mathf.Min(Mathf.Max(0f, deltaSeconds), MaxLoadingStepSeconds);

            var progress = Mathf.Clamp01(_loadingElapsed / minSeconds);
            panels?.SetLoadingProgress(progress);

            if (progress < 1f)
                return;

            if (!_loadingReady)
            {
                _loadingReady = true;
                _loadingReadyAt = Time.unscaledTime;
                panels?.ShowLoadingStartPrompt();
            }

            if (Time.unscaledTime - _loadingReadyAt >= AutoStartGraceSeconds)
                NotifyStartTapped();
        }

        private static bool WasStartTapped()
        {
            if (Input.GetMouseButtonDown(0))
                return true;

            for (var i = 0; i < Input.touchCount; i++)
            {
                if (Input.GetTouch(i).phase == TouchPhase.Began)
                    return true;
            }

            return false;
        }

        private void FinishEntryFlow()
        {
            panels?.Hide(PanelId.Loading);

            if (Context.Privacy.IsAccepted)
            {
                BeginAfterConsent();
                return;
            }

            // 正式发布必须走隐私门控（R20）；Demo 版按需求先不做，直接按同意处理并开局。
            if (requirePrivacyConsent)
            {
                panels?.Show(PanelId.Privacy);
                Context.SetInputBlocked(true);
                return;
            }

            Context.Privacy.Accept();
            BeginAfterConsent();
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

            // UI 一律取自场景资产（由 `MergeWater/Build Main Scene` 生成，可直接在编辑器里调整）。
            // 这里曾经有一条「缺引用就现场搭一套 HUD」的兜底（`HudBuilder.Build`），现予以移除：
            // 它让「编辑器里看到的」与「运行时看到的」可能不是同一套界面，布局/美术调整也必须先改代码。
            // 现在缺失即配置错误——明确报错，而不是悄悄换一套 UI 掩盖问题。
            if (missing)
                Debug.LogError("[GameBootstrapper] 场景缺少表现层引用（PanelController/HudBinder/" +
                               "FeedbackDirector/AudioDirector 至少一项为空）。" +
                               "请运行菜单 MergeWater/Build Main Scene 重建主场景。");
        }

        private GameContext BuildContext()
        {
            var balance = balanceAsset != null ? balanceAsset.ToBalance() : GameBalance.CreateDefault();

            // `GameContext` 会直接解引用 Aim（ObstacleProvider、DropRequested 订阅），
            // 因此这里必须把「开局必需引用」一次性校验完再构造，缺任何一个都明确报错并停止，
            // 而不是把 null 传进去让它在构造器里抛 NullReference。
            if (field == null)
            {
                Debug.LogError("[GameBootstrapper] 场景缺少 GameField 引用，无法初始化对局。");
                return null;
            }

            if (aim == null)
            {
                Debug.LogError("[GameBootstrapper] 场景缺少 AimController 引用，无法初始化对局。");
                return null;
            }

            if (panels == null || hud == null || audio == null || feedback == null)
            {
                Debug.LogError("[GameBootstrapper] 场景缺少表现层引用（PanelController/HudBinder/" +
                               "AudioDirector/FeedbackDirector）。请运行菜单 MergeWater/Build Main Scene 重建主场景。");
                return null;
            }

            field.Configure(balance, Resources.Load<Sprite>("Placeholder/fruit_circle"), null);

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

            panels.SfxVolumeChanged += value =>
            {
                Context.SettingsService.SetSfxVolume(value);
                TrackVolumeSetting("sfx", value);
            };
            panels.MusicVolumeChanged += value =>
            {
                Context.SettingsService.SetMusicVolume(value);
                TrackVolumeSetting("music", value);
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
            panels?.SetVolumeStates(Context.SettingsService.SfxVolume, Context.SettingsService.MusicVolume);

            BeginRound();
            Context.RefreshBadges();
            panels?.ShowToast(GetPrimaryInputHint(), 4f);
        }

        private static string GetPrimaryInputHint()
        {
            if (Application.isEditor)
                return "请先点击 Game 视图，再按住鼠标左键左右拖动，松开投放";

            return "按住屏幕左右拖动选落点，松手投放";
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
            panels.SetVolumeStates(Context.SettingsService.SfxVolume, Context.SettingsService.MusicVolume);
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

            // Demo 版不做隐私门控：清缓存后直接重开一局，不回到隐私页。
            if (!requirePrivacyConsent)
            {
                Context.Privacy.Accept();
                BeginAfterConsent();
                Context.Notify("已清除本地缓存");
                return;
            }

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

        /// <summary>音量条改动（0..1）：按百分比上报，便于在埋点里看出玩家习惯的音量档位。</summary>
        private void TrackVolumeSetting(string key, float value)
        {
            Context.Analytics.Track(AnalyticsEventNames.SettingChanged,
                AnalyticsParam.Text("key", key),
                AnalyticsParam.Text("value", $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%"));
        }
    }
}
