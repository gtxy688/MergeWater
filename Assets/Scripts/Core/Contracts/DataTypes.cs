using System;
using UnityEngine;

namespace MergeWater.Core
{
    /// <summary>
    /// 单级水果的数值定义。数值权威来源为 Docs/requirements.md 的 V1 表，
    /// 代码默认值集中在 <see cref="GameBalance.CreateDefault"/>。
    /// </summary>
    [Serializable]
    public struct FruitTierDefinition
    {
        public int Level;
        public string DisplayName;
        public float Radius;
        public float Mass;
        public int Score;
        public float DropWeight;

        public FruitTierDefinition(int level, string displayName, float radius, float mass, int score, float dropWeight)
        {
            Level = level;
            DisplayName = displayName;
            Radius = radius;
            Mass = mass;
            Score = score;
            DropWeight = dropWeight;
        }

        public bool IsValid => Level > 0 && Radius > 0f;
    }

    /// <summary>阶段目标节点（V2.18）。</summary>
    [Serializable]
    public struct StageMilestone
    {
        public int Score;
        public ItemKind Reward;

        public StageMilestone(int score, ItemKind reward)
        {
            Score = score;
            Reward = reward;
        }
    }

    /// <summary>一次成功合成的事实（M2 发布，M3 计分，M5 表现）。</summary>
    public readonly struct MergeEvent
    {
        public readonly int FruitIdA;
        public readonly int FruitIdB;
        public readonly int SourceLevel;
        public readonly int ResultLevel;
        public readonly Vector2 Position;

        public MergeEvent(int fruitIdA, int fruitIdB, int sourceLevel, int resultLevel, Vector2 position)
        {
            FruitIdA = fruitIdA;
            FruitIdB = fruitIdB;
            SourceLevel = sourceLevel;
            ResultLevel = resultLevel;
            Position = position;
        }
    }

    /// <summary>越线物理查询结果（V2.9）。</summary>
    public readonly struct DangerViolation
    {
        public readonly int FruitId;
        public readonly int Level;
        public readonly float TopY;
        public readonly float Overflow;

        public DangerViolation(int fruitId, int level, float topY, float overflow)
        {
            FruitId = fruitId;
            Level = level;
            TopY = topY;
            Overflow = overflow;
        }
    }

    /// <summary>一次投放的事实。</summary>
    public readonly struct DropPerformedEvent
    {
        public readonly int FruitId;
        public readonly int Level;
        public readonly float X;
        public readonly int DropIndex;

        public DropPerformedEvent(int fruitId, int level, float x, int dropIndex)
        {
            FruitId = fruitId;
            Level = level;
            X = x;
            DropIndex = dropIndex;
        }
    }

    /// <summary>一次计分的事实。</summary>
    public readonly struct ScoreEvent
    {
        public readonly int Delta;
        public readonly int Total;
        public readonly float Multiplier;
        public readonly int Combo;
        public readonly int ResultLevel;
        public readonly Vector2 Position;

        public ScoreEvent(int delta, int total, float multiplier, int combo, int resultLevel, Vector2 position)
        {
            Delta = delta;
            Total = total;
            Multiplier = multiplier;
            Combo = combo;
            ResultLevel = resultLevel;
            Position = position;
        }
    }

    /// <summary>瞄准状态（M4 输出，M5 绘制，M3 消费投放指令）。</summary>
    public readonly struct AimState
    {
        public readonly bool IsAiming;
        public readonly ItemKind AimItem;
        public readonly float X;
        public readonly float NormalizedX;
        public readonly bool IsItemAim;

        public AimState(bool isAiming, ItemKind aimItem, float x, float normalizedX, bool isItemAim)
        {
            IsAiming = isAiming;
            AimItem = aimItem;
            X = x;
            NormalizedX = normalizedX;
            IsItemAim = isItemAim;
        }

        public static AimState Idle => new AimState(false, ItemKind.None, 0f, 0.5f, false);
    }

    /// <summary>局内只读快照，供表现层每帧读取。</summary>
    public readonly struct RoundSnapshot
    {
        public readonly int Score;
        public readonly int BestScore;
        public readonly int Combo;
        public readonly float Multiplier;
        public readonly RoundPhase Phase;
        public readonly int DropCount;
        public readonly int CurrentLevel;
        public readonly int NextLevel;
        public readonly bool DangerActive;
        public readonly bool CanRevive;
        public readonly int StageMilestoneIndex;

        public RoundSnapshot(int score, int bestScore, int combo, float multiplier, RoundPhase phase,
            int dropCount, int currentLevel, int nextLevel, bool dangerActive, bool canRevive,
            int stageMilestoneIndex)
        {
            Score = score;
            BestScore = bestScore;
            Combo = combo;
            Multiplier = multiplier;
            Phase = phase;
            DropCount = dropCount;
            CurrentLevel = currentLevel;
            NextLevel = nextLevel;
            DangerActive = dangerActive;
            CanRevive = canRevive;
            StageMilestoneIndex = stageMilestoneIndex;
        }
    }


    /// <summary>埋点参数键值对。</summary>
    public readonly struct AnalyticsParam
    {
        public readonly string Key;
        public readonly string Value;

        public AnalyticsParam(string key, string value)
        {
            Key = key;
            Value = value;
        }

        public static AnalyticsParam Int(string key, int value) => new AnalyticsParam(key, value.ToString());

        public static AnalyticsParam Num(string key, float value) =>
            new AnalyticsParam(key, value.ToString("0.###"));

        public static AnalyticsParam Text(string key, string value) => new AnalyticsParam(key, value ?? string.Empty);
    }

    /// <summary>R24 定义的事件名，避免字符串漂移。</summary>
    public static class AnalyticsEventNames
    {
        public const string AppLaunch = "app_launch";
        public const string FirstDrop = "first_drop";
        public const string Merge = "merge";
        public const string DangerStart = "danger_start";
        public const string DangerEnd = "danger_end";
        public const string ReviveClick = "revive_click";
        public const string ReviveComplete = "revive_complete";
        public const string ShareStart = "share_start";

        public const string ItemGrant = "item_grant";
        public const string ItemUse = "item_use";
        public const string RoundEnd = "round_end";
        public const string EntryClick = "entry_click";
        public const string AdComplete = "ad_complete";
        public const string MilestoneReached = "milestone_reached";
        public const string SettingChanged = "setting_changed";
    }
}
