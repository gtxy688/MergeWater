using UnityEngine;

namespace MergeWater.Meta
{
    /// <summary>
    /// 广告侧诊断日志（2026-09-14）：默认**关闭**，由场景里 `GameBootstrapper.logDiagnosticsToConsole` 打开。
    ///
    /// <para>为什么需要：广告是异步的，出问题时表现往往是「点了没反应」——分不清是
    /// ①放行规则拒绝、②适配器不可用、③用户取消、④回调没回来。打开这个开关后，
    /// 每次请求/结果都会在 Console 留一行，排查不必再猜（默认关闭以符合 V2.47「运行期不刷 Console」）。</para>
    ///
    /// <para><b>关于 static</b>：这里只是一个**日志开关**（不是业务状态、更不是事件总线），
    /// 由组合根 `GameBootstrapper` 在启动时按 Inspector 勾选设置一次；硬约束 3 禁的是"静态事件总线"。</para>
    /// </summary>
    public static class AdsDiagnostics
    {
        public static bool Enabled { get; set; }

        public static void Log(string message)
        {
            if (Enabled)
                Debug.Log("[Ads] " + message);
        }

        public static string Describe(MergeWater.Core.RewardedResult result) => result switch
        {
            MergeWater.Core.RewardedResult.Completed => "Completed（可发奖）",
            MergeWater.Core.RewardedResult.Skipped => "Skipped（用户中途关闭）",
            MergeWater.Core.RewardedResult.Failed => "Failed（拉取/播放失败）",
            _ => "Unavailable（适配器不可用/播放中重复请求）"
        };
    }
}
