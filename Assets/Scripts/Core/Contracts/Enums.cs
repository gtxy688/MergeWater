namespace MergeWater.Core
{
    /// <summary>局内阶段。转换规则见 Docs/architecture/03-session.md。</summary>
    public enum RoundPhase
    {
        Booting = 0,
        Ready = 1,
        Playing = 2,
        Reviving = 3,
        GameOver = 4
    }

    /// <summary>道具种类。撤销/炸弹/锤子/摇一摇（R11–R14）。</summary>
    public enum ItemKind
    {
        None = 0,
        Undo = 1,
        Bomb = 2,
        Hammer = 3,
        Shake = 4
    }

    /// <summary>道具使用结果，用于区分「拒绝」与「已生效」。</summary>
    public enum ItemUseResult
    {
        Applied = 0,
        NoStock = 1,
        NotAllowed = 2,
        NoTarget = 3,
        AdRejected = 4,
        Cancelled = 5
    }

    /// <summary>复活请求结果。失败时局状态不变。</summary>
    public enum ReviveOutcome
    {
        Applied = 0,
        NotInGameOver = 1,
        AlreadyUsed = 2,
        Cooldown = 3,
        AdFailed = 4,
        Unavailable = 5
    }

    /// <summary>广告点位。</summary>
    public enum AdPlacement
    {
        Revive = 0,
        ItemGrant = 1,
        Shake = 2,
        Gift = 3,
        Interstitial = 4
    }

    /// <summary>广告位放行判定。</summary>
    public enum AdDecision
    {
        Allowed = 0,
        DeniedByToggle = 1,
        DailyCapReached = 2,
        CooldownActive = 3,
        Unavailable = 4,
        NotAllowedNow = 5
    }

    /// <summary>激励视频结果。</summary>
    public enum RewardedResult
    {
        Completed = 0,
        Skipped = 1,
        Failed = 2,
        Unavailable = 3
    }

    /// <summary>插屏结果。</summary>
    public enum InterstitialResult
    {
        Shown = 0,
        Skipped = 1,
        Failed = 2,
        Unavailable = 3
    }

    /// <summary>分享结果。回调不可信，只按「发起即记录」处理（V2.19）。</summary>
    public enum ShareResult
    {
        Shared = 0,
        Cooldown = 1,
        Unavailable = 2
    }

    /// <summary>道具发放结果。</summary>
    public enum GrantResult
    {
        Granted = 0,
        DailyCapReached = 1,
        Failed = 2
    }

    /// <summary>道具消耗结果。</summary>
    public enum SpendResult
    {
        Spent = 0,
        NoStock = 1,
        NotAllowed = 2
    }

    /// <summary>广告位放行判定结果，附带可读原因供 UI 展示。</summary>
    public readonly struct AdDecisionResult
    {
        public readonly AdDecision Decision;
        public readonly string Reason;

        public AdDecisionResult(AdDecision decision, string reason)
        {
            Decision = decision;
            Reason = reason;
        }

        public bool IsAllowed => Decision == AdDecision.Allowed;

        public static AdDecisionResult Allowed() => new AdDecisionResult(AdDecision.Allowed, string.Empty);

        public static AdDecisionResult Denied(AdDecision decision, string reason) =>
            new AdDecisionResult(decision, reason);
    }
}
