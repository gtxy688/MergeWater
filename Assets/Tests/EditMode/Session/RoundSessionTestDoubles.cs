using System;
using System.Collections.Generic;
using MergeWater.Core;
using UnityEngine;

namespace MergeWater.Tests.EditMode
{
    /// <summary>M3 测试替身：不涉及物理的场地假实现。</summary>
    internal sealed class FakeField : IFieldPort
    {
        public int LiveFruitCount { get; set; }
        public float DangerLineY { get; set; } = 3.4f;
        public bool AcceptDrops { get; set; } = true;
        public int NextFruitId { get; private set; } = 1;
        public DangerViolation? Danger { get; set; }
        public int ClusterRemovedCount { get; set; } = 3;
        public int RemoveHighestClusterCalls { get; private set; }
        public int ClearAllCalls { get; private set; }
        public bool SimulationEnabled { get; private set; } = true;
        public int LastSimulationFreezeCount { get; private set; }

        public readonly List<KeyValuePair<int, float>> Drops = new List<KeyValuePair<int, float>>();

        public event Action<MergeEvent> Merged;

        public bool Drop(int level, float x, out int fruitId)
        {
            fruitId = 0;
            if (!AcceptDrops)
                return false;

            fruitId = NextFruitId++;
            Drops.Add(new KeyValuePair<int, float>(level, x));
            LiveFruitCount++;
            return true;
        }

        public bool TryGetDangerViolation(out DangerViolation violation)
        {
            if (Danger.HasValue)
            {
                violation = Danger.Value;
                return true;
            }

            violation = default;
            return false;
        }

        public bool RemoveFruit(int fruitId) => true;

        public int RemoveHighestCluster(int maxCount)
        {
            RemoveHighestClusterCalls++;
            var removed = Mathf.Min(maxCount, ClusterRemovedCount);
            LiveFruitCount = Mathf.Max(0, LiveFruitCount - removed);
            return removed;
        }

        public void ApplyShakeShuffle(float impulse, int seed)
        {
        }

        public void SetSimulationEnabled(bool enabled)
        {
            SimulationEnabled = enabled;
            if (!enabled)
                LastSimulationFreezeCount = LiveFruitCount;
        }

        public void ClearAll()
        {
            ClearAllCalls++;
            LiveFruitCount = 0;
            Drops.Clear();
            SimulationEnabled = true;
        }

        public void SimulateMerge(int resultLevel, Vector2 position = default)
        {
            Merged?.Invoke(new MergeEvent(0, 0, resultLevel - 1, resultLevel, position));
        }
    }

    /// <summary>M3 测试替身：可控结果的激励视频适配器。</summary>
    internal sealed class FakeAdsService : IAdsService
    {
        public bool IsAvailable { get; set; } = true;
        public bool IsInitialized { get; private set; }
        public bool IsShowing { get; set; }
        public RewardedResult RewardedResult { get; set; } = RewardedResult.Completed;
        public InterstitialResult InterstitialResult { get; set; } = InterstitialResult.Shown;
        public int RewardedCalls { get; private set; }
        public int InterstitialCalls { get; private set; }

        public void Initialize() => IsInitialized = true;

        public void ShowRewarded(AdPlacement placement, Action<RewardedResult> onComplete)
        {
            RewardedCalls++;
            onComplete?.Invoke(RewardedResult);
        }

        public void ShowInterstitial(Action<InterstitialResult> onComplete)
        {
            InterstitialCalls++;
            onComplete?.Invoke(InterstitialResult);
        }
    }
}
