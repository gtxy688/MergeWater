namespace MergeWater.Core
{
    /// <summary>
    /// 表现层可用的局内只读视图（M3 实现，M5 只依赖本接口）。
    /// 这样 M5 不需要引用 M3 的具体类型，符合架构总览的依赖方向。
    /// </summary>
    public interface ISessionView
    {
        /// <summary>本局事件契约（订阅者必须在解绑时取消订阅，跨局重新绑定）。</summary>
        SessionEvents Events { get; }

        /// <summary>只读快照。</summary>
        RoundSnapshot Snapshot { get; }

        int Score { get; }

        int BestScore { get; }

        /// <summary>当前是否允许操作（投放与道具）。</summary>
        bool CanAct { get; }

        /// <summary>本局已使用的复活次数。</summary>
        int RevivesUsed { get; }
    }
}
