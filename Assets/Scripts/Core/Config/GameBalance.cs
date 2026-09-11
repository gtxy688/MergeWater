using System;
using UnityEngine;

namespace MergeWater.Core
{
    /// <summary>
    /// 全部局内数值的单一来源。数值权威定义见 Docs/requirements.md 的 V1/V2 表；
    /// 本类的 <see cref="CreateDefault"/> 是与该表逐项一致的代码默认值。
    /// 运行时只读使用：不要在共享资产上写回可变状态。
    /// </summary>
    [Serializable]
    public sealed class GameBalance
    {
        public const int MinTier = 1;
        public const int MaxTier = 11;

        // ── V1 水果等级表 ─────────────────────────────────────────────
        [SerializeField] private FruitTierDefinition[] tiers = Array.Empty<FruitTierDefinition>();

        // ── V2.4–V2.6 投放节奏 ────────────────────────────────────────
        [SerializeField] private int earlyPhaseDropCount = 20;
        [SerializeField] private int midPhaseDropCount = 60;

        // ── V2.1–V2.3 连击 ───────────────────────────────────────────
        [SerializeField] private float comboWindowSeconds = 3.0f;
        [SerializeField] private float comboMultiplierStep = 0.1f;
        [SerializeField] private float comboMultiplierCap = 2.0f;

        // ── V2.8–V2.9 越线 ───────────────────────────────────────────
        [SerializeField] private float dangerHoldSeconds = 1.2f;
        [SerializeField] private float dangerSettleLinearSpeed = 0.30f;
        [SerializeField] private float dangerSettleAngularSpeed = 25f;
        [SerializeField] private float dangerSettleGraceSeconds = 0.35f;

        // ── V2.10–V2.12 复活与广告 ───────────────────────────────────
        [SerializeField] private int revivePerRound = 1;
        [SerializeField] private int reviveClusterMax = 3;
        [SerializeField] private float reviveCooldownSeconds = 90f;
        [SerializeField] private float shareCooldownSeconds = 60f;
        [SerializeField] private int interstitialEveryNGames = 3;

        // ── V2.13–V2.15 道具每日上限 ─────────────────────────────────
        [SerializeField] private int itemDailyCap = 3;
        [SerializeField] private int shakeDailyCap = 2;
        [SerializeField] private int giftDailyCap = 1;

        // ── V2.17 道具作用范围 ───────────────────────────────────────
        [SerializeField] private float bombRadius = 0.6f;
        [SerializeField] private float hammerMaxRadius = 1.2f;

        // ── V2.18 阶段目标 ───────────────────────────────────────────
        [SerializeField] private StageMilestone[] stageMilestones = Array.Empty<StageMilestone>();

        // ── V2.20–V2.26 手感 ─────────────────────────────────────────
        [SerializeField] private float dropShakeSeconds = 0.05f;
        [SerializeField] private float slowMoScale = 0.2f;
        [SerializeField] private float slowMoSeconds = 0.15f;
        [SerializeField] private float hitStopMinSeconds = 0.08f;
        [SerializeField] private float hitStopMaxSeconds = 0.12f;
        [SerializeField] private int hitStopMaxCombo = 6;
        [SerializeField] private int comboSemitoneCap = 12; // V2.24「封顶 8 度」= 12 半音（音高 ×2）
        [SerializeField] private float aimPreviewMaxSeconds = 0.8f;
        [SerializeField] private float absorbDurationSeconds = 0.06f;

        // ── 场地与物理稳定性（V3） ───────────────────────────────────
        [SerializeField] private float fieldHalfWidth = 2.2f;
        [SerializeField] private float wallThickness = 0.4f;
        [SerializeField] private float fieldFloorY = -4.2f;
        [SerializeField] private float dropSpawnY = 5.2f;
        [SerializeField] private float dangerLineY = 3.4f;
        [SerializeField] private float killY = -6.5f;
        [SerializeField] private int maxLiveFruits = 120;
        [SerializeField] private float maxLinearVelocity = 20f;
        [SerializeField] private float dropCooldownSeconds = 0.20f;
        [SerializeField] private float spawnJitter = 0.02f;
        [SerializeField] private float mergeResultUpwardImpulse = 0.6f;
        [SerializeField] private float shakeImpulse = 1.6f;
        [SerializeField] private float fruitFriction = 0.45f;
        [SerializeField] private float fruitBounciness = 0.02f;
        [SerializeField] private float fruitLinearDrag = 0.05f;

        public FruitTierDefinition[] Tiers => tiers;
        public int TierCount => tiers?.Length ?? 0;

        public int EarlyPhaseDropCount => earlyPhaseDropCount;
        public int MidPhaseDropCount => midPhaseDropCount;

        public float ComboWindowSeconds => comboWindowSeconds;
        public float ComboMultiplierStep => comboMultiplierStep;
        public float ComboMultiplierCap => comboMultiplierCap;

        public float DangerHoldSeconds => dangerHoldSeconds;
        public float DangerSettleLinearSpeed => dangerSettleLinearSpeed;
        public float DangerSettleAngularSpeed => dangerSettleAngularSpeed;

        /// <summary>连续静止多久才可用于越线判定（V2.9 的静止判定缓冲）。</summary>
        public float DangerSettleGraceSeconds => dangerSettleGraceSeconds;

        public int RevivePerRound => revivePerRound;
        public int ReviveClusterMax => reviveClusterMax;
        public float ReviveCooldownSeconds => reviveCooldownSeconds;
        public float ShareCooldownSeconds => shareCooldownSeconds;
        public int InterstitialEveryNGames => interstitialEveryNGames;

        public int ItemDailyCap => itemDailyCap;
        public int ShakeDailyCap => shakeDailyCap;
        public int GiftDailyCap => giftDailyCap;

        public float BombRadius => bombRadius;
        public float HammerMaxRadius => hammerMaxRadius;

        public StageMilestone[] StageMilestones => stageMilestones;

        public float DropShakeSeconds => dropShakeSeconds;
        public float SlowMoScale => slowMoScale;
        public float SlowMoSeconds => slowMoSeconds;
        public float HitStopMinSeconds => hitStopMinSeconds;
        public float HitStopMaxSeconds => hitStopMaxSeconds;
        public int HitStopMaxCombo => hitStopMaxCombo;
        /// <summary>连击音阶封顶的半音数（V2.24 的「8 度」= 12 半音）。</summary>
        public int ComboSemitoneCap => comboSemitoneCap;
        public float AimPreviewMaxSeconds => aimPreviewMaxSeconds;
        public float AbsorbDurationSeconds => absorbDurationSeconds;

        public float FieldHalfWidth => fieldHalfWidth;
        public float WallThickness => wallThickness;
        public float FieldFloorY => fieldFloorY;
        public float DropSpawnY => dropSpawnY;
        public float DangerLineY => dangerLineY;
        public float KillY => killY;
        public int MaxLiveFruits => maxLiveFruits;
        public float MaxLinearVelocity => maxLinearVelocity;
        public float DropCooldownSeconds => dropCooldownSeconds;
        public float SpawnJitter => spawnJitter;
        public float MergeResultUpwardImpulse => mergeResultUpwardImpulse;
        public float ShakeImpulse => shakeImpulse;
        public float FruitFriction => fruitFriction;
        public float FruitBounciness => fruitBounciness;
        public float FruitLinearDrag => fruitLinearDrag;

        /// <summary>按等级取定义；越界返回结构体默认值（IsValid 为 false），不抛异常。</summary>
        public FruitTierDefinition GetTier(int level)
        {
            if (tiers == null || level < MinTier || level > tiers.Length)
                return default;

            return tiers[level - 1];
        }

        public bool HasTier(int level) => tiers != null && level >= MinTier && level <= tiers.Length;

        /// <summary>某道具的每日领取上限。</summary>
        public int GetDailyCap(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Undo:
                case ItemKind.Bomb:
                case ItemKind.Hammer:
                    return itemDailyCap;
                case ItemKind.Shake:
                    return shakeDailyCap;
                default:
                    return 0;
            }
        }

        public GameBalance Clone()
        {
            var copy = (GameBalance)MemberwiseClone();
            copy.tiers = tiers == null ? Array.Empty<FruitTierDefinition>() : (FruitTierDefinition[])tiers.Clone();
            copy.stageMilestones = stageMilestones == null
                ? Array.Empty<StageMilestone>()
                : (StageMilestone[])stageMilestones.Clone();
            return copy;
        }

        /// <summary>与 Docs/requirements.md 的 V1/V2 表逐项一致的代码默认值。</summary>
        public static GameBalance CreateDefault()
        {
            return new GameBalance
            {
                tiers = new[]
                {
                    new FruitTierDefinition(1, "葡萄", 0.18f, 0.10f, 10, 0.5f),
                    new FruitTierDefinition(2, "樱桃", 0.24f, 0.18f, 15, 0.3f),
                    new FruitTierDefinition(3, "橘子", 0.30f, 0.28f, 21, 0.2f),
                    new FruitTierDefinition(4, "柠檬", 0.38f, 0.45f, 28, 0f),
                    new FruitTierDefinition(5, "猕猴桃", 0.46f, 0.66f, 36, 0f),
                    new FruitTierDefinition(6, "番茄", 0.55f, 0.95f, 45, 0f),
                    new FruitTierDefinition(7, "桃子", 0.65f, 1.32f, 55, 0f),
                    new FruitTierDefinition(8, "菠萝", 0.76f, 1.80f, 66, 0f),
                    new FruitTierDefinition(9, "椰子", 0.88f, 2.41f, 78, 0f),
                    new FruitTierDefinition(10, "西瓜", 1.00f, 3.14f, 91, 0f),
                    new FruitTierDefinition(11, "大西瓜", 1.12f, 3.94f, 105, 0f)
                },
                stageMilestones = new[]
                {
                    new StageMilestone(200, ItemKind.Undo),
                    new StageMilestone(500, ItemKind.Bomb),
                    new StageMilestone(1000, ItemKind.Hammer)
                }
            };
        }
    }
}
