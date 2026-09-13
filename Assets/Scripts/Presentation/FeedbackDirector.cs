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

        /// <summary>震屏目标是否就绪（自检用；未就绪时震屏会静默失效）。</summary>
        public bool ScreenShakeTargetAvailable => screenShaker != null && screenShaker.HasTarget;

        /// <summary>震屏目标（通常是主相机）。运行时由 M7 指向真实相机。</summary>
        public void SetShakeTarget(Transform shakeTarget)
        {
            if (screenShaker != null)
                screenShaker.SetTarget(shakeTarget);
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

        /// <summary>连击等级越高颜色越「烫」，与 V2.24 的音阶上行配套。</summary>
        private static readonly Color[] ComboColors =
        {
            new Color(1f, 0.85f, 0.30f),
            new Color(1f, 0.66f, 0.20f),
            new Color(1f, 0.45f, 0.15f),
            new Color(1f, 0.28f, 0.16f),
            new Color(1f, 0.15f, 0.35f)
        };

        /// <summary>
        /// 飘字：得分与连击提示都落在**合成位置**上（需求方：连击提示要跟到水果处、反馈更强）。
        /// 连击 ≥2 时把「N 连击」作为主视觉（字号随连击放大、颜色随连击升温），分数线作为副视觉。
        /// </summary>
        public void PlayScore(Vector2 position, int delta, float multiplier, int combo)
        {
            var spawnAt = position + new Vector2(0f, 0.25f);

            if (combo < 2)
            {
                floatingText?.Spawn(spawnAt, $"+{delta}", Color.white, 1f);
                return;
            }

            var tier = Mathf.Clamp(combo - 2, 0, ComboColors.Length - 1);
            var color = ComboColors[tier];
            var size = 1.25f + tier * 0.22f;

            floatingText?.Spawn(spawnAt, $"{combo} 连击", color, size);
            floatingText?.Spawn(spawnAt + new Vector2(0f, -0.24f * size), $"+{delta}  ×{multiplier:0.0}",
                new Color(1f, 1f, 1f, 0.95f), size * 0.68f);
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
