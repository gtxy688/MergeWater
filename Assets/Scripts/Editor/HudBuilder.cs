using MergeWater.Core;
using MergeWater.Presentation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MergeWater.Editor
{
    /// <summary>
    /// 程序化构建对局 HUD（GDD §6.2）与世界表现对象。
    ///
    /// <para><b>仅编辑器可用</b>：本类位于 <c>MergeWater.Editor</c>（Editor 平台程序集），
    /// 运行时代码**无法**生成界面——UI 一律来自 <c>Assets/Scenes/Main.unity</c> 里的实际对象，
    /// 由菜单 `MergeWater/Build Main Scene` 生成，之后可直接在编辑器里选中调整。
    /// 这样「编辑器里看到的」与「运行时看到的」是同一套对象，改布局/美术不必先改代码。</para>
    ///
    /// <para>改结构性布局请改本文件后重建场景；只微调位置/颜色/文字时直接改场景即可。</para>
    /// </summary>
    public static class HudBuilder
    {
        public static readonly Color TextColor = new Color(0.16f, 0.12f, 0.10f);

        /// <summary>
        /// 弹窗面板底板是美术包的深紫 `panel_popup`，直接落在面板上的文字（标题/正文/开关名）
        /// 必须用浅色，否则深棕 `TextColor` 在紫底上对比度不足（2026-09-12 修完九宫格缩放后暴露）。
        /// 按钮内部的文字仍用 `TextColor`——按钮底是浅色胶囊。
        /// </summary>
        public static readonly Color PanelTextColor = new Color(1f, 0.98f, 0.94f);

        /// <summary>面板上的次要文字（版本号、提示语）：浅紫灰，弱化但仍可读。</summary>
        public static readonly Color PanelTextMutedColor = new Color(0.78f, 0.76f, 0.95f);

        /// <summary>面板上的分数高亮：紫底上用暖黄，比原来的橙红更跳。</summary>
        public static readonly Color PanelScoreColor = new Color(1f, 0.84f, 0.28f);

        public static readonly Color PanelColor = new Color(1f, 0.97f, 0.90f, 0.97f);
        public static readonly Color ButtonColor = new Color(1f, 0.80f, 0.25f, 1f);
        public static readonly Color SecondaryButtonColor = new Color(0.70f, 0.85f, 0.95f, 1f);
        public static readonly Color DimColor = new Color(0f, 0f, 0f, 0.62f);
        public static readonly Color BadgeColor = new Color(0.92f, 0.24f, 0.22f, 1f);

        /// <summary>
        /// 对局世界底色，由相机的 SolidColor 清屏提供（见 <see cref="Build"/>）。
        /// HUD 里**不能**再放一块满屏不透明底板：Canvas 是 ScreenSpaceOverlay，永远绘制在世界之上，
        /// 一块不透明的满屏 Image 会把水果、容器、警戒线、落点预览线全部盖住。
        ///
        /// 2026-09-12：需求方先是要求换掉米色底、后又保留意见要求「换回原来的」，因此本值恢复为米色；
        /// `BackdropView` 仍保留在构建流程里（`BuildBackdrop`），只是没有指派背景素材时自动关闭渲染器，
        /// 后续想再启用只需在 <see cref="BuildBackdrop"/> 里填回资源名。
        /// </summary>
        public static readonly Color BackgroundColor = new Color(0.99f, 0.93f, 0.84f, 1f);

        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 1920f;

        /// <summary>微信胶囊保留区（右上角，参考分辨率单位）：顶栏元素不得进入（GDD §6.2）。</summary>
        public const float CapsuleZoneWidth = 300f;

        public const float CapsuleZoneHeight = 115f;

        /// <summary>底部净空：HUD 不放任何元素，对应 R27（底部不放 Banner / 信息流 / 推广位）。</summary>
        public const float BottomClearance = 60f;

        /// <summary>预置飘字池的容量（运行时不再创建飘字对象，见 FloatingTextSpawner）。</summary>
        public const int FloatingTextPoolSize = 12;

        /// <summary>
        /// 随包中文 TMP 字体资产路径（权威定义在 <see cref="TMPFontBuilder.OutputPath"/>）。
        /// 使用 SIMYOU（幼圆）静态烘焙的 SDF 资产：WebGL / 微信小游戏无法访问系统字体，
        /// 字体必须随包分发（决策 D16）。字形在编辑期烘进图集，运行时不生成任何 glyph。
        ///
        /// <para>**刻意不放在 `Resources/` 下**：Resources 里的资源会被无条件打进包体，
        /// 而字体资产只要被场景引用就会随包分发；放进 Resources 反而失去「不再被引用就不打包」的保护；
        /// 6.7MB 的源 .ttf 更不该待在那里——运行时完全用不到，只有编辑期烘焙需要。</para>
        /// </summary>
        public const string FontAssetPath = TMPFontBuilder.OutputPath;

        /// <summary>加载随包中文字体资产（仅编辑器工具使用；运行时靠场景里的序列化引用）。</summary>
        public static TMP_FontAsset LoadFontAsset() =>
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

        /// <summary>构建完整表现层，返回挂在根节点上的 <see cref="HudView"/>。</summary>
        public static HudView Build(Transform parent, Sprite circleSprite, Sprite arrowSprite,
            out GameObject presentationRoot)
        {
            var font = LoadFontAsset();
            if (font == null)
                Debug.LogError($"[HudBuilder] 找不到 TMP 字体资产 {FontAssetPath}；" +
                               "先运行 MergeWater/Font/2. 烘焙中文 TMP 字体资产。" +
                               "否则文本会回退 TMP 默认字体，中文显示为空白。");

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

            // 加载页最后建：作为最后一个子节点绘制在最上层，遮住对局 HUD。
            BuildLoadingScreen(canvasTransform, font, circleSprite, view);

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

            // 飘字：预先建好一池对象（运行时不生成 UI，需求方 2026-09-12 要求）。
            // 池子放在 `Presentation/FloatingText/Pool` 下，条目是带 TextMeshPro 的普通对象，
            // 运行时只做「取一条 → 激活 → 播完隐藏」。
            var floatingTextRoot = new GameObject("FloatingText");
            floatingTextRoot.transform.SetParent(root.transform, false);
            var floatingText = floatingTextRoot.AddComponent<FloatingTextSpawner>();

            var poolRoot = new GameObject("Pool");
            poolRoot.transform.SetParent(floatingTextRoot.transform, false);
            var pool = new FloatingTextItem[FloatingTextPoolSize];
            for (var i = 0; i < pool.Length; i++)
            {
                var itemGo = new GameObject($"FloatingText_{i:00}");
                itemGo.transform.SetParent(poolRoot.transform, false);

                var mesh = itemGo.AddComponent<TextMeshPro>();
                if (font != null)
                    mesh.font = font;
                mesh.alignment = TextAlignmentOptions.Center;
                mesh.fontSize = 4;
                mesh.text = string.Empty;

                var itemRenderer = itemGo.GetComponent<MeshRenderer>();
                if (itemRenderer != null && font != null && font.material != null)
                    itemRenderer.sharedMaterial = font.material;
                if (itemRenderer != null)
                    itemRenderer.sortingOrder = 600;

                pool[i] = itemGo.AddComponent<FloatingTextItem>();
                itemGo.SetActive(false);
            }

            floatingText.Configure(pool);

            var burstRoot = new GameObject("ParticleBurst");
            burstRoot.transform.SetParent(root.transform, false);
            var burst = burstRoot.AddComponent<ParticleBurst>();
            burst.Configure(CreateParticleSystem(burstRoot.transform, circleSprite));

            // 手感反馈的组件引用必须在构建期接好（这些字段是可序列化的）；
            // balance 由 GameBootstrapper 在运行时补入，震屏目标也由它在运行时指向真实相机。
            feedback.Configure(timeDirector, shaker, audio, floatingText, burst, null);

            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                shaker.SetTarget(mainCamera.transform);

                // 世界底色只能来自相机清屏色：屏幕空间叠加 Canvas 的任何满屏不透明底板都会挡住世界内容。
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
                mainCamera.backgroundColor = BackgroundColor;
            }

            // 对局背景（2026-09-12 需求方换回原来的纯色底后默认不启用素材，见 BuildBackdrop）。
            BuildBackdrop(root.transform, mainCamera);

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

            // 刻意不在这里建「满屏背景板」：Canvas 是 ScreenSpaceOverlay，永远绘制在世界之上，
            // 任意一块不透明的满屏 Image 都会让玩家看不到任何对局画面（水果、容器、警戒线、预览线），
            // 表现为「看不出落点」「改了参数也没区别」。底色由相机的 SolidColor 清屏提供。

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

        // ── 对局背景 ─────────────────────────────────────────────────

        /// <summary>背景排序值：必须低于全部对局元素（场地视觉 -10、水果 100+、线 400+、粒子 550、飘字 600）。</summary>
        private const int BackdropSortingOrder = -100;

        /// <summary>
        /// 对局背景资源名。2026-09-12 需求方要求换回原来的纯色底，故置空（`BuildBackdrop` 会自动关闭渲染器并回落相机清屏色）。
        /// 想再启用背景图时，把美术资源放进 <see cref="ArtSpriteRoot"/>（如 `bg_night`），并在这里填回资源名即可，其余代码无需改动。
        /// </summary>
        private const string BackdropArtName = "";

        /// <summary>
        /// 对局背景：世界空间 SpriteRenderer + <see cref="BackdropView"/> 按相机视野铺满（cover）。
        /// 未指派素材（<see cref="BackdropArtName"/> 为空）或素材缺失时不抛异常，直接关闭渲染器、
        /// 由相机纯色清屏兜底（与其它 <see cref="LoadArt"/> 调用点的降级方式一致）。
        /// </summary>
        private static void BuildBackdrop(Transform parent, Camera camera)
        {
            var go = new GameObject("Backdrop");
            go.transform.SetParent(parent, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = string.IsNullOrEmpty(BackdropArtName) ? null : LoadArt(BackdropArtName);
            renderer.sortingOrder = BackdropSortingOrder;
            renderer.enabled = renderer.sprite != null;

            if (renderer.sprite == null && !string.IsNullOrEmpty(BackdropArtName))
                Debug.LogWarning($"[HudBuilder] 缺少背景图 {ArtSpriteRoot}{BackdropArtName}.png，对局背景回落为相机纯色清屏。");

            go.AddComponent<BackdropView>().Configure(renderer, camera);
        }

        // ── 顶栏与两侧入口 ───────────────────────────────────────────

        private static void BuildTopBar(Transform parent, TMP_FontAsset font, Sprite circleSprite, HudView view)
        {
            // 贴边元素一律使用「同侧 pivot + 偏移 = 距该边距离」，避免被裁出画布。
            // 右上角 CapsuleZoneWidth × CapsuleZoneHeight 为微信胶囊保留区（GDD §6.2），顶栏元素不得进入。
            view.bestScoreText = CreateText("BestScore", parent, font, "最高分：0", 38,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -34f), new Vector2(520f, 56f),
                TextAnchor.UpperLeft);

            // 中央分数收窄到 440，右边缘 760 < 胶囊区左边界（1080 − 300 = 780）。
            view.scoreText = CreateText("Score", parent, font, "0", 104,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(440f, 120f),
                TextAnchor.UpperCenter);

            // 需求方（2026-09-12）：连击提示不在顶栏显示，改为在合成位置用飘字给出更强反馈
            //（见 FeedbackDirector.PlayScore），因此 HudView 里已无 comboText 字段。

            // 需求方（2026-09-12）：删去「阶段目标 + 进度条」，顶栏只保留总分（与连击提示）。
            // 设置按钮：换成美术包里的圆形齿轮钮（泡泡立体感，比原先的胶囊文字钮好看），只放图标。
            var settingsSprite = LoadArt("button_settings");
            var settings = CreateImage("SettingsButton", parent, Color.white);
            Place(settings.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-46f, -120f), new Vector2(132f, 132f));
            settings.raycastTarget = true;

            if (settingsSprite != null)
            {
                settings.sprite = settingsSprite;
                settings.preserveAspect = true;
            }
            else
            {
                var fallback = ResolveButtonSprite(SecondaryButtonColor, out var fallbackTint);
                if (fallback != null)
                {
                    settings.sprite = fallback;
                    settings.type = Image.Type.Sliced;
                    settings.color = fallbackTint;
                }
            }

            var settingsButton = settings.gameObject.AddComponent<Button>();
            var settingsColors = settingsButton.colors;
            settingsColors.normalColor = Color.white;
            settingsColors.highlightedColor = new Color(0.94f, 0.94f, 0.94f);
            settingsColors.pressedColor = new Color(0.82f, 0.82f, 0.82f);
            settingsButton.colors = settingsColors;

            var gearIcon = LoadArt("icon_settings");
            if (gearIcon != null)
            {
                var gear = CreateImage("Icon", settings.transform, Color.white);
                gear.sprite = gearIcon;
                gear.preserveAspect = true;
                Place(gear.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(66f, 66f));
            }
            else
            {
                CreateText("Label", settings.transform, font, "设置", 30,
                    new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero,
                    TextAnchor.MiddleCenter);
            }

            view.settingsButton = settingsButton;

            // 需求方（2026-09-12）：删去右上角「NEXT 下一个水果」预览圆圈——玩家认为它没必要。
            // HudView.nextFruitIcon / nextFruitLabel 保留字段以便日后恢复，但这里不再创建。
        }

        private static void BuildEntryButtons(Transform parent, TMP_FontAsset font, HudView view)
        {
            // 左侧（上→下）：摇一摇、锤子、大礼包
            CreateEntry(parent, font, "ShakeButton", "摇一摇", new Vector2(0f, 1f), new Vector2(120f, -470f),
                "icon_shake", out var shakeButton, out var shakeLabel, out var shakeBadge);
            view.shakeButton = shakeButton;
            view.shakeLabel = shakeLabel;
            view.shakeBadge = shakeBadge;

            CreateEntry(parent, font, "HammerButton", "锤子", new Vector2(0f, 1f), new Vector2(120f, -680f),
                "icon_hammer", out var hammerButton, out var hammerLabel, out var hammerBadge);
            view.hammerButton = hammerButton;
            view.hammerLabel = hammerLabel;
            view.hammerBadge = hammerBadge;

            CreateEntry(parent, font, "GiftButton", "大礼包", new Vector2(0f, 1f), new Vector2(120f, -890f),
                "icon_gift", out var giftButton, out var giftLabel, out var giftBadge);
            view.giftButton = giftButton;
            view.giftLabel = giftLabel;
            view.giftBadge = giftBadge;

            // 右侧（上→下）：撤销、炸弹
            CreateEntry(parent, font, "UndoButton", "撤销", new Vector2(1f, 1f), new Vector2(-120f, -470f),
                "icon_undo", out var undoButton, out var undoLabel, out var undoBadge);
            view.undoButton = undoButton;
            view.undoLabel = undoLabel;
            view.undoBadge = undoBadge;

            CreateEntry(parent, font, "BombButton", "炸弹", new Vector2(1f, 1f), new Vector2(-120f, -680f),
                "icon_bomb", out var bombButton, out var bombLabel, out var bombBadge);
            view.bombButton = bombButton;
            view.bombLabel = bombLabel;
            view.bombBadge = bombBadge;

            CreateEntry(parent, font, "LeaderboardButton", "排行", new Vector2(1f, 1f), new Vector2(-120f, -890f),
                "icon_leaderboard", out var boardButton, out _, out var boardBadge);
            view.leaderboardButton = boardButton;
            view.leaderboardBadge = boardBadge;
        }

        private static void CreateEntry(Transform parent, TMP_FontAsset font, string name, string label,
            Vector2 anchor, Vector2 anchoredPosition, string iconName,
            out Button button, out TextMeshProUGUI text, out GameObject badge)
        {
            var root = CreateImage(name, parent, PanelColor);
            Place(root.rectTransform, anchor, anchor, anchoredPosition, new Vector2(150f, 150f));

            // 美术按钮底（正方形：图标居中偏上、文字在下）。入口按钮用暖色底，和米黄/果实主题一致。
            var sprite = ResolveButtonSprite(ButtonColor, out var tint);
            if (sprite != null)
            {
                root.sprite = sprite;
                root.type = Image.Type.Sliced;
                root.color = tint;
            }

            button = root.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.94f, 0.94f, 0.94f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            button.colors = colors;

            var icon = LoadArt(iconName);
            if (icon != null)
            {
                var iconImage = CreateImage("Icon", root.transform, Color.white);
                iconImage.sprite = icon;
                iconImage.preserveAspect = true;
                Place(iconImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 24f), new Vector2(68f, 68f));
            }

            text = CreateText("Label", root.transform, font, label, 30,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(150f, 38f),
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

        private static void BuildPanels(Transform parent, TMP_FontAsset font, HudView view)
        {
            // 隐私政策（首启必现，R20）
            var privacy = CreatePanelRoot("PrivacyPanel", parent, out var privacyContent);
            view.privacyPanel = privacy;
            CreateText("Title", privacyContent, font, "隐私政策", 52,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(600f, 70f),
                TextAnchor.UpperCenter, PanelTextColor);
            view.privacyText = CreateText("Body", privacyContent, font,
                "《合成果园》为离线单机 Demo，不接入支付，不收集可识别个人信息；" +
                "同意后将启用本地数据统计与测试广告位，用于验证留存与广告频次设计。" +
                "你可以随时在设置中查看政策详情或清除本地缓存。", 34,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(760f, 420f),
                TextAnchor.UpperCenter, PanelTextColor);
            view.privacyText.enableWordWrapping = true;
            view.privacyText.overflowMode = TextOverflowModes.Overflow;
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
                TextAnchor.UpperCenter, PanelTextColor);
            view.settlementScoreText = CreateText("Score", settlementContent, font, "本局：0", 72,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(700f, 100f),
                TextAnchor.UpperCenter, PanelScoreColor);
            view.settlementBestText = CreateText("Best", settlementContent, font, "最高分：0", 40,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(700f, 60f),
                TextAnchor.UpperCenter, PanelTextColor);
            view.settlementHintText = CreateText("Hint", settlementContent, font, string.Empty, 32,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -380f), new Vector2(720f, 90f),
                TextAnchor.UpperCenter, PanelTextMutedColor);
            view.settlementHintText.enableWordWrapping = true;
            view.settlementHintText.overflowMode = TextOverflowModes.Overflow;

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
                TextAnchor.UpperCenter, PanelTextColor);
            view.sfxToggle = CreateToggle("SfxToggle", settingsContent, font, "音效", new Vector2(0f, -170f),
                withVolumeBar: true, out var sfxVolumeBar, out var sfxVolumeFill);
            view.sfxSlider = sfxVolumeBar;
            view.sfxVolumeFill = sfxVolumeFill;
            view.musicToggle = CreateToggle("MusicToggle", settingsContent, font, "音乐", new Vector2(0f, -270f),
                withVolumeBar: true, out var musicVolumeBar, out var musicVolumeFill);
            view.musicSlider = musicVolumeBar;
            view.musicVolumeFill = musicVolumeFill;
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
            // 底部块（版本 + 关闭）整体上移 40 设计单位（2026-09-12 需求方「把底部上移一点」）：
            // 清除缓存按钮下边缘在距面板底 338 处，原布局把底部块压在 60–204 这一段，
            // 于是上方空出 134、下方只剩 60，重心偏低。上移后空档 94 / 底部留白 100，接近均分。
            view.versionText = CreateText("Version", settingsContent, font, "版本 0.1", 28,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(600f, 44f),
                TextAnchor.LowerCenter, PanelTextMutedColor);
            view.settingsCloseButton = CreateButton("CloseButton", settingsContent, font, "关闭", 36,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 100f), new Vector2(420f, 96f),
                SecondaryButtonColor);

            // 排行榜（R18）
            var leaderboard = CreatePanelRoot("LeaderboardPanel", parent, out var leaderboardContent);
            view.leaderboardPanel = leaderboard;
            view.leaderboardText = CreateText("Body", leaderboardContent, font, "本地排行榜", 36,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(800f, 900f),
                TextAnchor.UpperCenter, PanelTextColor);
            view.leaderboardText.enableWordWrapping = true;
            view.leaderboardText.overflowMode = TextOverflowModes.Overflow;
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

        private static void BuildToast(Transform parent, TMP_FontAsset font, HudView view)
        {
            // 提示条要装得下「点击 Game 视图，按住鼠标左键左右拖动选落点，松手投放」这类长句：
            // 早期 760x110 + Overflow 会让首尾文字被裁切，这里加宽、换行、缩小字号。
            var group = new GameObject("Toast");
            group.transform.SetParent(parent, false);
            var rect = group.AddComponent<RectTransform>();
            Place(rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 320f), new Vector2(1000f, 160f));

            var canvasGroup = group.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            var toastPanel = group.AddComponent<UiPanel>();

            var background = CreateImage("Bg", group.transform, new Color(0f, 0f, 0f, 0.72f));
            Stretch(background.rectTransform);

            view.toastText = CreateText("Text", group.transform, font, string.Empty, 30,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940f, 140f),
                TextAnchor.MiddleCenter, Color.white);
            // TMP：换行 + 超长截断（对应 legacy 的 Wrap/Truncate）。
            view.toastText.enableWordWrapping = true;
            view.toastText.overflowMode = TextOverflowModes.Ellipsis;
            view.toastPanel = toastPanel;
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

        // ── 加载页 ───────────────────────────────────────────────────

        /// <summary>
        /// 启动加载页（参考占位版）：暖黄竖条背景 + 底部地面条 + 装饰水果 + 标题 +
        /// 健康游戏忠告 + 进度条/百分比/「加载中…」。
        /// 全部为占位美术（`Placeholder/ui_stripe_bg` 与水果圆片染色），替换正式加载图时只改这里与占位图。
        /// </summary>
        private static void BuildLoadingScreen(Transform parent, TMP_FontAsset font, Sprite circleSprite, HudView view)
        {
            var root = new GameObject("LoadingPanel");
            root.transform.SetParent(parent, false);

            var rect = root.AddComponent<RectTransform>();
            Stretch(rect);

            var group = root.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
            view.loadingPanel = root.AddComponent<UiPanel>();

            var stripes = Resources.Load<Sprite>("Placeholder/ui_stripe_bg");
            var background = CreateImage("Bg", root.transform, new Color(0.98f, 0.85f, 0.46f, 1f));
            background.sprite = stripes;
            Stretch(background.rectTransform);

            var ground = CreateImage("Ground", root.transform, new Color(0.45f, 0.31f, 0.20f, 1f));
            Place(ground.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                Vector2.zero, new Vector2(0f, 240f));

            // 装饰水果：用美术包里的光泽果实球（棕 / 橙 / 柠檬 / 蓝紫）代替纯色圆片。
            // 注意锚点是屏幕中心（0.5,0.5），坐标是「相对中心」的偏移，必须留出边距，
            // 否则会像早期版本那样被屏幕边缘裁切、并压到左上角的适龄角标。
            var decorations = new[]
            {
                new Vector4(-300f, 430f, 180f, 0f),
                new Vector4(300f, 470f, 165f, 1f),
                new Vector4(-330f, 150f, 145f, 2f),
                new Vector4(330f, 120f, 200f, 3f),
                new Vector4(-230f, -150f, 170f, 1f),
                new Vector4(250f, -220f, 150f, 2f),
                new Vector4(0f, 250f, 320f, 1f)
            };
            var ballNames = new[] { "ball_brown", "ball_orange", "ball_lemon", "ball_blue" };

            foreach (var item in decorations)
            {
                var index = Mathf.Clamp(Mathf.RoundToInt(item.w), 0, ballNames.Length - 1);
                var ball = LoadArt(ballNames[index]);

                var fruit = CreateImage($"Deco_{index}", root.transform, FruitPalette.ForLevel(GameBalance.MinTier + index * 3));
                if (ball != null)
                {
                    fruit.sprite = ball;
                    fruit.preserveAspect = true;
                    fruit.color = Color.white;
                }
                else
                {
                    fruit.sprite = circleSprite;
                }

                Place(fruit.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(item.x, item.y), new Vector2(item.z, item.z));
            }

            // 标题：美术丝带底 + 文字（丝带偏棕，用白字加深色投影保证可读）。
            var ribbon = CreateImage("TitleRibbon", root.transform, Color.white);
            var ribbonSprite = LoadArt("ribbon_title");
            if (ribbonSprite != null)
            {
                ribbon.sprite = ribbonSprite;
                ribbon.type = Image.Type.Sliced;
            }
            else
            {
                ribbon.color = new Color(0.62f, 0.42f, 0.26f, 1f);
            }

            Place(ribbon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -320f), new Vector2(820f, 240f));

            var title = CreateText("Title", ribbon.transform, font, "合成果园", 120,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(900f, 170f),
                TextAnchor.MiddleCenter, new Color(1f, 0.97f, 0.90f));
            var titleShadow = title.gameObject.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0.28f, 0.14f, 0.08f, 0.85f);
            titleShadow.effectDistance = new Vector2(5f, -5f);

            // 适龄提示角标（参考图左上角同款位置）。
            var badge = CreateImage("AgeBadge", root.transform, Color.white);
            var badgeSprite = LoadArt("badge_plate");
            if (badgeSprite != null)
            {
                badge.sprite = badgeSprite;
                badge.type = Image.Type.Sliced;
            }
            else
            {
                badge.color = new Color(0.35f, 0.75f, 0.95f, 1f);
            }

            Place(badge.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(30f, -30f), new Vector2(190f, 96f));
            CreateText("Age", badge.transform, font, "12+", 44,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(180f, 56f),
                TextAnchor.MiddleCenter, new Color(0.35f, 0.16f, 0.30f));
            CreateText("AgeHint", badge.transform, font, "适龄提示", 24,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -28f), new Vector2(180f, 32f),
                TextAnchor.MiddleCenter, new Color(0.35f, 0.16f, 0.30f));

            var advisory = CreateImage("Advisory", root.transform, new Color(0f, 0f, 0f, 0.5f));
            Place(advisory.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 340f), new Vector2(960f, 210f));
            CreateText("Text", advisory.transform, font,
                "《健康游戏忠告》\n抵制不良游戏，拒绝盗版游戏。注意自我保护，谨防受骗上当\n" +
                "适度游戏益脑，沉迷游戏伤身。合理安排时间，享受健康生活", 30,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(930f, 190f),
                TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 1f));

            view.loadingPercentText = CreateText("Percent", root.transform, font, "0%", 42,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 236f), new Vector2(600f, 56f),
                TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.96f));

            var track = CreateImage("ProgressTrack", root.transform, new Color(0.35f, 0.22f, 0.13f, 1f));
            Place(track.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 176f), new Vector2(900f, 40f));

            var trackSprite = LoadArt("bar_track");
            if (trackSprite != null)
            {
                track.sprite = trackSprite;
                track.type = Image.Type.Sliced;
                track.color = Color.white;
            }

            var fill = CreateImage("ProgressFill", track.transform, new Color(1f, 0.66f, 0.15f, 1f));
            Stretch(fill.rectTransform);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;

            var barSprite = LoadArt("bar_fill");
            if (barSprite != null)
            {
                fill.sprite = barSprite;
                fill.color = Color.white;
            }

            view.loadingProgressFill = fill;

            view.loadingHintText = CreateText("Hint", root.transform, font, "加载中…", 32,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 118f), new Vector2(600f, 48f),
                TextAnchor.MiddleCenter, new Color(1f, 0.97f, 0.88f, 0.95f));

            view.loadingPanel.SetVisible(false);
        }

        // ── 基础控件 ─────────────────────────────────────────────────

        /// <summary>
        /// 创建一个面板根节点：<see cref="UiPanel"/> + CanvasGroup + 半透明遮罩（+ 可选面板底板）。
        ///
        /// <para>场景里的初始状态是 <b>alpha=1 且 active=false</b>：这样在编辑器里手动勾上 active
        /// 就能直接看到面板内容来调布局（此前 alpha 被写成 0，勾 active 也看不见，需求方实测踩到）。</para>
        /// </summary>
        private static UiPanel CreatePanelRoot(string name, Transform parent, out Transform content,
            bool dimOnly = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            Stretch(rect);

            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;

            var panelComponent = go.AddComponent<UiPanel>();

            var dim = CreateImage("Dim", go.transform, DimColor);
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);

            if (dimOnly)
            {
                content = go.transform;
                return panelComponent;
            }

            var panel = CreateImage("Panel", go.transform, PanelColor);
            Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(900f, 1240f));

            // 弹窗面板底（九宫格）。不变量：九宫格素材必须按 PPU=100（= Canvas.referencePixelsPerUnit）
            // 导入，边框才会 1:1 落在设计单位上。PPU 越小边框越大（100/PPU 倍）：曾把 PPU 设成 1，
            // 边框被放大 100 倍后超过 900×1240 的面板尺寸，Unity 只能把边框压满整个 RectTransform，
            // 素材被整体拉伸成「大白椭圆」（2026-09-12 需求方实测）。见 `UiArtSlicingTests`。
            var panelSprite = LoadArt("panel_popup");
            if (panelSprite != null)
            {
                panel.sprite = panelSprite;
                panel.type = Image.Type.Sliced;
                panel.color = Color.white;
            }

            content = panel.transform;
            return panelComponent;
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

        /// <summary>
        /// 创建一段 TMP 文本。使用 TextMeshPro 而不是 legacy <c>Text</c>：
        /// WebGL / 微信小游戏无法访问系统字体，legacy 动态字体会在真机上显示为方块（决策 D16）。
        /// 字形来自场景引用的 `SIMYOU SDF`（静态烘焙）。
        /// </summary>
        private static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font,
            string content, int fontSize, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition,
            Vector2 size, TextAnchor alignment, Color? color = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            Place(rect, anchorMin, anchorMax, anchoredPosition, size);

            var text = go.AddComponent<TextMeshProUGUI>();
            if (font != null)
                text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = ToTmpAlignment(alignment);
            text.color = color ?? TextColor;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableWordWrapping = false;
            return text;
        }

        /// <summary>uGUI <see cref="TextAnchor"/> → TMP 对齐。两者枚举数值不同，不能直接转换。</summary>
        private static TextAlignmentOptions ToTmpAlignment(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Center;
            }
        }

        private static Button CreateButton(string name, Transform parent, TMP_FontAsset font, string label, int fontSize,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, Color color,
            string iconName = null)
        {
            var button = CreateButtonWithLabel(name, parent, font, label, fontSize, anchorMin, anchorMax,
                anchoredPosition, size, color, out _, iconName);
            return button;
        }

        private static Button CreateButtonWithLabel(string name, Transform parent, TMP_FontAsset font, string label,
            int fontSize, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size,
            Color color, out TextMeshProUGUI labelText, string iconName = null)
        {
            var image = CreateImage(name, parent, color);
            Place(image.rectTransform, anchorMin, anchorMax, anchoredPosition, size);
            image.raycastTarget = true;

            // 美术按钮底（九宫格）：按角色色相挑一张底图，再用轻染色保留角色语义色。
            var sprite = ResolveButtonSprite(color, out var tint);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
                image.color = tint;
            }

            var button = image.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.94f, 0.94f, 0.94f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f);
            button.colors = colors;

            var square = size.x <= size.y * 1.25f;
            var icon = LoadArt(iconName);
            if (icon != null)
            {
                var iconImage = CreateImage("Icon", image.transform, Color.white);
                iconImage.sprite = icon;
                iconImage.preserveAspect = true;

                var iconSize = Mathf.Min(size.x, size.y) * 0.46f;
                Place(iconImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    square ? new Vector2(0f, size.y * 0.14f) : new Vector2(-size.x * 0.5f + size.y * 0.62f, 0f),
                    new Vector2(iconSize, iconSize));
            }

            labelText = CreateText("Label", image.transform, font, label, fontSize,
                new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
            labelText.rectTransform.offsetMin = Vector2.zero;
            labelText.rectTransform.offsetMax = Vector2.zero;

            // 带图标的宽按钮：文字让出左侧图标位，避免压在一起。
            if (icon != null && !square)
            {
                labelText.rectTransform.offsetMin = new Vector2(size.y * 0.85f, 0f);
            }
            else if (icon != null)
            {
                labelText.rectTransform.offsetMin = new Vector2(0f, 0f);
                labelText.rectTransform.offsetMax = new Vector2(0f, -size.y * 0.34f);
            }

            return button;
        }

        /// <summary>按角色色挑美术按钮底：暖色→黄底、冷色→蓝底、其余→紫底；返回轻染色。</summary>
        private static Sprite ResolveButtonSprite(Color color, out Color tint)
        {
            tint = Color.Lerp(Color.white, color, 0.22f);

            var max = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            var min = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
            var saturation = max <= 0.001f ? 0f : (max - min) / max;

            if (max < 0.2f || saturation < 0.12f)
                return LoadArt("button_secondary");

            if (color.b >= color.r)
                return LoadArt("button_accent");

            if (color.g > color.r * 1.05f && color.g > color.b * 1.05f)
                return LoadArt("button_secondary");

            return LoadArt("button_main");
        }

        /// <summary>
        /// UI 美术资源根目录（由美术包挑选复制而来，单张 UI 图，`spriteMode = Single`）。
        ///
        /// <para>**刻意不放在 `Resources/` 下**：`Resources` 里的资源会被**无条件**打进包，
        /// 而运行时的 `Image`/`SpriteRenderer` 早已持有序列化的 sprite 引用——也就是说，
        /// 只要被场景引用就会随包分发，放进 `Resources` 反而丢掉「没人引用就不打包」的保护。
        /// 实测：本目录 28 张图里 4 张没有任何引用（其中 `bg_night.png` 一张就 5.0MB），
        /// 放在 `Resources` 下时这 4.9MB 会被白白打进首包；移出后它们自然不进包，
        /// 文件仍留在工程里随时可用。</para>
        ///
        /// <para>这里用 `AssetDatabase` 而不是 `Resources.Load`：本类在 Editor 程序集，
        /// 构建 UI 是编辑期行为，运行时不需要这条路径。</para>
        /// </summary>
        public const string ArtSpriteRoot = "Assets/UI/Art/";

        /// <summary>加载美术资源（<see cref="ArtSpriteRoot"/>）。缺失返回 null，调用方回退纯色占位。</summary>
        private static Sprite LoadArt(string name) =>
            string.IsNullOrEmpty(name)
                ? null
                : AssetDatabase.LoadAssetAtPath<Sprite>(ArtSpriteRoot + name + ".png");

        private static Toggle CreateToggle(string name, Transform parent, TMP_FontAsset font, string label, Vector2 position)
            => CreateToggle(name, parent, font, label, position, withVolumeBar: false, out _, out _);

        /// <summary>
        /// 设置行的开关。<paramref name="withVolumeBar"/> 为真时在行内右侧再放一条音量滑条
        /// （2026-09-12 需求方反馈「音量条丢失」后按美术包 Settings_Demo 的 Sound/Music Slider 补回）：
        /// 勾选框管开关、滑条管音量，行高与既有一致，不额外占用面板纵向空间。
        /// </summary>
        private static Toggle CreateToggle(string name, Transform parent, TMP_FontAsset font, string label,
            Vector2 position, bool withVolumeBar, out Slider volumeBar, out Image volumeFill)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var rect = root.AddComponent<RectTransform>();
            Place(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), position, new Vector2(680f, 84f));

            var box = CreateImage("Box", root.transform, new Color(1f, 1f, 1f, 1f));
            Place(box.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(46f, 0f), new Vector2(64f, 64f));
            box.raycastTarget = true;

            var boxSprite = LoadArt("toggle_box");
            if (boxSprite != null)
            {
                box.sprite = boxSprite;
                box.type = Image.Type.Sliced;
            }

            var check = CreateImage("Check", box.transform, new Color(0.35f, 0.78f, 0.45f));
            Place(check.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(40f, 40f));

            var checkSprite = LoadArt("toggle_check");
            if (checkSprite != null)
            {
                check.sprite = checkSprite;
                check.type = Image.Type.Sliced;
                check.color = Color.white;
                Place(check.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(56f, 56f));
            }

            var toggle = root.AddComponent<Toggle>();
            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.isOn = true;

            var labelWidth = withVolumeBar ? 150f : 400f;
            CreateText("Label", root.transform, font, label, 38,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(labelWidth, 60f),
                TextAnchor.MiddleLeft, PanelTextColor);

            if (withVolumeBar)
            {
                volumeBar = CreateVolumeBar(root.transform, out volumeFill);
            }
            else
            {
                volumeBar = null;
                volumeFill = null;
            }

            // 「勾选框关掉 → 音量条不可调」的联动与填充同步一样，必须由运行时装配的
            // PanelController 接线（编辑器期挂的监听不会被序列化，见其 HookButtons）。
            return toggle;
        }

        /// <summary>音量条尺寸：轨道取自美术包 Medium 版 `sound-bar-container`（390×44），填充同 `sound-bar-full`（376×24）。</summary>
        private const float VolumeBarWidth = 390f;

        private const float VolumeBarHeight = 44f;
        private const float VolumeFillWidth = 376f;
        private const float VolumeFillHeight = 24f;
        private const float VolumeHandleSize = 34f;

        /// <summary>
        /// 音量滑条：深色轨道 + 分段填充 + 滑钮。
        ///
        /// 两个实现要点：
        /// ① 填充用 <see cref="Image.Type.Filled"/> 按音量揭示（分段间距与素材一致），**不交给 Slider 拉锚点**——
        ///    Slider 默认会改 fill 的锚点，整条分段会被横向压扁；
        /// ② 滑钮（handleRect）必须存在，否则 Slider 的拖拽判定没有参考矩形，拖不动。
        /// </summary>
        private static Slider CreateVolumeBar(Transform parent, out Image fillImage)
        {
            var root = new GameObject("VolumeBar");
            root.transform.SetParent(parent, false);
            var rect = root.AddComponent<RectTransform>();
            Place(rect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(270f, 0f),
                new Vector2(VolumeBarWidth, VolumeBarHeight));

            var track = CreateImage("Track", root.transform, new Color(0.12f, 0.11f, 0.29f, 1f));
            Stretch(track.rectTransform);
            track.raycastTarget = true;

            var trackSprite = LoadArt("sound_bar_track");
            if (trackSprite != null)
            {
                track.sprite = trackSprite;
                track.color = Color.white;
            }

            var fill = CreateImage("Fill", root.transform, Color.white);
            Place(fill.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(VolumeFillWidth, VolumeFillHeight));

            var fillSprite = LoadArt("sound_bar_fill");
            if (fillSprite != null)
            {
                fill.sprite = fillSprite;
                fill.color = Color.white;
            }

            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;

            // 滑钮滑区左右各内缩半个滑钮宽，保证 0% / 100% 时滑钮仍在轨道内。
            var handleArea = new GameObject("HandleArea");
            handleArea.transform.SetParent(root.transform, false);
            var areaRect = handleArea.AddComponent<RectTransform>();
            Stretch(areaRect);
            areaRect.offsetMin = new Vector2(VolumeHandleSize * 0.5f, 0f);
            areaRect.offsetMax = new Vector2(-VolumeHandleSize * 0.5f, 0f);

            var handle = CreateImage("Handle", handleArea.transform, Color.white);
            Place(handle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(VolumeHandleSize, VolumeHandleSize));
            handle.sprite = fillSprite != null ? fillSprite : trackSprite;
            handle.preserveAspect = true;
            handle.raycastTarget = false;

            var slider = root.AddComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.targetGraphic = track;
            slider.fillRect = null;
            slider.handleRect = handle.rectTransform;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = 1f;

            // 刻意不在这里挂 onValueChanged：编辑器期挂的监听不会被序列化进场景，
            // 「填充 + 音量」与滑条的绑定统一交给运行时装配的 PanelController（见其 HookButtons），
            // 否则实机上会出现「音量变了、填充条却一直满格」。
            fillImage = fill;
            return slider;
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

        /// <summary>
        /// 定位 UI 元素。pivot 跟随锚点：贴边锚点使用同侧 pivot，使 anchoredPosition 的语义统一为
        /// 「距该边的距离」；居中或拉伸锚点使用 0.5。这样上/右贴边的元素不会被裁到屏幕外。
        /// </summary>
        private static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(ResolvePivot(anchorMin.x, anchorMax.x), ResolvePivot(anchorMin.y, anchorMax.y));
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static float ResolvePivot(float anchorMin, float anchorMax)
        {
            if (!Mathf.Approximately(anchorMin, anchorMax))
                return 0.5f;

            if (Mathf.Approximately(anchorMin, 0f))
                return 0f;

            if (Mathf.Approximately(anchorMin, 1f))
                return 1f;

            return 0.5f;
        }
    }
}
