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

        private readonly Dictionary<PanelId, CanvasGroup> _panels = new Dictionary<PanelId, CanvasGroup>();
        private readonly Dictionary<BadgeId, GameObject> _badges = new Dictionary<BadgeId, GameObject>();
        private float _toastRemaining;

        public event Action SettingsClicked;
        public event Action RetryClicked;
        public event Action ReviveClicked;
        public event Action ShareClicked;
        public event Action LeaderboardClicked;
        public event Action LeaderboardCloseClicked;
        public event Action LeaderboardNextPageClicked;
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

        public PanelId CurrentPanel { get; private set; } = PanelId.None;

        public HudView View => view;

        public void Configure(HudView hudView)
        {
            view = hudView;
            RebuildLookups();
            HookButtons();
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
            _panels[PanelId.Leaderboard] = view.leaderboardPanel;
            _panels[PanelId.AdOverlay] = view.adOverlay;

            _badges[BadgeId.Undo] = view.undoBadge;
            _badges[BadgeId.Bomb] = view.bombBadge;
            _badges[BadgeId.Hammer] = view.hammerBadge;
            _badges[BadgeId.Shake] = view.shakeBadge;
            _badges[BadgeId.Gift] = view.giftBadge;
            _badges[BadgeId.Leaderboard] = view.leaderboardBadge;
        }

        private void HookButtons()
        {
            if (view == null)
                return;

            Hook(view.settingsButton, () => SettingsClicked?.Invoke());
            Hook(view.retryButton, () => RetryClicked?.Invoke());
            Hook(view.reviveButton, () => ReviveClicked?.Invoke());
            Hook(view.shareButton, () => ShareClicked?.Invoke());
            Hook(view.leaderboardButton, () => LeaderboardClicked?.Invoke());
            Hook(view.leaderboardCloseButton, () => LeaderboardCloseClicked?.Invoke());
            Hook(view.leaderboardNextPageButton, () => LeaderboardNextPageClicked?.Invoke());
            Hook(view.privacyAcceptButton, () => PrivacyAcceptClicked?.Invoke());
            Hook(view.privacyDeclineButton, () => PrivacyDeclineClicked?.Invoke());
            Hook(view.privacyPolicyButton, () => PrivacyPolicyClicked?.Invoke());
            Hook(view.userAgreementButton, () => UserAgreementClicked?.Invoke());
            Hook(view.antiAddictionButton, () => AntiAddictionClicked?.Invoke());
            Hook(view.clearCacheButton, () => ClearCacheClicked?.Invoke());
            Hook(view.settingsCloseButton, () => Hide(PanelId.Settings));

            Hook(view.undoButton, () => ItemEntryClicked?.Invoke(ItemKind.Undo));
            Hook(view.bombButton, () => ItemEntryClicked?.Invoke(ItemKind.Bomb));
            Hook(view.hammerButton, () => ItemEntryClicked?.Invoke(ItemKind.Hammer));
            Hook(view.shakeButton, () => ItemEntryClicked?.Invoke(ItemKind.Shake));

            if (view.sfxToggle != null)
                view.sfxToggle.onValueChanged.AddListener(value => SfxToggled?.Invoke(value));
            if (view.musicToggle != null)
                view.musicToggle.onValueChanged.AddListener(value => MusicToggled?.Invoke(value));
            if (view.vibrateToggle != null)
                view.vibrateToggle.onValueChanged.AddListener(value => VibrateToggled?.Invoke(value));
        }

        private static void Hook(Button button, Action callback)
        {
            if (button != null)
                button.onClick.AddListener(() => callback());
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
            if (view == null || view.toastGroup == null)
                return;

            if (view.toastText != null)
                view.toastText.text = message ?? string.Empty;

            _toastRemaining = seconds > 0f ? seconds : toastSeconds;
            view.toastGroup.alpha = 1f;
            view.toastGroup.blocksRaycasts = false;
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

        public void SetLeaderboard(IReadOnlyList<LeaderboardEntry> entries, int page)
        {
            if (view?.leaderboardText == null)
                return;

            var builder = new System.Text.StringBuilder();
            builder.Append("本地排行榜（第 ").Append(page).Append(" 页）\n\n");

            if (entries == null || entries.Count == 0)
            {
                builder.Append("暂无记录，先玩一局吧");
            }
            else
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    builder.Append(i + 1 + (page - 1) * 20).Append(". ");
                    if (entry.IsSelf)
                        builder.Append("<color=#FFD24D>");
                    builder.Append(entry.Name).Append("  ").Append(entry.Score);
                    if (entry.IsSelf)
                        builder.Append("</color>");
                    builder.Append('\n');
                }
            }

            view.leaderboardText.text = builder.ToString();
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

        public void SetVersion(string version)
        {
            if (view?.versionText != null)
                view.versionText.text = $"版本 {version}";
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

            panel.alpha = visible ? 1f : 0f;
            panel.blocksRaycasts = visible;
            panel.interactable = visible;

            if (panel.gameObject.activeSelf != visible)
                panel.gameObject.SetActive(visible);
        }

        private void Update()
        {
            if (_toastRemaining <= 0f || view?.toastGroup == null)
                return;

            _toastRemaining -= Time.unscaledDeltaTime;
            var fade = Mathf.Clamp01(_toastRemaining);
            view.toastGroup.alpha = fade;

            if (_toastRemaining <= 0f)
            {
                _toastRemaining = 0f;
                view.toastGroup.alpha = 0f;
            }
        }
    }
}
