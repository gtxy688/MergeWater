using System.Collections.Generic;
using MergeWater.Core;
using MergeWater.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace MergeWater.Tests.PlayMode
{
    /// <summary>M5 测试替身：可控的只读局内视图。</summary>
    internal sealed class SessionStub : ISessionView
    {
        public SessionEvents Events { get; } = new SessionEvents();

        public RoundSnapshot Snapshot { get; set; }

        public int Score { get; set; }

        public int BestScore { get; set; }

        public bool CanAct { get; set; } = true;

        public int RevivesUsed { get; set; }

        public void RaiseScored(int delta, int total, float multiplier, int combo, int level = 2)
        {
            Score = total;
            BestScore = Mathf.Max(BestScore, total);
            Snapshot = Build(total, combo, multiplier);
            Events.PublishScored(new ScoreEvent(delta, total, multiplier, combo, level, Vector2.zero));
        }

        public void RaiseMerged(int resultLevel = 2, int combo = 1)
        {
            Snapshot = Build(Score, combo, 1f + 0.1f * Mathf.Max(0, combo - 1));
            Events.PublishMerged(new MergeEvent(0, 1, resultLevel - 1, resultLevel, Vector2.zero));
        }

        public void RaiseComboChanged(int combo) => Events.PublishComboChanged(combo);

        public void RaisePhase(RoundPhase from, RoundPhase to) => Events.PublishPhaseChanged(from, to);

        public void RaiseDanger() =>
            Events.PublishDangerStarted(new DangerViolation(1, 3, 3.9f, 0.5f));

        public void RaiseDangerEnded() => Events.PublishDangerEnded();

        public void RaiseMilestone(StageMilestone milestone) => Events.PublishMilestoneReached(milestone);

        private RoundSnapshot Build(int score, int combo, float multiplier) =>
            new RoundSnapshot(score, score, combo, multiplier, RoundPhase.Playing, 3, 1, 2, false, false, 0);
    }

    /// <summary>M5 测试替身：可控的瞄准来源。</summary>
    internal sealed class AimStub : IAimSource
    {
        public AimState State { get; set; } = AimState.Idle;

        public int PreviewPointCount { get; set; }

        public event System.Action<float> DropRequested;
        public event System.Action<ItemKind, Vector2> ItemTargetRequested;

        public void SetDropRadius(float radius)
        {
        }

        public void SetDropBounds(float minX, float maxX)
        {
        }

        public void SetInteractable(bool interactable)
        {
        }

        public void BeginItemAim(ItemKind kind, float radius)
        {
        }

        public void CancelItemAim()
        {
        }

        public int BuildPreview(List<Vector2> points)
        {
            points?.Clear();
            if (PreviewPointCount > 0 && points != null)
            {
                for (var i = 0; i < PreviewPointCount; i++)
                    points.Add(new Vector2(0f, 5f - i));
            }

            return PreviewPointCount;
        }

        public void RaiseDrop(float x) => DropRequested?.Invoke(x);

        public void RaiseItemTarget(ItemKind kind, Vector2 point) => ItemTargetRequested?.Invoke(kind, point);
    }

    /// <summary>
    /// M5 测试用最小 HUD：包含 HudBinder 与 PanelController 实际会访问的元素，
    /// 不使用 HudBuilder/系统字体，保证批量模式下稳定。
    /// </summary>
    internal sealed class PresentationHarness
    {
        public GameObject Root;
        public GameObject CameraObject;
        public Camera Camera;
        public HudView View;
        public PanelController Panels;
        public FeedbackDirector Feedback;
        public AudioDirector Audio;
        public AimPreviewView Preview;
        public DangerLineView DangerLine;

        public static PresentationHarness Create(Transform parent = null)
        {
            var harness = new PresentationHarness();

            harness.Root = new GameObject("PresentationHarness");
            if (parent != null)
                harness.Root.transform.SetParent(parent, false);

            harness.CameraObject = new GameObject("HarnessCamera");
            harness.Camera = harness.CameraObject.AddComponent<Camera>();
            harness.Camera.orthographic = true;
            harness.Camera.orthographicSize = 5.6f;
            harness.CameraObject.transform.position = new Vector3(0f, 0.4f, -10f);

            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(harness.Root.transform, false);
            canvasGo.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();

            var font = ResolveFont();
            var view = harness.Root.AddComponent<HudView>();

            view.scoreText = MakeText(canvasGo.transform, font, "Score");
            view.bestScoreText = MakeText(canvasGo.transform, font, "Best");
            view.comboText = MakeText(canvasGo.transform, font, "Combo");
            view.stageProgressLabel = MakeText(canvasGo.transform, font, "StageLabel");
            view.nextFruitLabel = MakeText(canvasGo.transform, font, "NextLabel");
            view.stageProgressFill = MakeImage(canvasGo.transform, "StageFill");
            view.nextFruitIcon = MakeImage(canvasGo.transform, "NextIcon");
            view.settingsButton = MakeButton(canvasGo.transform, font, "Settings");
            view.retryButton = MakeButton(canvasGo.transform, font, "Retry");
            view.shareButton = MakeButton(canvasGo.transform, font, "Share");
            view.reviveButton = MakeButton(canvasGo.transform, font, "Revive");
            view.reviveLabel = view.reviveButton.GetComponentInChildren<Text>();
            view.privacyAcceptButton = MakeButton(canvasGo.transform, font, "PrivacyAccept");
            view.privacyDeclineButton = MakeButton(canvasGo.transform, font, "PrivacyDecline");
            view.privacyPanel = MakeGroup(canvasGo.transform, "Privacy");

            view.undoButton = MakeButton(canvasGo.transform, font, "Undo");
            view.bombButton = MakeButton(canvasGo.transform, font, "Bomb");
            view.hammerButton = MakeButton(canvasGo.transform, font, "Hammer");
            view.shakeButton = MakeButton(canvasGo.transform, font, "Shake");
            view.giftButton = MakeButton(canvasGo.transform, font, "Gift");

            view.settlementPanel = MakeGroup(canvasGo.transform, "Settlement");
            view.settlementTitleText = MakeText(canvasGo.transform, font, "SettlementTitle");
            view.settlementScoreText = MakeText(canvasGo.transform, font, "SettlementScore");
            view.settlementBestText = MakeText(canvasGo.transform, font, "SettlementBest");
            view.settlementHintText = MakeText(canvasGo.transform, font, "SettlementHint");

            view.leaderboardPanel = MakeGroup(canvasGo.transform, "Leaderboard");
            view.leaderboardText = MakeText(canvasGo.transform, font, "LeaderboardText");

            view.toastGroup = MakeGroup(canvasGo.transform, "Toast");
            view.toastText = MakeText(canvasGo.transform, font, "ToastText");

            harness.View = view;

            harness.Panels = harness.Root.AddComponent<PanelController>();
            harness.Panels.Configure(view);

            harness.Audio = harness.Root.AddComponent<AudioDirector>();
            harness.Audio.SetSfxEnabled(false);
            harness.Audio.SetMusicEnabled(false);

            harness.Feedback = harness.Root.AddComponent<FeedbackDirector>();
            harness.Feedback.SetBalance(GameBalance.CreateDefault());

            var previewGo = new GameObject("Preview");
            previewGo.transform.SetParent(harness.Root.transform, false);
            harness.Preview = previewGo.AddComponent<AimPreviewView>();
            var lineGo = new GameObject("Line");
            lineGo.transform.SetParent(previewGo.transform, false);
            var fruitGo = new GameObject("Fruit");
            fruitGo.transform.SetParent(previewGo.transform, false);
            harness.Preview.Configure(lineGo.AddComponent<LineRenderer>(), fruitGo.AddComponent<SpriteRenderer>());

            var dangerGo = new GameObject("Danger");
            dangerGo.transform.SetParent(harness.Root.transform, false);
            harness.DangerLine = dangerGo.AddComponent<DangerLineView>();
            var dangerLineGo = new GameObject("Line");
            dangerLineGo.transform.SetParent(dangerGo.transform, false);
            harness.DangerLine.Configure(dangerLineGo.AddComponent<LineRenderer>());

            return harness;
        }

        public HudBinder CreateBinder()
        {
            var binder = Root.AddComponent<HudBinder>();
            binder.Configure(View, Panels, Feedback, Preview, DangerLine, null);
            return binder;
        }

        public void Dispose()
        {
            if (Root != null)
                Object.Destroy(Root);

            if (CameraObject != null)
                Object.Destroy(CameraObject);

            Root = null;
            CameraObject = null;
        }

        private static Font ResolveFont()
        {
            try
            {
                return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch (System.Exception)
            {
                try
                {
                    return Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                catch (System.Exception)
                {
                    return null;
                }
            }
        }

        private static Text MakeText(Transform parent, Font font, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var text = go.AddComponent<Text>();
            if (font != null)
                text.font = font;
            text.text = string.Empty;
            return text;
        }

        private static Image MakeImage(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go.AddComponent<Image>();
        }

        private static Button MakeButton(Transform parent, Font font, string name)
        {
            var image = MakeImage(parent, name);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(image.transform, false);
            labelGo.AddComponent<RectTransform>();
            var label = labelGo.AddComponent<Text>();
            if (font != null)
                label.font = font;
            label.text = name;

            return button;
        }

        private static CanvasGroup MakeGroup(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go.AddComponent<CanvasGroup>();
        }
    }
}
