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

        /// <summary>与 <see cref="GameField"/> 共用同一条「等级 → 贴图 / 染色」规则（M7 运行时注入）。</summary>
        private FruitArt _art;

        private readonly List<Vector2> _previewPoints = new List<Vector2>(128);

        private ISessionView _session;
        private IAimSource _aim;
        private GameBalance _balance;
        private int _lastScore = -1;
        private int _lastBestScore = -1;
        private int _lastCurrentLevel = -1;
        private bool _subscribed;
        private float _playHalfWidth = -1f;

        public ISessionView Session => _session;

        /// <summary>
        /// 方案 B：场地有效半宽由相机可视宽度决定（M7 在运行时注入）。
        /// 未注入时回退到数值表的设计半宽（EditMode/脚手架测试即该路径）。
        /// </summary>
        public void SetPlayAreaHalfWidth(float halfWidth) => _playHalfWidth = halfWidth > 0f ? halfWidth : -1f;

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

        /// <summary>
        /// 注入「等级 → 水果外观」的正式美术（M7 在运行时调用，与 <see cref="GameField"/> 同一个实例）。
        /// 未注入时退化为「单一占位图 + 调色板染色」。
        /// </summary>
        public void SetFruitArt(FruitArt art) => _art = art;

        /// <summary>未注入正式美术时，用单一占位图构成退化集合，行为与占位美术时代完全一致。</summary>
        private FruitArt Art => _art ??= new FruitArt(null, fruitSprite);

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

            _session.Events.DropPerformed += OnDropPerformed;
            _session.Events.Merged += OnMerged;
            _session.Events.Scored += OnScored;
            // 需求方（2026-09-12）：连击提示不再走顶栏文本，改为在合成位置由
            // FeedbackDirector.PlayScore 用飘字给出更强反馈，因此这里不再订阅 ComboChanged。
            _session.Events.DangerStarted += OnDangerStarted;
            _session.Events.DangerEnded += OnDangerEnded;
            _session.Events.PhaseChanged += OnPhaseChanged;
            _session.Events.MilestoneReached += OnMilestoneReached;
            _subscribed = true;

            if (dangerLine != null && _balance != null)
            {
                // 方案 B：场地边界跟随屏幕，危险线要横跨整个可玩宽度（未注入时回退设计值）。
                var lineHalfWidth = (_playHalfWidth > 0f ? _playHalfWidth : _balance.FieldHalfWidth)
                                    + _balance.WallThickness;
                dangerLine.SetLine(_balance.DangerLineY, lineHalfWidth, 0.05f);
            }

            if (preview != null)
            {
                if (_balance != null)
                    preview.ConfigureReveal(_balance.NextFruitRevealDelaySeconds,
                        _balance.NextFruitRevealDurationSeconds);

                // 开局第一颗也走「渐显」出现，节奏与后续投放一致（延迟为 0）。
                preview.BeginPendingReveal(0f);
            }

            RefreshSnapshot(force: true);
        }

        public void Unbind()
        {
            if (_session != null && _subscribed)
            {
                _session.Events.DropPerformed -= OnDropPerformed;
                _session.Events.Merged -= OnMerged;
                _session.Events.Scored -= OnScored;
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
            // 下一颗待投水果稍等片刻再从屏幕中央渐显出现（V2.33/V2.34），
            // 而不是上一颗刚离手就立刻冒出。
            preview?.BeginPendingReveal();
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

        // 连击提示已改为在合成位置飘字（FeedbackDirector.PlayScore），顶栏不再有 comboText。

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
            // 需求方（2026-09-12）：里程碑不再发放奖励（奖励只通过广告获取），
            // 因此也不再弹「获得道具」飘字与 Toast；事件本身仍会发布，供埋点使用。
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

                // 需求方（2026-09-12）：删去「阶段目标 + 进度条」，顶栏只显示总分，故这里不再刷新阶段 UI。
            }

            // 需求方（2026-09-12）：删去右上角「NEXT 下一个水果」预览，故不再刷新 next 图标/文字。

            if (_balance != null && preview != null && (force || snapshot.CurrentLevel != _lastCurrentLevel))
            {
                _lastCurrentLevel = snapshot.CurrentLevel;
                var tier = _balance.GetTier(snapshot.CurrentLevel);
                var art = Art;
                preview.SetPendingFruit(snapshot.CurrentLevel, art.ForLevel(snapshot.CurrentLevel),
                    art.TintForLevel(snapshot.CurrentLevel), tier.Radius * 2f);
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
    }
}
