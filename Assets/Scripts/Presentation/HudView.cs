using UnityEngine;
using UnityEngine.UI;

namespace MergeWater.Presentation
{
    public enum PanelId
    {
        None = 0,
        Privacy = 1,
        Settlement = 2,
        Settings = 3,
        Leaderboard = 4,
        AdOverlay = 5
    }

    public enum BadgeId
    {
        Undo = 0,
        Bomb = 1,
        Hammer = 2,
        Shake = 3,
        Gift = 4,
        Leaderboard = 5
    }

    /// <summary>
    /// 对局 HUD 的引用容器（GDD §6.2）。不含逻辑，允许部分引用为空并降级。
    /// 由 <see cref="HudBuilder"/> 在编辑器或运行时构建并填充；替换正式美术时只改 Builder。
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        [Header("顶栏")] public Text bestScoreText;
        public Text scoreText;
        public Text comboText;
        public Image stageProgressFill;
        public Text stageProgressLabel;
        public Button settingsButton;

        [Header("next 预览")] public Image nextFruitIcon;
        public Text nextFruitLabel;

        [Header("左侧入口（上→下）")] public Button shakeButton;
        public Text shakeLabel;
        public GameObject shakeBadge;

        public Button hammerButton;
        public Text hammerLabel;
        public GameObject hammerBadge;

        public Button giftButton;
        public Text giftLabel;
        public GameObject giftBadge;

        [Header("右侧入口（上→下）")] public Button undoButton;
        public Text undoLabel;
        public GameObject undoBadge;

        public Button bombButton;
        public Text bombLabel;
        public GameObject bombBadge;

        public Button leaderboardButton;
        public GameObject leaderboardBadge;

        [Header("隐私政策")] public CanvasGroup privacyPanel;
        public Text privacyText;
        public Button privacyAcceptButton;
        public Button privacyDeclineButton;

        [Header("结算")] public CanvasGroup settlementPanel;
        public Text settlementTitleText;
        public Text settlementScoreText;
        public Text settlementBestText;
        public Text settlementHintText;
        public Button reviveButton;
        public Text reviveLabel;
        public Button retryButton;
        public Button shareButton;

        [Header("设置")] public CanvasGroup settingsPanel;
        public Toggle sfxToggle;
        public Toggle musicToggle;
        public Toggle vibrateToggle;
        public Button settingsCloseButton;
        public Button privacyPolicyButton;
        public Button userAgreementButton;
        public Button antiAddictionButton;
        public Button clearCacheButton;
        public Text versionText;

        [Header("排行榜")] public CanvasGroup leaderboardPanel;
        public Text leaderboardText;
        public Button leaderboardCloseButton;
        public Button leaderboardNextPageButton;

        [Header("广告占位遮罩")] public CanvasGroup adOverlay;
        public Text adOverlayText;

        [Header("Toast")] public CanvasGroup toastGroup;
        public Text toastText;

        [Header("新手引导")] public Image tutorialArrow;

        /// <summary>兼容旧引用名：设置按钮也可通过 <see cref="settingsButton"/> 访问。</summary>
        public Button SettingsButton => settingsButton;
    }
}
