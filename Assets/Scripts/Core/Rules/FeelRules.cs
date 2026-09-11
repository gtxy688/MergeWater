using UnityEngine;

namespace MergeWater.Core
{
    /// <summary>手感数值的派生计算（V2.23/V2.24）。</summary>
    public static class FeelRules
    {
        /// <summary>连击音阶：+1 半音/连击，封顶 <paramref name="semitoneCap"/> 个半音（V2.24）。</summary>
        public static float ComboPitch(int combo, int semitoneCap)
        {
            var semitones = Mathf.Clamp(combo - 1, 0, Mathf.Max(0, semitoneCap));
            return Mathf.Pow(2f, semitones / 12f);
        }

        /// <summary>顿帧时长：随连击从最小值线性增至最大值（V2.23）。</summary>
        public static float HitStopSeconds(int combo, GameBalance balance)
        {
            var maxCombo = Mathf.Max(2, balance.HitStopMaxCombo);
            var t = Mathf.Clamp01((combo - 1f) / (maxCombo - 1f));
            return Mathf.Lerp(balance.HitStopMinSeconds, balance.HitStopMaxSeconds, t);
        }

        /// <summary>震屏档位：1 连击无额外震屏，2–3 为 1 档，4 以上为 2 档（配合表现层三档强度）。</summary>
        public static int ShakeTier(int combo)
        {
            if (combo <= 1) return 0;
            if (combo <= 3) return 1;
            return 2;
        }
    }
}
