using System;

namespace MergeWater.Core
{
    /// <summary>
    /// 一局的事件契约（架构决策 A2：不使用静态事件总线）。
    /// 由 M3 的 <c>RoundSession</c> 每局实例化并公开 <c>Events</c>；
    /// M5 订阅、M7 传引用；重开一局必须重新订阅。
    /// </summary>
    public sealed class SessionEvents
    {
        public event Action<DropPerformedEvent> DropPerformed;
        public event Action<MergeEvent> Merged;
        public event Action<ScoreEvent> Scored;
        public event Action<int> ComboChanged;
        public event Action<DangerViolation> DangerStarted;
        public event Action DangerEnded;
        public event Action<RoundPhase, RoundPhase> PhaseChanged;
        public event Action<StageMilestone> MilestoneReached;
        public event Action<ItemKind, ItemUseResult> ItemUsed;
        public event Action<ReviveOutcome> ReviveResolved;

        public void PublishDropPerformed(DropPerformedEvent evt) => DropPerformed?.Invoke(evt);

        public void PublishMerged(MergeEvent evt) => Merged?.Invoke(evt);

        public void PublishScored(ScoreEvent evt) => Scored?.Invoke(evt);

        public void PublishComboChanged(int combo) => ComboChanged?.Invoke(combo);

        public void PublishDangerStarted(DangerViolation violation) => DangerStarted?.Invoke(violation);

        public void PublishDangerEnded() => DangerEnded?.Invoke();

        public void PublishPhaseChanged(RoundPhase from, RoundPhase to) => PhaseChanged?.Invoke(from, to);

        public void PublishMilestoneReached(StageMilestone milestone) => MilestoneReached?.Invoke(milestone);

        public void PublishItemUsed(ItemKind kind, ItemUseResult result) => ItemUsed?.Invoke(kind, result);

        public void PublishReviveResolved(ReviveOutcome outcome) => ReviveResolved?.Invoke(outcome);
    }
}
