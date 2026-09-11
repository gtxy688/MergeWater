using System.Collections.Generic;
using MergeWater.Core;
using UnityEngine;

namespace MergeWater.Presentation
{
    /// <summary>
    /// 手感编排（R22、V2.20–V2.25）：由 <see cref="HudBinder"/> 在事件回调中调用，
    /// 自身不订阅事件（决策 D10）。
    /// </summary>
    public sealed class FeedbackDirector : MonoBehaviour
    {
        private static readonly float[] MergeShakeAmplitudes = { 0.08f, 0.15f, 0.24f };
        private const float DropShakeAmplitude = 0.05f;
        private const float FailShakeAmplitude = 0.3f;

        [SerializeField] private TimeDirector timeDirector;
        [SerializeField] private ScreenShaker screenShaker;
        [SerializeField] private AudioDirector audioDirector;
        [SerializeField] private FloatingTextSpawner floatingText;
        [SerializeField] private ParticleBurst particleBurst;
        [SerializeField] private GameBalanceAsset balanceAsset;

        private GameBalance _balance;
        private bool _dangerActive;
        private float _dangerBeatTimer;

        public bool DangerActive => _dangerActive;

        public void Configure(TimeDirector time, ScreenShaker shaker, AudioDirector audio,
            FloatingTextSpawner text, ParticleBurst particles, GameBalance balance)
        {
            timeDirector = time;
            screenShaker = shaker;
            audioDirector = audio;
            floatingText = text;
            particleBurst = particles;

            if (balance != null)
                _balance = balance;
        }

        public void SetBalance(GameBalance balance)
        {
            if (balance != null)
                _balance = balance;
        }

        public GameBalance Balance => _balance ?? (_balance = balanceAsset != null
            ? balanceAsset.ToBalance()
            : GameBalance.CreateDefault());

        /// <summary>投放确认：轻震屏 + 松手音（V2.20）。</summary>
        public void PlayDrop(Vector2 position)
        {
            screenShaker?.Shake(DropShakeAmplitude, Balance.DropShakeSeconds);
            audioDirector?.PlaySfx(SfxId.Drop);
        }

        /// <summary>合成表现：粒子 + 慢放 + 顿帧 + 震屏 + 合成音（V2.21–V2.23）。</summary>
        public void PlayMerge(MergeEvent evt, int combo)
        {
            var tier = FeelRules.ShakeTier(combo);

            particleBurst?.Play(evt.Position, FruitPalette.ForLevel(evt.ResultLevel), 14 + tier * 6,
                1f + tier * 0.35f);

            timeDirector?.RequestSlowMo(Balance.SlowMoScale, Balance.SlowMoSeconds);
            timeDirector?.RequestHitStop(FeelRules.HitStopSeconds(combo, Balance));

            screenShaker?.Shake(MergeShakeAmplitudes[Mathf.Clamp(tier, 0, MergeShakeAmplitudes.Length - 1)],
                0.14f + tier * 0.04f);

            var pitch = FeelRules.ComboPitch(combo, Balance.ComboSemitoneCap);
            audioDirector?.PlaySfx(combo > 1 ? SfxId.ComboUp : SfxId.Merge, pitch);
        }

        /// <summary>飘字：得分与连击倍率。</summary>
        public void PlayScore(Vector2 position, int delta, float multiplier, int combo)
        {
            var color = combo > 1 ? new Color(1f, 0.82f, 0.3f) : Color.white;
            var text = combo > 1 ? $"+{delta}  x{multiplier:0.0}" : $"+{delta}";
            floatingText?.Spawn(position + new Vector2(0f, 0.25f), text, color, combo > 1 ? 1.25f : 1f);
        }

        /// <summary>越线心跳音（V2.25）。脉冲视觉由 <see cref="DangerLineView"/> 负责。</summary>
        public void PlayDanger(bool active)
        {
            _dangerActive = active;
            _dangerBeatTimer = 0f;

            if (!active)
                return;

            audioDirector?.PlaySfx(SfxId.DangerBeat);
            _dangerBeatTimer = 0.6f;
        }

        /// <summary>失败重震 + 失败音（V2.25）。</summary>
        public void PlayFail()
        {
            screenShaker?.Shake(FailShakeAmplitude, 0.2f);
            audioDirector?.PlaySfx(SfxId.Fail);
            audioDirector?.Vibrate();
        }

        public void PlayItemUse(ItemKind kind)
        {
            audioDirector?.PlaySfx(kind == ItemKind.Shake ? SfxId.ComboUp : SfxId.Button,
                kind == ItemKind.Shake ? 1.25f : 1f);
        }

        public void PlayClaim()
        {
            audioDirector?.PlaySfx(SfxId.Claim);
            floatingText?.Spawn(new Vector2(0f, Balance.DangerLineY), "获得道具", new Color(1f, 0.85f, 0.35f), 1.1f);
        }

        public void PlayButton() => audioDirector?.PlaySfx(SfxId.Button);

        public void PlayRevive()
        {
            audioDirector?.PlaySfx(SfxId.Revive);
            floatingText?.Spawn(new Vector2(0f, 0f), "复活！", new Color(0.45f, 0.95f, 0.6f), 1.4f);
        }

        public void PlayReviveFailed()
        {
            audioDirector?.PlaySfx(SfxId.Fail);
        }

        private void Update()
        {
            if (!_dangerActive || audioDirector == null)
                return;

            _dangerBeatTimer -= Time.unscaledDeltaTime;
            if (_dangerBeatTimer > 0f)
                return;

            audioDirector.PlaySfx(SfxId.DangerBeat);
            _dangerBeatTimer = 0.6f;
        }
    }
}
