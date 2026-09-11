using System;
using System.Collections;
using System.Collections.Generic;
using MergeWater.Core;
using UnityEngine;

namespace MergeWater.Field
{
    /// <summary>
    /// 对局场地：水果实体的生成/销毁、2D 物理堆叠、同级合成解析、越线物理查询与道具作用。
    /// 实现 <see cref="IFieldPort"/>；场地根节点应位于原点，所有坐标均为世界坐标。
    /// </summary>
    public sealed class GameField : MonoBehaviour, IFieldPort
    {
        [SerializeField] private GameBalanceAsset balanceAsset;
        [SerializeField] private Sprite fruitSprite;
        [SerializeField] private PhysicsMaterial2D fruitMaterial;
        [SerializeField] private bool autoBuildArena = true;

        private readonly Dictionary<int, FruitBody> _fruits = new Dictionary<int, FruitBody>();
        private readonly List<FruitBody> _ordered = new List<FruitBody>();
        private readonly HashSet<int> _merging = new HashSet<int>();
        private readonly Dictionary<int, FrozenState> _frozen = new Dictionary<int, FrozenState>();
        private readonly List<FruitBody> _scratch = new List<FruitBody>();

        private GameBalance _balance;
        private PhysicsMaterial2D _runtimeMaterial;
        private Transform _arenaRoot;
        private bool _arenaBuilt;
        private bool _simulationEnabled = true;
        private bool _limitWarned;
        private bool _escapeWarned;
        private DropRecord _lastDrop;
        private bool _hasLastDrop;
        private int _nextId;

        private struct FrozenState
        {
            public Vector2 Linear;
            public float Angular;
        }

        public event Action<MergeEvent> Merged;

        public GameBalance Balance => _balance ?? (_balance = balanceAsset != null
            ? balanceAsset.ToBalance()
            : GameBalance.CreateDefault());

        public float DangerLineY => Balance.DangerLineY;

        public float DropSpawnY => Balance.DropSpawnY;

        public float AbsorbDuration => Balance.AbsorbDurationSeconds;

        public int LiveFruitCount => _fruits.Count;

        public bool SimulationEnabled => _simulationEnabled;

        public PhysicsMaterial2D Material
        {
            get
            {
                if (fruitMaterial != null)
                    return fruitMaterial;

                if (_runtimeMaterial == null)
                {
                    _runtimeMaterial = new PhysicsMaterial2D("MW_FruitRuntime")
                    {
                        friction = Balance.FruitFriction,
                        bounciness = Balance.FruitBounciness
                    };
                }

                return _runtimeMaterial;
            }
        }

        /// <summary>测试与 Bootstrap 用：注入数值、占位图与物理材质。</summary>
        public void Configure(GameBalance balance, Sprite sprite, PhysicsMaterial2D material, bool buildArena = true)
        {
            if (balance != null)
                _balance = balance;

            if (sprite != null)
                fruitSprite = sprite;

            if (material != null)
                fruitMaterial = material;

            autoBuildArena = buildArena;
            if (buildArena)
            {
                _arenaBuilt = false;
                EnsureArena();
            }
        }

        // ── IFieldPort ───────────────────────────────────────────────

        public bool Drop(int level, float x, out int fruitId)
        {
            fruitId = 0;
            var balance = Balance;
            var tier = balance.GetTier(level);
            if (!tier.IsValid)
                return false;

            if (_fruits.Count >= balance.MaxLiveFruits)
            {
                if (!_limitWarned)
                {
                    _limitWarned = true;
                    Debug.LogWarning($"[GameField] 达到水果数量上限 {balance.MaxLiveFruits}，拒绝生成。");
                }

                return false;
            }

            EnsureArena();

            var limit = Mathf.Max(0f, balance.FieldHalfWidth - tier.Radius);
            var clampedX = Mathf.Clamp(x, -limit, limit);
            var id = _nextId++;
            var jitter = balance.SpawnJitter > 0f ? (id % 2 == 0 ? 1f : -1f) * balance.SpawnJitter * 0.5f : 0f;

            Spawn(id, level, new Vector2(clampedX, balance.DropSpawnY), new Vector2(jitter, 0f));

            _lastDrop = new DropRecord { FruitId = id, Level = level, X = clampedX, Merged = false };
            _hasLastDrop = true;
            fruitId = id;
            return true;
        }

        public bool TryGetDangerViolation(out DangerViolation violation)
        {
            violation = default;
            var balance = Balance;
            var line = balance.DangerLineY;
            var worstOverflow = float.NegativeInfinity;
            var found = false;

            for (var i = 0; i < _ordered.Count; i++)
            {
                var fruit = _ordered[i];
                if (fruit == null || fruit.IsPending || !fruit.IsSettled)
                    continue;

                var top = fruit.Collider.bounds.max.y;
                if (top <= line)
                    continue;

                var overflow = top - line;
                if (overflow <= worstOverflow)
                    continue;

                worstOverflow = overflow;
                violation = new DangerViolation(fruit.FruitId, fruit.Level, top, overflow);
                found = true;
            }

            return found;
        }

        public bool TryPeekLastDrop(out DropRecord record)
        {
            record = _lastDrop;
            return _hasLastDrop;
        }

        /// <summary>
        /// 在指定位置直接生成水果（不写投放记录）。用于测试装配与特殊玩法。
        /// 越线判定会通过静止计时过滤刚生成的水果，因此本方法不会造成误判。
        /// </summary>
        public bool SpawnAt(int level, Vector2 position, out int fruitId)
        {
            fruitId = 0;
            var balance = Balance;
            if (!balance.HasTier(level))
                return false;

            if (_fruits.Count >= balance.MaxLiveFruits)
                return false;

            EnsureArena();
            fruitId = _nextId++;
            Spawn(fruitId, level, position, Vector2.zero);
            return true;
        }

        public bool RemoveFruit(int fruitId)
        {
            if (!TryGetLive(fruitId, out var fruit))
            {
                _fruits.Remove(fruitId);
                return false;
            }

            RemoveInternal(fruit);
            return true;
        }

        public int RemoveFruitInRadius(Vector2 point, float radius)
        {
            if (radius <= 0f)
                return 0;

            _scratch.Clear();
            for (var i = 0; i < _ordered.Count; i++)
            {
                var fruit = _ordered[i];
                if (fruit == null)
                    continue;

                var distance = ((Vector2)fruit.transform.position - point).magnitude;
                if (distance <= radius)
                    _scratch.Add(fruit);
            }

            for (var i = 0; i < _scratch.Count; i++)
                RemoveInternal(_scratch[i]);

            var removed = _scratch.Count;
            _scratch.Clear();
            return removed;
        }

        public bool HasFruitInRadius(Vector2 point, float radius)
        {
            if (radius <= 0f)
                return false;

            for (var i = 0; i < _ordered.Count; i++)
            {
                var fruit = _ordered[i];
                if (fruit == null)
                    continue;

                if (((Vector2)fruit.transform.position - point).sqrMagnitude <= radius * radius)
                    return true;
            }

            return false;
        }

        public bool RemoveSingleNearest(Vector2 point, float maxRadius, out int removedId)
        {
            removedId = 0;
            FruitBody best = null;
            var bestDistance = float.MaxValue;

            for (var i = 0; i < _ordered.Count; i++)
            {
                var fruit = _ordered[i];
                if (fruit == null)
                    continue;

                var distance = ((Vector2)fruit.transform.position - point).magnitude;
                if (distance > maxRadius || distance >= bestDistance)
                    continue;

                best = fruit;
                bestDistance = distance;
            }

            if (best == null)
                return false;

            removedId = best.FruitId;
            RemoveInternal(best);
            return true;
        }

        public int RemoveHighestCluster(int maxCount)
        {
            if (maxCount <= 0 || _fruits.Count == 0)
                return 0;

            var seed = FindHighest();
            if (seed == null)
                return 0;

            var cluster = CollectContactCluster(seed);
            cluster.Sort(CompareByLevelThenHeight);

            // 簇内不足 maxCount 时，按等级由高到低补齐，保证复活确有缓解但不超过上限。
            if (cluster.Count < maxCount)
            {
                var inCluster = new HashSet<int>();
                for (var i = 0; i < cluster.Count; i++)
                    inCluster.Add(cluster[i].FruitId);

                for (var i = 0; i < _ordered.Count; i++)
                {
                    var fruit = _ordered[i];
                    if (fruit == null || inCluster.Contains(fruit.FruitId))
                        continue;

                    cluster.Add(fruit);
                }

                cluster.Sort(CompareByLevelThenHeight);
            }

            var count = Mathf.Min(maxCount, cluster.Count);
            for (var i = 0; i < count; i++)
                RemoveInternal(cluster[i]);

            return count;
        }

        public void ApplyShakeShuffle(float impulse, int seed)
        {
            if (impulse <= 0f || !_simulationEnabled)
                return;

            var rng = new System.Random(seed);
            for (var i = 0; i < _ordered.Count; i++)
            {
                var fruit = _ordered[i];
                if (fruit == null || fruit.Body.bodyType != RigidbodyType2D.Dynamic)
                    continue;

                var dx = (float)(rng.NextDouble() * 2.0 - 1.0);
                var dy = (float)(rng.NextDouble() * 0.5 + 0.35);
                fruit.Body.AddForce(new Vector2(dx, dy) * impulse, ForceMode2D.Impulse);
            }
        }

        public void SetSimulationEnabled(bool enabled)
        {
            if (_simulationEnabled == enabled)
                return;

            _simulationEnabled = enabled;

            if (!enabled)
            {
                _frozen.Clear();
                for (var i = 0; i < _ordered.Count; i++)
                {
                    var fruit = _ordered[i];
                    if (fruit == null)
                        continue;

                    fruit.Freeze(out var linear, out var angular);
                    _frozen[fruit.FruitId] = new FrozenState { Linear = linear, Angular = angular };
                }

                return;
            }

            for (var i = 0; i < _ordered.Count; i++)
            {
                var fruit = _ordered[i];
                if (fruit == null)
                    continue;

                if (_frozen.TryGetValue(fruit.FruitId, out var state))
                    fruit.Thaw(state.Linear, state.Angular);
                else
                    fruit.Thaw(Vector2.zero, 0f);
            }

            _frozen.Clear();
        }

        public void ClearAll()
        {
            StopAllCoroutines();
            _merging.Clear();
            _frozen.Clear();

            for (var i = 0; i < _ordered.Count; i++)
            {
                var fruit = _ordered[i];
                if (fruit == null)
                    continue;

                DestroyObject(fruit.gameObject);
            }

            _ordered.Clear();
            _fruits.Clear();
            _scratch.Clear();
            _hasLastDrop = false;
            _lastDrop = default;
            _nextId = 0;
            _limitWarned = false;
            _escapeWarned = false;
            _simulationEnabled = true;
        }

        // ── 合成解析（由 FruitBody 回调） ─────────────────────────────

        public void OnFruitCollision(FruitBody self, Collision2D collision)
        {
            if (!_simulationEnabled || self == null || self.IsPending)
                return;

            var other = collision.collider != null ? collision.collider.GetComponent<FruitBody>() : null;
            if (other == null || other == self || other.IsPending)
                return;

            if (self.Level != other.Level || !ScoreRules.CanMerge(self.Level, Balance))
                return;

            if (!IsLive(self) || !IsLive(other))
                return;

            if (_merging.Contains(self.FruitId) || _merging.Contains(other.FruitId))
                return;

            _merging.Add(self.FruitId);
            _merging.Add(other.FruitId);
            StartCoroutine(RunMerge(self.FruitId, other.FruitId, self.Level));
        }

        private IEnumerator RunMerge(int idA, int idB, int level)
        {
            if (!TryGetLive(idA, out var a) || !TryGetLive(idB, out var b))
            {
                _merging.Remove(idA);
                _merging.Remove(idB);
                yield break;
            }

            a.SetPending(true);
            b.SetPending(true);

            var startA = (Vector2)a.transform.position;
            var startB = (Vector2)b.transform.position;
            var mid = (startA + startB) * 0.5f;
            var duration = Mathf.Max(0f, Balance.AbsorbDurationSeconds);

            if (duration > 0f)
            {
                var elapsed = 0f;
                while (elapsed < duration)
                {
                    if (!TryGetLive(idA, out a) || !TryGetLive(idB, out b))
                        break;

                    elapsed += Time.deltaTime;
                    var k = Mathf.Clamp01(elapsed / duration);
                    a.MoveTo(Vector2.Lerp(startA, mid, k));
                    b.MoveTo(Vector2.Lerp(startB, mid, k));
                    yield return null;
                }
            }

            yield return null;

            _merging.Remove(idA);
            _merging.Remove(idB);

            if (!TryGetLive(idA, out a) || !TryGetLive(idB, out b))
                yield break;

            MarkLastDropMerged(idA);
            MarkLastDropMerged(idB);

            RemoveInternal(a);
            RemoveInternal(b);

            var resultLevel = level + 1;
            var resultId = _nextId++;
            Spawn(resultId, resultLevel, mid, Vector2.up * Balance.MergeResultUpwardImpulse);

            Merged?.Invoke(new MergeEvent(idA, idB, level, resultLevel, mid));
        }

        // ── 内部工具 ─────────────────────────────────────────────────

        private void Spawn(int fruitId, int level, Vector2 position, Vector2 velocity)
        {
            var balance = Balance;
            var tier = balance.GetTier(level);
            var go = new GameObject($"Fruit_{level}_{fruitId}");
            go.transform.SetParent(transform, false);
            go.transform.position = position;

            var body = go.AddComponent<FruitBody>();
            body.Initialize(this, fruitId, tier, fruitSprite, FruitPalette.ForLevel(level), Material,
                new FruitPhysicsConfig(balance));

            if (velocity != Vector2.zero)
                body.Body.velocity = velocity;

            _fruits[fruitId] = body;
            _ordered.Add(body);
        }

        private void RemoveInternal(FruitBody fruit)
        {
            if (fruit == null)
                return;

            _fruits.Remove(fruit.FruitId);
            _ordered.Remove(fruit);
            _frozen.Remove(fruit.FruitId);
            _merging.Remove(fruit.FruitId);
            DestroyObject(fruit.gameObject);
        }

        private void MarkLastDropMerged(int fruitId)
        {
            if (_hasLastDrop && _lastDrop.FruitId == fruitId)
                _lastDrop.Merged = true;
        }

        private bool IsLive(FruitBody fruit) => fruit != null && _fruits.ContainsKey(fruit.FruitId);

        private bool TryGetLive(int fruitId, out FruitBody fruit)
        {
            if (_fruits.TryGetValue(fruitId, out fruit) && fruit != null)
                return true;

            fruit = null;
            return false;
        }

        private FruitBody FindHighest()
        {
            FruitBody highest = null;
            var bestY = float.NegativeInfinity;

            for (var i = 0; i < _ordered.Count; i++)
            {
                var fruit = _ordered[i];
                if (fruit == null)
                    continue;

                var top = fruit.Collider.bounds.max.y;
                if (top <= bestY)
                    continue;

                bestY = top;
                highest = fruit;
            }

            return highest;
        }

        private List<FruitBody> CollectContactCluster(FruitBody seed)
        {
            var cluster = new List<FruitBody>();
            var visited = new HashSet<int>();
            var queue = new Queue<FruitBody>();

            visited.Add(seed.FruitId);
            queue.Enqueue(seed);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                cluster.Add(current);

                for (var i = 0; i < _ordered.Count; i++)
                {
                    var candidate = _ordered[i];
                    if (candidate == null || visited.Contains(candidate.FruitId))
                        continue;

                    var delta = (Vector2)candidate.transform.position - (Vector2)current.transform.position;
                    var contactDistance = current.Radius + candidate.Radius + 0.05f;
                    if (delta.sqrMagnitude > contactDistance * contactDistance)
                        continue;

                    visited.Add(candidate.FruitId);
                    queue.Enqueue(candidate);
                }
            }

            return cluster;
        }

        private static int CompareByLevelThenHeight(FruitBody x, FruitBody y)
        {
            if (x == null)
                return y == null ? 0 : 1;

            if (y == null)
                return -1;

            var byLevel = y.Level.CompareTo(x.Level);
            if (byLevel != 0)
                return byLevel;

            return y.Collider.bounds.max.y.CompareTo(x.Collider.bounds.max.y);
        }

        private void EnsureArena()
        {
            if (_arenaBuilt || !autoBuildArena)
                return;

            var balance = Balance;
            var halfWidth = balance.FieldHalfWidth;
            var thickness = balance.WallThickness;
            var bottom = balance.FieldFloorY;
            var top = balance.DropSpawnY + 1f;
            var height = top - bottom;

            if (_arenaRoot == null)
            {
                var existing = transform.Find("Arena");
                if (existing != null)
                {
                    _arenaRoot = existing;
                }
                else
                {
                    var go = new GameObject("Arena");
                    go.transform.SetParent(transform, false);
                    _arenaRoot = go.transform;
                }
            }

            CreateWall("LeftWall", new Vector2(-(halfWidth + thickness * 0.5f), bottom + height * 0.5f),
                new Vector2(thickness, height));
            CreateWall("RightWall", new Vector2(halfWidth + thickness * 0.5f, bottom + height * 0.5f),
                new Vector2(thickness, height));
            CreateWall("Floor", new Vector2(0f, bottom - thickness * 0.5f),
                new Vector2(halfWidth * 2f + thickness * 2f, thickness));

            _arenaBuilt = true;
        }

        private void CreateWall(string wallName, Vector2 center, Vector2 size)
        {
            var existing = _arenaRoot.Find(wallName);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject(wallName);
                go.transform.SetParent(_arenaRoot, false);
            }

            go.transform.localPosition = center;
            var collider = go.GetComponent<BoxCollider2D>();
            if (collider == null)
                collider = go.AddComponent<BoxCollider2D>();

            collider.size = size;
            collider.offset = Vector2.zero;
            collider.sharedMaterial = Material;
        }

        private static void DestroyObject(GameObject go)
        {
            if (go == null)
                return;

            if (Application.isPlaying)
                Destroy(go);
            else
                DestroyImmediate(go);
        }

        private static void DestroyAsset(UnityEngine.Object asset)
        {
            if (asset == null)
                return;

            if (Application.isPlaying)
                Destroy(asset);
            else
                DestroyImmediate(asset);
        }

        private void FixedUpdate()
        {
            SanitizeEscapedFruits();
        }

        /// <summary>异常保护（V3）：水果若离开场地范围则回收，避免残留空引用。</summary>
        private void SanitizeEscapedFruits()
        {
            var balance = Balance;
            var xLimit = balance.FieldHalfWidth + balance.WallThickness + 2f;

            for (var i = _ordered.Count - 1; i >= 0; i--)
            {
                var fruit = _ordered[i];
                if (fruit == null)
                {
                    _ordered.RemoveAt(i);
                    continue;
                }

                var position = fruit.transform.position;
                if (position.y >= balance.KillY && Mathf.Abs(position.x) <= xLimit)
                    continue;

                if (!_escapeWarned)
                {
                    _escapeWarned = true;
                    Debug.LogWarning($"[GameField] 检测到水果离开场地（{position}），已回收并抑制后续同类告警。");
                }

                RemoveInternal(fruit);
            }
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial != null)
            {
                DestroyAsset(_runtimeMaterial);
                _runtimeMaterial = null;
            }
        }
    }
}
