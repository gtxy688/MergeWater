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

        /// <summary>
        /// 顶级等级。2026-09-13 需求方「减少一级，来适配美术资源」：11 → 10，
        /// 原 11 级「大西瓜」整条移除，顶级变为 10 级「西瓜」（V1 表同步）。
        /// </summary>
        public const int MaxTier = 10;

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
        [SerializeField] private float fieldHalfWidth = 3.3f;
        [SerializeField] private float wallThickness = 0.6f;
        [SerializeField] private float fieldFloorY = -6.3f;
        /// <summary>
        /// 屏幕地面相对屏幕底边抬起的距离（V2.42）。方案 B「屏幕即边框」原先把地面精确放在
        /// 屏幕底边（`floorY = 相机 y − 正交半高`），水果落地后紧贴最后一行像素，
        /// 观感上像被下边缘切掉（需求方 2026-09-12 截图指认）。
        /// 抬起这段距离后，堆叠底部与屏幕底之间留出可见空隙；与 `KillY` 的落差也仍然足够。
        /// </summary>
        [SerializeField] private float floorScreenInset = 0.06f;
        // 生成高度必须落在「HUD 顶栏之下、警戒线之上」这一段可见区间里：
        // 相机正交半高 5.6、中心 y=0.4 → 视口世界 y 范围 [-5.2, 6.0]；
        // HUD 顶栏占屏幕顶部约 15%（对应世界 y≈4.3 以上），警戒线在 3.4。
        // 取 4.0 时待投水果位于屏幕顶部约 18% 处，既不被顶栏遮挡，也在警戒线之上。
        [SerializeField] private float dropSpawnY = 4.0f;
        [SerializeField] private float dangerLineY = 3.4f;
        [SerializeField] private float killY = -9.75f;
        [SerializeField] private int maxLiveFruits = 120;
        [SerializeField] private float maxLinearVelocity = 30f;
        [SerializeField] private float dropCooldownSeconds = 0.20f;
        [SerializeField] private float spawnJitter = 0.03f;
        [SerializeField] private float mergeResultUpwardImpulse = 0f;   // V2.31b：不再向上蹦（需求方要求）
        [SerializeField] private float mergeResultSideImpulse = 2.25f;   // V2.31b：向左右推开；2026-09-12 需求方「力度太吝啬」后 0.45→1.5
        [SerializeField] private float shakeImpulse = 2.4f;
        // 物理手感参数（2026-09-12 第三轮，按需求方「往左右移动、别往上蹦」的反馈）：
        // 弹性压低，避免落地/碰撞后向上弹；摩擦与阻尼再降一点，让水果更愿意横向滑动去找同级水果。
        // 注意别把阻尼降到接近 0，否则会回到「一路滚到墙角、摊平成一层」的老问题（V2.27 的教训）。
        [SerializeField] private float fruitFriction = 0.35f;
        [SerializeField] private float fruitBounciness = 0.25f;
        [SerializeField] private float fruitLinearDrag = 1.1f;
        [SerializeField] private float fruitAngularDrag = 0.05f;

        /// <summary>重力倍率随等级线性递增（V2.28）：大果下落更快、重果沉底压住堆叠。</summary>
        [SerializeField] private float minGravityScale = 1.6f;
        [SerializeField] private float maxGravityScale = 3.6f;

        // ── V2.33/V2.34 待投水果重现节奏、V2.35 加载页 ─────────────────
        // 投放后不要立刻冒出下一颗（玩家来不及看清刚投下的结果）：先等一小会，
        // 再让下一颗水果从屏幕中央「渐显」出现（缩放 + 淡入）。
        // 2026-09-13 需求方「小球的出现间隔」0.45 → 1.0 秒（要求 2 秒，确认后取 1 秒）；
        // 随后又要求「再调大点」→ 1.5 秒。
        [SerializeField] private float nextFruitRevealDelaySeconds = 1.5f;
        [SerializeField] private float nextFruitRevealDurationSeconds = 0.25f;
        [SerializeField] private float loadingMinSeconds = 2.0f;

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

        /// <summary>V2.42：运行时地面相对屏幕底边抬起的距离，避免水果贴屏底被切。</summary>
        public float FloorScreenInset => floorScreenInset;
        public float DropSpawnY => dropSpawnY;
        public float DangerLineY => dangerLineY;
        public float KillY => killY;
        public int MaxLiveFruits => maxLiveFruits;
        public float MaxLinearVelocity => maxLinearVelocity;
        public float DropCooldownSeconds => dropCooldownSeconds;
        public float SpawnJitter => spawnJitter;
        public float MergeResultUpwardImpulse => mergeResultUpwardImpulse;

        /// <summary>V2.31b：合成结果的水平初速（左右交替，单位 m/s），把结果推开而不是向上蹦。</summary>
        public float MergeResultSideImpulse => mergeResultSideImpulse;
        public float ShakeImpulse => shakeImpulse;
        public float FruitFriction => fruitFriction;
        public float FruitBounciness => fruitBounciness;
        public float FruitLinearDrag => fruitLinearDrag;

        public float FruitAngularDrag => fruitAngularDrag;

        /// <summary>V2.33：投放后下一颗待投水果的出现延迟（秒），让玩家先看清本次结果。</summary>
        public float NextFruitRevealDelaySeconds => nextFruitRevealDelaySeconds;

        /// <summary>V2.34：待投水果渐显（缩放 + 淡入）时长（秒）。</summary>
        public float NextFruitRevealDurationSeconds => nextFruitRevealDurationSeconds;

        /// <summary>V2.35：加载页最短展示时长（秒）。</summary>
        public float LoadingMinSeconds => loadingMinSeconds;

        /// <summary>该等级的重力倍率（V2.28，按 1..MaxTier 线性插值）。</summary>
        public float GetGravityScale(int level)
        {
            var span = Mathf.Max(1, TierCount - 1);
            var t = Mathf.Clamp01((level - MinTier) / (float)span);
            return Mathf.Lerp(minGravityScale, maxGravityScale, t);
        }

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
                    // 半径 2026-09-13 整体 x1.5（需求方「球形弄得太小了」）：质量/得分/权重不变，
                    // 即「同样重的果子变大」；阻尼/摩擦/弹性未动，手感需真机手动验收。
                    new FruitTierDefinition(1, "葡萄", 0.27f, 0.10f, 10, 0.5f),
                    new FruitTierDefinition(2, "樱桃", 0.36f, 0.18f, 15, 0.3f),
                    new FruitTierDefinition(3, "橘子", 0.45f, 0.28f, 21, 0.2f),
                    new FruitTierDefinition(4, "柠檬", 0.57f, 0.45f, 28, 0f),
                    new FruitTierDefinition(5, "猕猴桃", 0.69f, 0.66f, 36, 0f),
                    new FruitTierDefinition(6, "番茄", 0.825f, 0.95f, 45, 0f),
                    new FruitTierDefinition(7, "桃子", 0.975f, 1.32f, 55, 0f),
                    new FruitTierDefinition(8, "菠萝", 1.14f, 1.80f, 66, 0f),
                    new FruitTierDefinition(9, "椰子", 1.32f, 2.41f, 78, 0f),
                    new FruitTierDefinition(10, "西瓜", 1.50f, 3.14f, 91, 0f)
                },
                stageMilestones = new[]
                {
                    new StageMilestone(200, ItemKind.Undo),
                    new StageMilestone(500, ItemKind.Undo),
                    new StageMilestone(1000, ItemKind.Shake)
                }
            };
        }
    }
}
