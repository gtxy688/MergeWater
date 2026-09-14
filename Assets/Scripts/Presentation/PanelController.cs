using System;
using System.Collections.Generic;
using MergeWater.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MergeWater.Presentation
{
    /// <summary>
    /// 面板、Toast、红点与按钮事件的中枢。只负责显示与转发，不决定业务（M7 订阅这里的事件）。
    /// 所有引用可空：缺失时降级为静默，不抛异常。
    /// </summary>
    public sealed class PanelController : MonoBehaviour
    {
        [SerializeField] private HudView view;
        [SerializeField] private float toastSeconds = 1.8f;

        private readonly Dictionary<PanelId, UiPanel> _panels = new Dictionary<PanelId, UiPanel>();
        private readonly Dictionary<BadgeId, GameObject> _badges = new Dictionary<BadgeId, GameObject>();
        private float _toastRemaining;
        private bool _configured;
        private bool _hooked;

        public event Action SettingsClicked;
        public event Action RetryClicked;
        public event Action ReviveClicked;
        public event Action ShareClicked;
        public event Action PrivacyAcceptClicked;
        public event Action PrivacyDeclineClicked;
        public event Action PrivacyPolicyClicked;
        public event Action UserAgreementClicked;
        public event Action AntiAddictionClicked;
        public event Action ClearCacheClicked;
        public event Action<ItemKind> ItemEntryClicked;
        public event Action<bool> SfxToggled;
        public event Action<bool> MusicToggled;
        public event Action<bool> VibrateToggled;

        /// <summary>音量条（0..1）。2026-09-12 需求方要求补回设置页的音量条。</summary>
        public event Action<float> SfxVolumeChanged;

        public event Action<float> MusicVolumeChanged;

        public PanelId CurrentPanel { get; private set; } = PanelId.None;

        public HudView View => view;

        /// <summary>
        /// 运行时装配入口。查找表与按钮监听都是非序列化状态，必须在运行时重新建立：
        /// 只在编辑器构建场景时装配会导致「面板不可见、按钮全部失效」。
        /// </summary>
        private void Awake() => EnsureConfigured();

        /// <summary>
        /// 注入视图并装配。编辑器构建场景工具与测试会显式调用；运行时由 <see cref="Awake"/> 自动完成。
        /// 幂等：同一视图重复调用不会重复挂监听。
        /// </summary>
        public void Configure(HudView hudView)
        {
            if (!ReferenceEquals(view, hudView))
            {
                view = hudView;
                _configured = false;
                _hooked = false;
            }

            EnsureConfigured();
        }

        private void EnsureConfigured()
        {
            if (_configured || view == null)
                return;

            _configured = true;
            RebuildLookups();

            if (!_hooked)
            {
                HookButtons();
                _hooked = true;
            }

            HideAll();
        }

        private void RebuildLookups()
        {
            _panels.Clear();
            _badges.Clear();

            if (view == null)
                return;

            _panels[PanelId.Privacy] = view.privacyPanel;
            _panels[PanelId.Settlement] = view.settlementPanel;
            _panels[PanelId.Settings] = view.settingsPanel;
            _panels[PanelId.AdOverlay] = view.adOverlay;
            _panels[PanelId.Loading] = view.loadingPanel;

            _badges[BadgeId.Undo] = view.undoBadge;
            _badges[BadgeId.Shake] = view.shakeBadge;
        }

        private void HookButtons()
        {
            if (view == null)
                return;

            Hook(view.settingsButton, () => SettingsClicked?.Invoke());
            Hook(view.retryButton, () => RetryClicked?.Invoke());
            Hook(view.reviveButton, () => ReviveClicked?.Invoke());
            Hook(view.shareButton, () => ShareClicked?.Invoke());
            Hook(view.privacyAcceptButton, () => PrivacyAcceptClicked?.Invoke());
            Hook(view.privacyDeclineButton, () => PrivacyDeclineClicked?.Invoke());
            Hook(view.privacyPolicyButton, () => PrivacyPolicyClicked?.Invoke());
            Hook(view.userAgreementButton, () => UserAgreementClicked?.Invoke());
            Hook(view.antiAddictionButton, () => AntiAddictionClicked?.Invoke());
            Hook(view.clearCacheButton, () => ClearCacheClicked?.Invoke());
            Hook(view.settingsCloseButton, () => Hide(PanelId.Settings));

            Hook(view.undoButton, () => ItemEntryClicked?.Invoke(ItemKind.Undo));
            Hook(view.shakeButton, () => ItemEntryClicked?.Invoke(ItemKind.Shake));

            if (view.sfxToggle != null)
            {
                view.sfxToggle.onValueChanged.AddListener(value => SfxToggled?.Invoke(value));
                // 需求方（2026-09-12）：取消勾选后禁止再调节对应音量，勾回来时恢复可调。
                // 关掉时保留音量值（只是在同一条滑条上不可操作），开关本身就是静音语义。
                view.sfxToggle.onValueChanged.AddListener(isOn => SetVolumeBarInteractable(view.sfxSlider, isOn));
            }

            if (view.musicToggle != null)
            {
                view.musicToggle.onValueChanged.AddListener(value => MusicToggled?.Invoke(value));
                view.musicToggle.onValueChanged.AddListener(isOn => SetVolumeBarInteractable(view.musicSlider, isOn));
            }

            if (view.vibrateToggle != null)
                view.vibrateToggle.onValueChanged.AddListener(value => VibrateToggled?.Invoke(value));
            // 音量条的分段填充必须在**运行时**接线：填充用的是 Image.fillAmount，与 Slider.value 的
            // 同步关系没法靠 Inspector 里拖一个持久监听搞定。历史缺陷正是「只在编辑器期用代码挂监听」——
            // 运行时监听不会被序列化进场景，表现为「音量真的变了、填充条却一直满格」（见
            // BootstrappedSceneTests 里这一类缺陷的回归守卫）。
            if (view.sfxSlider != null)
            {
                view.sfxSlider.onValueChanged.AddListener(value =>
                {
                    if (view.sfxVolumeFill != null)
                        view.sfxVolumeFill.fillAmount = Mathf.Clamp01(value);
                    SfxVolumeChanged?.Invoke(value);
                });
            }

            if (view.musicSlider != null)
            {
                view.musicSlider.onValueChanged.AddListener(value =>
                {
                    if (view.musicVolumeFill != null)
                        view.musicVolumeFill.fillAmount = Mathf.Clamp01(value);
                    MusicVolumeChanged?.Invoke(value);
                });
            }
        }

        private static void Hook(Button button, Action callback)
        {
            if (button != null)
                button.onClick.AddListener(() => callback());
        }

        /// <summary>
        /// 音量条的可调状态（2026-09-12 需求方：取消勾选时禁止调节大小）。
        /// 用 <c>interactable</c> 而不是隐藏：关闭时填充仍显示，玩家能看到「关掉前的音量是多少」，
        /// 勾回来时不用重新调。视觉变暗由 Slider 的 DisabledColor 提供。
        /// </summary>
        private static void SetVolumeBarInteractable(Slider slider, bool interactable)
        {
            if (slider != null)
                slider.interactable = interactable;
        }

        public void Show(PanelId id)
        {
            if (id == PanelId.None)
            {
                HideAll();
                return;
            }

            // 互斥显示：后请求者优先，不叠层。
            foreach (var pair in _panels)
                SetPanelVisible(pair.Key, pair.Key == id);

            CurrentPanel = id;
        }

        public void Hide(PanelId id)
        {
            SetPanelVisible(id, false);
            if (CurrentPanel == id)
                CurrentPanel = PanelId.None;
        }

        public void HideAll()
        {
            foreach (var pair in _panels)
                SetPanelVisible(pair.Key, false);

            CurrentPanel = PanelId.None;
        }

        public void ShowToast(string message, float seconds = -1f)
        {
            if (view == null || view.toastPanel == null)
                return;

            if (view.toastText != null)
                view.toastText.text = message ?? string.Empty;

            _toastRemaining = seconds > 0f ? seconds : toastSeconds;
            view.toastPanel.Group.alpha = 1f;
            view.toastPanel.Group.blocksRaycasts = false;
        }

        public void SetBadge(BadgeId id, bool visible)
        {
            if (_badges.TryGetValue(id, out var badge) && badge != null)
                badge.SetActive(visible);
        }

        public void SetSettlement(string title, int score, int best, string hint, bool canRevive, bool showShare)
        {
            if (view == null)
                return;

            if (view.settlementTitleText != null)
                view.settlementTitleText.text = title;
            if (view.settlementScoreText != null)
                view.settlementScoreText.text = $"本局：{score}";
            if (view.settlementBestText != null)
                view.settlementBestText.text = $"最高分：{best}";
            if (view.settlementHintText != null)
                view.settlementHintText.text = hint ?? string.Empty;

            if (view.reviveButton != null)
                view.reviveButton.interactable = canRevive;
            if (view.reviveLabel != null)
                view.reviveLabel.text = canRevive ? "复活（看视频）" : "复活已用完";
            if (view.shareButton != null)
                view.shareButton.gameObject.SetActive(showShare);
        }

        public void SetToggleStates(bool sfx, bool music, bool vibrate)
        {
            if (view == null)
                return;

            if (view.sfxToggle != null)
                view.sfxToggle.isOn = sfx;
            if (view.musicToggle != null)
                view.musicToggle.isOn = music;
            if (view.vibrateToggle != null)
                view.vibrateToggle.isOn = vibrate;
        }

        /// <summary>
        /// 音量条的初始位置（0..1）。用 <c>SetValueWithoutNotify</c>：读档回填不应触发
        /// 「玩家改了设置」的事件链，音量由 <c>GameContext.ApplySettingsToAudio</c> 统一应用；
        /// 填充条不随 Slider 自动刷新，所以这里要一并写入。
        /// 同时按开关状态对齐可调性（关掉音效/音乐后音量条不可拖）。
        /// </summary>
        public void SetVolumeStates(float sfx, float music)
        {
            if (view == null)
                return;

            var sfxValue = Mathf.Clamp01(sfx);
            var musicValue = Mathf.Clamp01(music);

            if (view.sfxSlider != null)
                view.sfxSlider.SetValueWithoutNotify(sfxValue);
            if (view.sfxVolumeFill != null)
                view.sfxVolumeFill.fillAmount = sfxValue;
            if (view.musicSlider != null)
                view.musicSlider.SetValueWithoutNotify(musicValue);
            if (view.musicVolumeFill != null)
                view.musicVolumeFill.fillAmount = musicValue;

            SetVolumeBarInteractable(view.sfxSlider, view.sfxToggle == null || view.sfxToggle.isOn);
            SetVolumeBarInteractable(view.musicSlider, view.musicToggle == null || view.musicToggle.isOn);
        }

        public void SetVersion(string version)
        {
            if (view?.versionText != null)
                view.versionText.text = $"版本 {version}";
        }

        /// <summary>加载页进度满后提示玩家点击开始（需求方要求加载页必须能被看到）。</summary>
        public void ShowLoadingStartPrompt()
        {
            if (view?.loadingHintText != null)
                view.loadingHintText.text = "点击开始";
        }

        /// <summary>加载页进度（0..1）：刷新进度条与百分比文本。</summary>
        public void SetLoadingProgress(float progress)        {
            if (view == null)
                return;

            var t = Mathf.Clamp01(progress);

            if (view.loadingProgressFill != null)
                view.loadingProgressFill.fillAmount = t;

            if (view.loadingPercentText != null)
                view.loadingPercentText.text = $"{Mathf.RoundToInt(t * 100f)}%";

            if (view.loadingHintText != null && string.IsNullOrEmpty(view.loadingHintText.text))
                view.loadingHintText.text = "加载中…";
        }

        public void SetAdOverlay(bool visible, string message)
        {
            if (view?.adOverlayText != null)
                view.adOverlayText.text = message ?? string.Empty;

            if (visible)
                Show(PanelId.AdOverlay);
            else if (CurrentPanel == PanelId.AdOverlay)
                Hide(PanelId.AdOverlay);
        }

        private void SetPanelVisible(PanelId id, bool visible)
        {
            if (!_panels.TryGetValue(id, out var panel) || panel == null)
                return;

            // 显隐的定义收敛在 UiPanel（alpha + raycast + interactable + activeSelf 必须同时正确，
            // 否则会出现「active 了却看不见」——alpha=0 那个坑）。
            panel.SetVisible(visible);
        }

        private void Update()
        {
            if (_toastRemaining <= 0f || view?.toastPanel == null)
                return;

            _toastRemaining -= Time.unscaledDeltaTime;
            var fade = Mathf.Clamp01(_toastRemaining);
            view.toastPanel.Group.alpha = fade;

            if (_toastRemaining <= 0f)
            {
                _toastRemaining = 0f;
                view.toastPanel.Group.alpha = 0f;
            }
        }
    }
}
