using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MergeWater.Presentation
{
    /// <summary>
    /// 程序化构建对局 HUD（GDD §6.2）与世界表现对象。
    /// 供编辑器场景构建工具与运行时兜底共用；替换正式美术时只改本文件。
    /// </summary>
    public static class HudBuilder
    {
        public static readonly Color TextColor = new Color(0.16f, 0.12f, 0.10f);
        public static readonly Color PanelColor = new Color(1f, 0.97f, 0.90f, 0.97f);
        public static readonly Color ButtonColor = new Color(1f, 0.80f, 0.25f, 1f);
        public static readonly Color SecondaryButtonColor = new Color(0.70f, 0.85f, 0.95f, 1f);
        public static readonly Color DimColor = new Color(0f, 0f, 0f, 0.62f);
        public static readonly Color BadgeColor = new Color(0.92f, 0.24f, 0.22f, 1f);

        private const float ReferenceWidth = 1080f;
        private const float ReferenceHeight = 1920f;

        /// <summary>构建完整表现层，返回挂在根节点上的 <see cref="HudView"/>。</summary>
        public static HudView Build(Transform parent, Sprite circleSprite, Sprite arrowSprite,
            out GameObject presentationRoot)
        {
            var font = UiFontProvider.Resolve();

            var root = new GameObject("Presentation");
            root.transform.SetParent(parent, false);
            presentationRoot = root;

            var canvas = BuildCanvas(root.transform, out var canvasTransform);
            EnsureEventSystem(parent);

            var view = root.AddComponent<HudView>();
            BuildTopBar(canvasTransform, font, circleSprite, view);
            BuildEntryButtons(canvasTransform, font, view);
            BuildPanels(canvasTransform, font, view);
            BuildToast(canvasTransform, font, view);
            BuildTutorialArrow(canvasTransform, arrowSprite, view);

            var timeDirector = root.AddComponent<TimeDirector>();
            var shaker = root.AddComponent<ScreenShaker>();
            var audio = root.AddComponent<AudioDirector>();

            var sfxSource = root.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            var musicSource = root.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.volume = 0.4f;
            audio.Configure(sfxSource, musicSource);

            var feedback = root.AddComponent<FeedbackDirector>();

            var floatingTextRoot = new GameObject("FloatingText");
            floatingTextRoot.transform.SetParent(root.transform, false);
            var floatingText = floatingTextRoot.AddComponent<FloatingTextSpawner>();
            floatingText.SetFont(font);

            var burstRoot = new GameObject("ParticleBurst");
            burstRoot.transform.SetParent(root.transform, false);
            var burst = burstRoot.AddComponent<ParticleBurst>();
            burst.Configure(CreateParticleSystem(burstRoot.transform, circleSprite));

            var previewRoot = new GameObject("AimPreview");
            previewRoot.transform.SetParent(root.transform, false);
            var preview = previewRoot.AddComponent<AimPreviewView>();
            var trajectory = CreateLineRenderer("Trajectory", previewRoot.transform,
                new Color(0.25f, 0.25f, 0.28f, 0.75f), 0.045f, 560);
            var pendingFruit = CreateWorldSprite("PendingFruit", previewRoot.transform, circleSprite, 400);
            preview.Configure(trajectory, pendingFruit);

            var dangerRoot = new GameObject("DangerLine");
            dangerRoot.transform.SetParent(root.transform, false);
            var dangerLine = dangerRoot.AddComponent<DangerLineView>();
            dangerLine.Configure(CreateLineRenderer("Line", dangerRoot.transform,
                new Color(1f, 1f, 1f, 0.35f), 0.06f, 540));

            var panels = root.AddComponent<PanelController>();
            panels.Configure(view);

            var binder = root.AddComponent<HudBinder>();
            binder.Configure(view, panels, feedback, preview, dangerLine, circleSprite);

            return view;
        }

        // ── Canvas ───────────────────────────────────────────────────

        private static Canvas BuildCanvas(Transform parent, out Transform canvasTransform)
        {
            var go = new GameObject("Canvas");
            go.transform.SetParent(parent, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();

            var background = CreateImage("Background", go.transform, new Color(0.99f, 0.93f, 0.84f, 1f));
            Stretch(background.rectTransform);

            canvasTransform = go.transform;
            return canvas;
        }

        private static void EnsureEventSystem(Transform parent)
        {
            if (Object.FindObjectOfType<EventSystem>() != null)
                return;

            var go = new GameObject("EventSystem");
            go.transform.SetParent(parent, false);
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        // ── 顶栏与两侧入口 ───────────────────────────────────────────

        private static void BuildTopBar(Transform parent, Font font, Sprite circleSprite, HudView view)
        {
            view.bestScoreText = CreateText("BestScore", parent, font, "最高分：0", 38,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -34f), new Vector2(420f, 56f),
                TextAnchor.UpperLeft);

            view.scoreText = CreateText("Score", parent, font, "0", 104,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(520f, 130f),
                TextAnchor.UpperCenter);

            view.comboText = CreateText("Combo", parent, font, string.Empty, 46,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(560f, 60f),
                TextAnchor.UpperCenter, new Color(1f, 0.62f, 0.08f));

            view.stageProgressLabel = CreateText("StageLabel", parent, font, "阶段目标 0/200", 32,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(660f, 44f),
                TextAnchor.UpperCenter, new Color(0.45f, 0.38f, 0.32f));

            var barBackground = CreateImage("StageBarBg", parent, new Color(0f, 0f, 0f, 0.16f));
            Place(barBackground.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -272f), new Vector2(620f, 22f));

            var fill = CreateImage("StageBarFill", barBackground.transform, new Color(0.95f, 0.55f, 0.15f));
            Stretch(fill.rectTransform);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            view.stageProgressFill = fill;

            var settings = CreateButton("SettingsButton", parent, font, "设置", 34,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-190f, -34f), new Vector2(140f, 76f),
                SecondaryButtonColor);
            view.settingsButton = settings;

            var nextIcon = CreateImage("NextIcon", parent, Color.white);
            nextIcon.sprite = circleSprite;
            Place(nextIcon.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-150f, -180f), new Vector2(120f, 120f));
            view.nextFruitIcon = nextIcon;

            view.nextFruitLabel = CreateText("NextLabel", parent, font, "NEXT", 30,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-150f, -312f), new Vector2(220f, 40f),
                TextAnchor.UpperCenter, new Color(0.45f, 0.38f, 0.32f));
        }

        private static void BuildEntryButtons(Transform parent, Font font, HudView view)
        {
            // 左侧（上→下）：摇一摇、锤子、大礼包
            CreateEntry(parent, font, "ShakeButton", "摇一摇", new Vector2(0f, 1f), new Vector2(120f, -430f),
                out var shakeButton, out var shakeLabel, out var shakeBadge);
            view.shakeButton = shakeButton;
            view.shakeLabel = shakeLabel;
            view.shakeBadge = shakeBadge;

            CreateEntry(parent, font, "HammerButton", "锤子", new Vector2(0f, 1f), new Vector2(120f, -640f),
                out var hammerButton, out var hammerLabel, out var hammerBadge);
            view.hammerButton = hammerButton;
            view.hammerLabel = hammerLabel;
            view.hammerBadge = hammerBadge;

            CreateEntry(parent, font, "GiftButton", "大礼包", new Vector2(0f, 1f), new Vector2(120f, -850f),
                out var giftButton, out var giftLabel, out var giftBadge);
            view.giftButton = giftButton;
            view.giftLabel = giftLabel;
            view.giftBadge = giftBadge;

            // 右侧（上→下）：撤销、炸弹
            CreateEntry(parent, font, "UndoButton", "撤销", new Vector2(1f, 1f), new Vector2(-120f, -430f),
                out var undoButton, out var undoLabel, out var undoBadge);
            view.undoButton = undoButton;
            view.undoLabel = undoLabel;
            view.undoBadge = undoBadge;

            CreateEntry(parent, font, "BombButton", "炸弹", new Vector2(1f, 1f), new Vector2(-120f, -640f),
                out var bombButton, out var bombLabel, out var bombBadge);
            view.bombButton = bombButton;
            view.bombLabel = bombLabel;
            view.bombBadge = bombBadge;

            CreateEntry(parent, font, "LeaderboardButton", "排行", new Vector2(1f, 1f), new Vector2(-120f, -850f),
                out var boardButton, out var boardLabel, out var boardBadge);
            view.leaderboardButton = boardButton;
            view.leaderboardBadge = boardBadge;
            boardLabel.fontSize = 30;
        }

        private static void CreateEntry(Transform parent, Font font, string name, string label,
            Vector2 anchor, Vector2 anchoredPosition, out Button button, out Text text, out GameObject badge)
        {
            var root = CreateImage(name, parent, PanelColor);
            Place(root.rectTransform, anchor, anchor, anchoredPosition, new Vector2(150f, 150f));

            button = root.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.94f, 0.94f, 0.94f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            button.colors = colors;

            text = CreateText("Label", root.transform, font, label, 30,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(150f, 42f),
                TextAnchor.LowerCenter);

            badge = CreateBadge(root.transform);
            badge.SetActive(false);
        }

        private static GameObject CreateBadge(Transform parent)
        {
            var badge = CreateImage("Badge", parent, BadgeColor);
            Place(badge.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-10f, -10f), new Vector2(34f, 34f));
            return badge.gameObject;
        }

        // ── 面板 ─────────────────────────────────────────────────────

        private static void BuildPanels(Transform parent, Font font, HudView view)
        {
            // 隐私政策（首启必现，R20）
            var privacy = CreatePanelRoot("PrivacyPanel", parent, out var privacyContent);
            view.privacyPanel = privacy;
            CreateText("Title", privacyContent, font, "隐私政策", 52,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(600f, 70f),
                TextAnchor.UpperCenter);
            view.privacyText = CreateText("Body", privacyContent, font,
                "《合成果园》为离线单机 Demo，不接入支付，不收集可识别个人信息；" +
                "同意后将启用本地数据统计与测试广告位，用于验证留存与广告频次设计。" +
                "你可以随时在设置中查看政策详情或清除本地缓存。", 34,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(760f, 420f),
                TextAnchor.UpperCenter);
            view.privacyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            view.privacyText.verticalOverflow = VerticalWrapMode.Overflow;
            view.privacyAcceptButton = CreateButton("AcceptButton", privacyContent, font, "同意并开始", 40,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(520f, 110f),
                ButtonColor);
            view.privacyDeclineButton = CreateButton("DeclineButton", privacyContent, font, "暂不同意", 34,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(420f, 90f),
                new Color(0.82f, 0.82f, 0.82f));

            // 结算（R7/R18/R19）
            var settlement = CreatePanelRoot("SettlementPanel", parent, out var settlementContent);
            view.settlementPanel = settlement;
            view.settlementTitleText = CreateText("Title", settlementContent, font, "本局结束", 54,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(700f, 80f),
                TextAnchor.UpperCenter);
            view.settlementScoreText = CreateText("Score", settlementContent, font, "本局：0", 72,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(700f, 100f),
                TextAnchor.UpperCenter, new Color(0.92f, 0.42f, 0.12f));
            view.settlementBestText = CreateText("Best", settlementContent, font, "最高分：0", 40,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(700f, 60f),
                TextAnchor.UpperCenter);
            view.settlementHintText = CreateText("Hint", settlementContent, font, string.Empty, 32,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -380f), new Vector2(720f, 90f),
                TextAnchor.UpperCenter, new Color(0.45f, 0.38f, 0.32f));
            view.settlementHintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            view.settlementHintText.verticalOverflow = VerticalWrapMode.Overflow;

            view.reviveButton = CreateButtonWithLabel("ReviveButton", settlementContent, font, "复活（看视频）", 38,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 420f), new Vector2(620f, 116f),
                new Color(0.35f, 0.78f, 0.45f), out var reviveLabel);
            view.reviveLabel = reviveLabel;
            view.retryButton = CreateButton("RetryButton", settlementContent, font, "再来一局", 40,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 290f), new Vector2(620f, 116f),
                ButtonColor);
            view.shareButton = CreateButton("ShareButton", settlementContent, font, "分享挑战", 36,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 160f), new Vector2(620f, 106f),
                SecondaryButtonColor);

            // 设置（R25）
            var settings = CreatePanelRoot("SettingsPanel", parent, out var settingsContent);
            view.settingsPanel = settings;
            CreateText("Title", settingsContent, font, "设置", 52,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(600f, 70f),
                TextAnchor.UpperCenter);
            view.sfxToggle = CreateToggle("SfxToggle", settingsContent, font, "音效", new Vector2(0f, -170f));
            view.musicToggle = CreateToggle("MusicToggle", settingsContent, font, "音乐", new Vector2(0f, -270f));
            view.vibrateToggle = CreateToggle("VibrateToggle", settingsContent, font, "震动", new Vector2(0f, -370f));
            view.privacyPolicyButton = CreateButton("PrivacyPolicyButton", settingsContent, font, "隐私政策", 34,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -480f), new Vector2(680f, 92f),
                new Color(0.90f, 0.90f, 0.90f));
            view.userAgreementButton = CreateButton("UserAgreementButton", settingsContent, font, "用户协议", 34,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -590f), new Vector2(680f, 92f),
                new Color(0.90f, 0.90f, 0.90f));
            view.antiAddictionButton = CreateButton("AntiAddictionButton", settingsContent, font, "实名 / 防沉迷说明", 34,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -700f), new Vector2(680f, 92f),
                new Color(0.90f, 0.90f, 0.90f));
            view.clearCacheButton = CreateButton("ClearCacheButton", settingsContent, font, "清除缓存", 34,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -810f), new Vector2(680f, 92f),
                new Color(0.95f, 0.78f, 0.72f));
            view.versionText = CreateText("Version", settingsContent, font, "版本 0.1", 28,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 160f), new Vector2(600f, 44f),
                TextAnchor.LowerCenter, new Color(0.5f, 0.45f, 0.4f));
            view.settingsCloseButton = CreateButton("CloseButton", settingsContent, font, "关闭", 36,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(420f, 96f),
                SecondaryButtonColor);

            // 排行榜（R18）
            var leaderboard = CreatePanelRoot("LeaderboardPanel", parent, out var leaderboardContent);
            view.leaderboardPanel = leaderboard;
            view.leaderboardText = CreateText("Body", leaderboardContent, font, "本地排行榜", 36,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(800f, 900f),
                TextAnchor.UpperCenter);
            view.leaderboardText.horizontalOverflow = HorizontalWrapMode.Wrap;
            view.leaderboardText.verticalOverflow = VerticalWrapMode.Overflow;
            view.leaderboardNextPageButton = CreateButton("NextPageButton", leaderboardContent, font, "下一页", 34,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-180f, 90f), new Vector2(320f, 92f),
                SecondaryButtonColor);
            view.leaderboardCloseButton = CreateButton("CloseButton", leaderboardContent, font, "关闭", 34,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(180f, 90f), new Vector2(320f, 92f),
                ButtonColor);

            // 广告占位遮罩
            var adOverlay = CreatePanelRoot("AdOverlay", parent, out var adContent, dimOnly: true);
            view.adOverlay = adOverlay;
            view.adOverlayText = CreateText("AdText", adContent, font, "广告播放中…", 46,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 120f),
                TextAnchor.MiddleCenter, Color.white);
        }

        private static void BuildToast(Transform parent, Font font, HudView view)
        {
            var group = new GameObject("Toast");
            group.transform.SetParent(parent, false);
            var rect = group.AddComponent<RectTransform>();
            Place(rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 320f), new Vector2(760f, 110f));

            var canvasGroup = group.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            var background = CreateImage("Bg", group.transform, new Color(0f, 0f, 0f, 0.72f));
            Stretch(background.rectTransform);

            view.toastText = CreateText("Text", group.transform, font, string.Empty, 34,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(740f, 100f),
                TextAnchor.MiddleCenter, Color.white);
            view.toastGroup = canvasGroup;
        }

        private static void BuildTutorialArrow(Transform parent, Sprite arrowSprite, HudView view)
        {
            var arrow = CreateImage("TutorialArrow", parent, new Color(0.95f, 0.35f, 0.30f, 0.95f));
            arrow.sprite = arrowSprite;
            Place(arrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -180f), new Vector2(110f, 110f));
            arrow.gameObject.SetActive(false);
            view.tutorialArrow = arrow;
        }

        // ── 基础控件 ─────────────────────────────────────────────────

        private static CanvasGroup CreatePanelRoot(string name, Transform parent, out Transform content,
            bool dimOnly = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            Stretch(rect);

            var group = go.AddComponent<CanvasGroup>();

            var dim = CreateImage("Dim", go.transform, DimColor);
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);

            if (dimOnly)
            {
                content = go.transform;
                return group;
            }

            var panel = CreateImage("Panel", go.transform, PanelColor);
            Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(900f, 1240f));

            content = panel.transform;
            return group;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 100f));
            return image;
        }

        private static Text CreateText(string name, Transform parent, Font font, string content, int fontSize,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, TextAnchor alignment,
            Color? color = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            Place(rect, anchorMin, anchorMax, anchoredPosition, size);

            var text = go.AddComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color ?? TextColor;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, Font font, string label, int fontSize,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            var button = CreateButtonWithLabel(name, parent, font, label, fontSize, anchorMin, anchorMax,
                anchoredPosition, size, color, out _);
            return button;
        }

        private static Button CreateButtonWithLabel(string name, Transform parent, Font font, string label,
            int fontSize, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size,
            Color color, out Text labelText)
        {
            var image = CreateImage(name, parent, color);
            Place(image.rectTransform, anchorMin, anchorMax, anchoredPosition, size);
            image.raycastTarget = true;

            var button = image.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.94f, 0.94f, 0.94f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f);
            button.colors = colors;

            labelText = CreateText("Label", image.transform, font, label, fontSize,
                new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
            labelText.rectTransform.offsetMin = Vector2.zero;
            labelText.rectTransform.offsetMax = Vector2.zero;

            return button;
        }

        private static Toggle CreateToggle(string name, Transform parent, Font font, string label, Vector2 position)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var rect = root.AddComponent<RectTransform>();
            Place(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), position, new Vector2(680f, 84f));

            var box = CreateImage("Box", root.transform, new Color(1f, 1f, 1f, 1f));
            Place(box.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(46f, 0f), new Vector2(64f, 64f));
            box.raycastTarget = true;

            var check = CreateImage("Check", box.transform, new Color(0.35f, 0.78f, 0.45f));
            Place(check.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(40f, 40f));

            var toggle = root.AddComponent<Toggle>();
            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.isOn = true;

            CreateText("Label", root.transform, font, label, 38,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(400f, 60f),
                TextAnchor.MiddleLeft);

            return toggle;
        }

        private static LineRenderer CreateLineRenderer(string name, Transform parent, Color color, float width,
            int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 0;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.numCapVertices = 2;
            line.material = CreateSpriteMaterial(null);
            line.sortingOrder = sortingOrder;
            line.enabled = false;
            return line;
        }

        private static SpriteRenderer CreateWorldSprite(string name, Transform parent, Sprite sprite, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            renderer.enabled = false;
            return renderer;
        }

        private static ParticleSystem CreateParticleSystem(Transform parent, Sprite circleSprite)
        {
            var go = new GameObject("Burst");
            go.transform.SetParent(parent, false);

            var system = go.AddComponent<ParticleSystem>();
            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 0.6f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 4.8f);
            main.startSize = 0.12f;
            main.gravityModifier = 0.7f;
            main.maxParticles = 600;
            main.startColor = Color.white;

            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = CreateSpriteMaterial(circleSprite);
            renderer.sortingOrder = 550;

            return system;
        }

        private static Material CreateSpriteMaterial(Sprite sprite)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");

            if (shader == null)
            {
                Debug.LogWarning("[HudBuilder] 找不到 Sprite 着色器，粒子与虚线可能不可见。");
                return null;
            }

            var material = new Material(shader) { name = "MW_PlaceholderSprite" };
            if (sprite != null)
                material.mainTexture = sprite.texture;

            return material;
        }

        // ── RectTransform 工具 ───────────────────────────────────────

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }
    }
}
