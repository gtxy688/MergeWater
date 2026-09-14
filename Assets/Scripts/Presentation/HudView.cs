using TMPro;
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
        AdOverlay = 5,
        Loading = 6
    }

    /// <summary>
    /// 红点/角标 id。2026-09-14 需求方裁剪入口：先删锤子/炸弹/大礼包，随后连排行入口也删除，
    /// 只剩 清屏(Undo) 与 摇一摇(Shake)。保留项数值**刻意不变**：Undo=0、Shake=3。
    /// </summary>
    public enum BadgeId
    {
        Undo = 0,
        Shake = 3
    }

    /// <summary>
    /// 对局 HUD 的引用容器（GDD §6.2）。不含逻辑，允许部分引用为空并降级。
    /// 这些引用指向场景里**实际存在**的 UI 对象（`Assets/Scenes/Main.unity` 的 `GameRoot/Presentation`
    /// 子树，在编辑器里手工维护）；运行时不生成界面，工程里也**没有**生成界面的编辑器工具。
    ///
    /// <para>文本一律使用 TextMeshPro（<see cref="TextMeshProUGUI"/>）：WebGL / 微信小游戏
    /// 无法访问系统字体，legacy <c>Text</c> + 动态系统字体在真机上会显示为方块（决策 D16）。</para>
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        [Header("顶栏")] public TextMeshProUGUI bestScoreText;
        public TextMeshProUGUI scoreText;
        public Button settingsButton;

        [Header("左侧入口（上→下）")] public Button shakeButton;
        public TextMeshProUGUI shakeLabel;
        public GameObject shakeBadge;

        [Header("右侧入口（上→下）")] public Button undoButton;
        public TextMeshProUGUI undoLabel;
        public GameObject undoBadge;

        [Header("隐私政策")] public UiPanel privacyPanel;
        public TextMeshProUGUI privacyText;
        public Button privacyAcceptButton;
        public Button privacyDeclineButton;

        [Header("结算")] public UiPanel settlementPanel;
        public TextMeshProUGUI settlementTitleText;
        public TextMeshProUGUI settlementScoreText;
        public TextMeshProUGUI settlementBestText;
        public TextMeshProUGUI settlementHintText;
        public Button reviveButton;
        public TextMeshProUGUI reviveLabel;
        public Button retryButton;
        public Button shareButton;

        [Header("设置")] public UiPanel settingsPanel;
        public Toggle sfxToggle;
        public Slider sfxSlider;
        public Image sfxVolumeFill;
        public Toggle musicToggle;
        public Slider musicSlider;
        public Image musicVolumeFill;
        public Toggle vibrateToggle;
        public Button settingsCloseButton;
        public Button privacyPolicyButton;
        public Button userAgreementButton;
        public Button antiAddictionButton;
        public Button clearCacheButton;
        public TextMeshProUGUI versionText;

        [Header("排行榜")] public UiPanel leaderboardPanel;
        public TextMeshProUGUI leaderboardText;
        public Button leaderboardCloseButton;
        public Button leaderboardNextPageButton;

        [Header("广告占位遮罩")] public UiPanel adOverlay;
        public TextMeshProUGUI adOverlayText;

        [Header("Toast")] public UiPanel toastPanel;
        public TextMeshProUGUI toastText;

        [Header("加载页")] public UiPanel loadingPanel;
        public Image loadingProgressFill;
        public TextMeshProUGUI loadingPercentText;
        public TextMeshProUGUI loadingHintText;

        [Header("新手引导")] public Image tutorialArrow;

        /// <summary>兼容旧引用名：设置按钮也可通过 <see cref="settingsButton"/> 访问。</summary>
        public Button SettingsButton => settingsButton;
    }
}
