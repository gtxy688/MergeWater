using System;

namespace MergeWater.Core
{
    /// <summary>
    /// 连击计数与倍率（V2.1–V2.3）。以显式 <see cref="Advance"/> 驱动，便于脱离物理帧单测。
    /// 倍率 = 1 + 0.1×(连击−1)，上限 2.0；超过窗口未合成则归零。
    /// </summary>
    public sealed class ComboTracker
    {
        private readonly float _window;
        private readonly float _step;
        private readonly float _cap;
        private float _elapsed;

        public int Combo { get; private set; }

        public float Multiplier { get; private set; }

        public ComboTracker(GameBalance balance)
            : this(balance.ComboWindowSeconds, balance.ComboMultiplierStep, balance.ComboMultiplierCap)
        {
        }

        public ComboTracker(float windowSeconds, float multiplierStep, float multiplierCap)
        {
            _window = Math.Max(0f, windowSeconds);
            _step = Math.Max(0f, multiplierStep);
            _cap = Math.Max(1f, multiplierCap);
            Reset();
        }

        public void Reset()
        {
            Combo = 0;
            _elapsed = 0f;
            Multiplier = 1f;
        }

        /// <summary>记录一次成功合成：连击 +1 并重置窗口计时。</summary>
        public void RegisterMerge()
        {
            Combo++;
            _elapsed = 0f;
            Multiplier = Math.Min(_cap, 1f + _step * (Combo - 1));
        }

        /// <summary>推进窗口计时；恰好等于窗口时长时不归零（严格大于才归零）。</summary>
        public void Advance(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || Combo == 0)
                return;

            _elapsed += deltaSeconds;
            if (_elapsed > _window)
                Reset();
        }
    }
}
