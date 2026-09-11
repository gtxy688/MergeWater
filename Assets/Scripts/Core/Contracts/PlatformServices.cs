using System;
using System.Collections.Generic;

namespace MergeWater.Core
{
    /// <summary>可注入时钟，便于测试每日上限与冷却。</summary>
    public interface IClock
    {
        DateTime Now { get; }

        /// <summary>本地日期键，格式 yyyy-MM-dd。</summary>
        string Today { get; }
    }

    public sealed class SystemClock : IClock
    {
        public DateTime Now => DateTime.Now;

        public string Today => DateTime.Now.ToString("yyyy-MM-dd");
    }

    /// <summary>存档原始字符串读写端口（M6 实现落盘）。</summary>
    public interface ISaveStore
    {
        bool Exists { get; }

        bool TryRead(out string json);

        bool Write(string json);

        /// <summary>把现有存档改名保留为备份（用于损坏或未来版本存档）。</summary>
        bool MoveToBackup();

        bool Delete();
    }

    /// <summary>激励视频与插屏适配器。回调式，便于测试与真实 SDK 对接。</summary>
    public interface IAdsService
    {
        /// <summary>适配器是否可用（离线/未初始化时为 false）。</summary>
        bool IsAvailable { get; }

        bool IsInitialized { get; }

        /// <summary>广告是否正在播放（用于冻结输入，避免播放期间误操作）。</summary>
        bool IsShowing { get; }

        /// <summary>隐私同意后才调用（R20）。</summary>
        void Initialize();

        void ShowRewarded(AdPlacement placement, Action<RewardedResult> onComplete);

        void ShowInterstitial(Action<InterstitialResult> onComplete);
    }

    /// <summary>埋点端口。同意隐私前不初始化（R20）。</summary>
    public interface IAnalyticsService
    {
        bool IsInitialized { get; }

        void Initialize();

        void Track(string eventName, params AnalyticsParam[] parameters);
    }

    /// <summary>本地排行榜端口。</summary>
    public interface ILeaderboardService
    {
        void Submit(string displayName, int score);

        IReadOnlyList<LeaderboardEntry> GetTop(int count, int page = 1);
    }

    /// <summary>分享端口。回调不可信，只按「发起即记录 + 冷却」处理（V2.19）。</summary>
    public interface IShareService
    {
        ShareResult ShareChallenge(int score, int bestCombo, out string text);
    }
}
