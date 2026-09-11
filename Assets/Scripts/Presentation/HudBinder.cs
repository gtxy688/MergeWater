using System.Collections.Generic;
using MergeWater.Core;
using UnityEngine;

namespace MergeWater.Presentation
{
    /// <summary>
    /// HUD 与表现的总绑定器：唯一订阅 <c>SessionEvents</c> 的地方（决策 D10），
    /// 每帧读取只读快照刷新视图，并把事件转发给 <see cref="FeedbackDirector"/> 与面板。
    /// </summary>
    public sealed class HudBinder : MonoBehaviour
    {
        [SerializeField] private HudView view;
        [SerializeField] private PanelController panels;
        [SerializeField] private FeedbackDirector feedback;
        [SerializeField] private AimPreviewView preview;
        [SerializeField] private DangerLineView dangerLine;
        [SerializeField] private Sprite fruitSprite;
        [SerializeField] private Color nextFruitColor = Color.white;

        private readonly List<Vector2> _previewPoints = new List<Vector2>(128);

        private ISessionView _session;
        private IAimSource _aim;
        private GameBalance _balance;
        private StageProgress _stageProgress;
        private int _lastScore = -1;
        private int _lastBestScore = -1;
        private int _lastCurrentLevel = -1;
        private int _lastNextLevel = -1;
        private bool _subscribed;

        public ISessionView Session => _session;

        public void Configure(HudView hudView, PanelController panelController, FeedbackDirector feedbackDirector,
            AimPreviewView aimPreview, DangerLineView lineView, Sprite sprite)
        {
            view = hudView;
            panels = panelController;
            feedback = feedbackDirector;
            preview = aimPreview;
            dangerLine = lineView;

            if (sprite != null)
                fruitSprite = sprite;
        }

        /// <summary>绑定一局。重复调用会先解绑上一局，避免事件串局（验收 E5）。</summary>
        public void Bind(ISessionView session, IAimSource aim, GameBalance balance, int bestScore)
        {
            Unbind();

            _session = session;
            _aim = aim;
            _balance = balance;

            if (_session == null)
                return;

            _lastScore = -1;
            _lastBestScore = -1;
            _lastCurrentLevel = -1;
            _lastNextLevel = -1;
            _stageProgress = _balance != null ? new StageProgress(_balance) : null;

            _session.Events.DropPerformed += OnDropPerformed;
            _session.Events.Merged += OnMerged;
            _session.Events.Scored += OnScored;
            _session.Events.ComboChanged += OnComboChanged;
            _session.Events.DangerStarted += OnDangerStarted;
            _session.Events.DangerEnded += OnDangerEnded;
            _session.Events.PhaseChanged += OnPhaseChanged;
            _session.Events.MilestoneReached += OnMilestoneReached;
            _subscribed = true;

            if (dangerLine != null && _balance != null)
                dangerLine.SetLine(_balance.DangerLineY, _balance.FieldHalfWidth + _balance.WallThickness, 0.05f);

            if (preview != null)
                preview.SetPendingVisible(true);

            RefreshSnapshot(force: true);
        }

        public void Unbind()
        {
            if (_session != null && _subscribed)
            {
                _session.Events.DropPerformed -= OnDropPerformed;
                _session.Events.Merged -= OnMerged;
                _session.Events.Scored -= OnScored;
                _session.Events.ComboChanged -= OnComboChanged;
                _session.Events.DangerStarted -= OnDangerStarted;
                _session.Events.DangerEnded -= OnDangerEnded;
                _session.Events.PhaseChanged -= OnPhaseChanged;
                _session.Events.MilestoneReached -= OnMilestoneReached;
            }

            _subscribed = false;
            _session = null;
        }

        public PanelController Panels => panels;

        private void OnDestroy() => Unbind();

        private void Update()
        {
            if (_session == null)
                return;

            RefreshSnapshot(force: false);
            RefreshPreview();
        }

        // ── 事件处理 ─────────────────────────────────────────────────

        private void OnDropPerformed(DropPerformedEvent evt)
        {
            preview?.SetPendingVisible(true);
        }

        private void OnMerged(MergeEvent evt)
        {
            var combo = _session != null ? _session.Snapshot.Combo : 1;
            feedback?.PlayMerge(evt, combo);
        }

        private void OnScored(ScoreEvent evt)
        {
            feedback?.PlayScore(evt.Position, evt.Delta, evt.Multiplier, evt.Combo);

            // 立刻刷新分数文本，避免依赖下一帧的 Update。
            _lastScore = evt.Total;
            _lastBestScore = _session != null ? _session.BestScore : evt.Total;

            if (view?.scoreText != null)
                view.scoreText.text = evt.Total.ToString();

            if (view?.bestScoreText != null)
                view.bestScoreText.text = $"最高分：{_lastBestScore}";
        }

        private void OnComboChanged(int combo)
        {
            if (view?.comboText == null || _session == null)
                return;

            var multiplier = _session.Snapshot.Multiplier;
            view.comboText.text = combo >= 2 ? $"{combo} 连击  x{multiplier:0.0}" : string.Empty;
        }

        private void OnDangerStarted(DangerViolation violation)
        {
            dangerLine?.SetPulsing(true);
            feedback?.PlayDanger(true);
        }

        private void OnDangerEnded()
        {
            dangerLine?.SetPulsing(false);
            feedback?.PlayDanger(false);
        }

        private void OnMilestoneReached(StageMilestone milestone)
        {
            feedback?.PlayClaim();
            panels?.ShowToast($"达到 {milestone.Score} 分，获得 1 个{milestone.Reward}");
        }

        private void OnPhaseChanged(RoundPhase from, RoundPhase to)
        {
            RefreshSnapshot(force: true);

            switch (to)
            {
                case RoundPhase.Reviving:
                    feedback?.PlayFail();
                    preview?.Clear();
                    ShowSettlement("差一步就过了", canRevive: true);
                    break;

                case RoundPhase.GameOver:
                    feedback?.PlayFail();
                    preview?.Clear();
                    ShowSettlement("本局结束", canRevive: false);
                    break;

                case RoundPhase.Playing:
                    panels?.Hide(PanelId.Settlement);
                    preview?.SetPendingVisible(true);
                    break;

                case RoundPhase.Ready:
                    panels?.Hide(PanelId.Settlement);
                    break;
            }
        }

        private void ShowSettlement(string title, bool canRevive)
        {
            if (panels == null || _session == null)
                return;

            panels.SetSettlement(title, _session.Score, _session.BestScore,
                canRevive ? "看激励视频清除最高一簇，继续这一局" : "再来一局试试更高连击",
                canRevive, showShare: true);
            panels.Show(PanelId.Settlement);
        }

        // ── 每帧刷新 ─────────────────────────────────────────────────

        private void RefreshSnapshot(bool force)
        {
            if (_session == null)
                return;

            var snapshot = _session.Snapshot;

            if (force || snapshot.Score != _lastScore || snapshot.BestScore != _lastBestScore)
            {
                _lastScore = snapshot.Score;
                _lastBestScore = snapshot.BestScore;

                if (view?.scoreText != null)
                    view.scoreText.text = snapshot.Score.ToString();

                if (view?.bestScoreText != null)
                    view.bestScoreText.text = $"最高分：{snapshot.BestScore}";

                if (view?.stageProgressFill != null && _stageProgress != null)
                    view.stageProgressFill.fillAmount = _stageProgress.GetNormalizedProgress(snapshot.Score);

                if (view?.stageProgressLabel != null)
                    view.stageProgressLabel.text = StageLabel(snapshot);
            }

            if (force || snapshot.NextLevel != _lastNextLevel)
            {
                _lastNextLevel = snapshot.NextLevel;

                if (view?.nextFruitLabel != null)
                    view.nextFruitLabel.text = TierName(snapshot.NextLevel);

                if (view?.nextFruitIcon != null)
                    view.nextFruitIcon.color = FruitPalette.ForLevel(snapshot.NextLevel);
            }

            if (_balance != null && preview != null && (force || snapshot.CurrentLevel != _lastCurrentLevel))
            {
                _lastCurrentLevel = snapshot.CurrentLevel;
                var tier = _balance.GetTier(snapshot.CurrentLevel);
                preview.SetPendingFruit(snapshot.CurrentLevel, fruitSprite,
                    FruitPalette.ForLevel(snapshot.CurrentLevel), tier.Radius * 2f);
            }
        }

        private void RefreshPreview()
        {
            if (preview == null || _aim == null || _balance == null || _session == null)
                return;

            var snapshot = _session.Snapshot;
            var active = snapshot.Phase == RoundPhase.Ready || snapshot.Phase == RoundPhase.Playing;
            preview.SetPendingVisible(active);

            if (!active)
                return;

            var state = _aim.State;
            preview.SetPendingBasePosition(new Vector2(state.X, _balance.DropSpawnY));

            if (state.IsAiming && !state.IsItemAim)
            {
                var count = _aim.BuildPreview(_previewPoints);
                preview.SetTrajectory(_previewPoints, count >= 2);
            }
            else
            {
                preview.SetTrajectory(null, false);
            }
        }

        private string TierName(int level)
        {
            if (_balance == null)
                return level.ToString();

            var tier = _balance.GetTier(level);
            return tier.IsValid ? tier.DisplayName : level.ToString();
        }

        private string StageLabel(RoundSnapshot snapshot)
        {
            if (_balance == null)
                return string.Empty;

            var progress = new StageProgress(_balance);
            if (progress.TryGetNext(out var next))
                return $"阶段目标 {snapshot.Score}/{next.Score}";

            return "阶段目标已全部达成";
        }
    }
}
