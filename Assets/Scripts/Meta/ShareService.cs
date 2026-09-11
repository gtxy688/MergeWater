using System;
using MergeWater.Core;

namespace MergeWater.Meta
{
    /// <summary>
    /// 分享挑战（R19）。回调不可信，只按「发起即记录 + 60s 冷却」处理，文案不做「必得」承诺（V2.19）。
    /// 实际拉起微信分享由平台层负责，这里只生成文案与记账。
    /// </summary>
    public sealed class ShareService : IShareService
    {
        private readonly SaveData _data;
        private readonly SaveService _save;
        private readonly IClock _clock;
        private readonly GameBalance _balance;

        public ShareService(SaveData data, SaveService save, IClock clock, GameBalance balance)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _save = save;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        public ShareResult ShareChallenge(int score, int bestCombo, out string text)
        {
            var decision = AdsPlacementRules.CanShare(_data, _clock, _balance);
            if (!decision.IsAllowed)
            {
                text = decision.Reason;
                return ShareResult.Cooldown;
            }

            text = BuildChallengeText(score, bestCombo);
            _data.lastShareUtcTicks = _clock.Now.ToUniversalTime().Ticks;
            _save?.Save();
            return ShareResult.Shared;
        }

        public static string BuildChallengeText(int score, int bestCombo)
        {
            var combo = Math.Max(0, bestCombo);
            return $"我在《合成果园》拿到 {score} 分、最高 {combo} 连击，来挑战我！口令：MW{score}";
        }
    }
}
