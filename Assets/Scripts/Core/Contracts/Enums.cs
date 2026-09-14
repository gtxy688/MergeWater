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

    /// <summary>道具种类（V2.58 起只剩清屏与摇一摇：炸弹/锤子随入口删除，R11–R14 收窄为 R11/R14）。</summary>
    public enum ItemKind
    {
        None = 0,
        Undo = 1,
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

    /// <summary>
    /// 广告适配器模式（2026-09-14）：未开通微信流量主、没有 `adUnitId` 时用 <see cref="Mock"/> 跑通全部流程；
    /// 拿到 `adunit-xxxxxxxx` 后，把场景里 <c>GameBootstrapper.adsMode</c> 改成 <see cref="WeChat"/> 并填 id 即可，
    /// **业务层（EconomyService / 复活流程 / UI）一行都不用改**。
    /// </summary>
    public enum AdsMode
    {
        /// <summary>模拟广告：Inspector 可切换 Success / Cancel / Error，并有 1~2 秒的假播放时长。</summary>
        Mock = 0,

        /// <summary>真实微信激励视频（`WX.CreateRewardedVideoAd`）：需要有效 adUnitId，且只在微信小游戏真机生效。</summary>
        WeChat = 1
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
