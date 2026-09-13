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
    public sealed class GameField : MonoBehaviour, IFieldPort, IPreviewObstacle
    {
        [SerializeField] private GameBalanceAsset balanceAsset;
        [SerializeField] private Sprite fruitSprite;

        /// <summary>
        /// 「等级 → 贴图 / 染色」的唯一来源，由组合根在运行时注入（M7）。
        /// 为空时退化成「只用 <see cref="fruitSprite"/> + 调色板染色」，即占位美术时代的旧行为。
        /// </summary>
        private FruitArt _art;
        [SerializeField] private PhysicsMaterial2D fruitMaterial;
        [SerializeField] private bool autoBuildArena = true;
        [SerializeField] private Sprite wallSprite;

        // 容器外观：早期墙体/地面只有碰撞体没有渲染器，水果看起来像掉进虚空。
        // 现在默认「屏幕即边框」——边界外移到屏幕左右边缘/底部后，再把棕色框画出来反而多余，
        // 因此外观默认隐藏（需要旧观感时把 showArenaVisuals 打开，仅用于对比/调试）。
        [SerializeField] private bool showArenaVisuals;
        [SerializeField] private Color wallColor = new Color(0.62f, 0.52f, 0.42f, 1f);
        [SerializeField] private Color floorColor = new Color(0.52f, 0.42f, 0.33f, 1f);

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
        private Sprite _wallSpriteFallback;
        private int _nextId;

        // 方案 B：场地边界跟随屏幕（左右墙贴屏幕左右边缘、地面 = 屏幕底 + FloorScreenInset），
        // 由 M7 按相机可视范围注入；
        // 没有注入时回退到 GameBalance 的固定设计值（EditMode/脚手架测试即该路径）。
        private float _playHalfWidth = -1f;
        private float _playFloorY = float.NaN;

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

        /// <summary>容器外观用的 sprite；未指派时回退到占位方块。</summary>
        public Sprite WallSprite
        {
            get
            {
                if (wallSprite != null)
                    return wallSprite;

                if (_wallSpriteFallback == null)
                    _wallSpriteFallback = Resources.Load<Sprite>("Placeholder/ui_square");

                return _wallSpriteFallback;
            }
        }

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

        /// <summary>当前对局的有效半宽（方案 B：由屏幕可视宽度决定；未注入时为数值表设计值）。</summary>
        public float PlayHalfWidth => _playHalfWidth > 0f ? _playHalfWidth : Balance.FieldHalfWidth;

        /// <summary>当前对局的有效地面高度（方案 B：屏幕底；未注入时为数值表设计值）。</summary>
        public float PlayFloorY => float.IsNaN(_playFloorY) ? Balance.FieldFloorY : _playFloorY;

        /// <summary>
        /// 按屏幕可视范围注入场地边界（方案 B「屏幕即边框」）：左右墙贴屏幕左右边缘、地面按
        /// <c>GameBalance.FloorScreenInset</c> 从屏幕底边抬起（不贴死屏底，V2.42）。
        /// 边界变化后容器按新尺寸重建；<paramref name="halfWidth"/> ≤ 0 时忽略。
        /// </summary>
        public void SetPlayArea(float halfWidth, float floorY)
        {
            if (halfWidth > 0f)
                _playHalfWidth = halfWidth;

            _playFloorY = floorY;

            // 边界变了：让 EnsureArena 按新尺寸重新摆放墙/地面（CreateWall 会复用既有子节点）。
            _arenaBuilt = false;
            EnsureArena();
        }

        /// <summary>
        /// 注入「等级 → 水果外观」的正式美术（M7 在运行时调用）。
        /// 未注入时退化为「单一占位图 + 调色板染色」，即旧的占位美术行为。
        /// </summary>
        public void SetFruitArt(FruitArt art)
        {
            _art = art;

            if (art?.Fallback != null)
                fruitSprite = art.Fallback;
        }

        /// <summary>未注入正式美术时，用单一占位图构成退化集合，行为与占位美术时代完全一致。</summary>
        private FruitArt Art => _art ??= new FruitArt(null, fruitSprite);

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
            _arenaBuilt = false;

            if (buildArena)
            {
                EnsureArena();
                return;
            }

            // 显式要求「无容器」时必须清掉 Awake 已建好的场地，
            // 否则墙体/地面会拦住本该掉出场地的水果（EscapedFruit 测试即此场景）。
            var existing = transform.Find("Arena");
            if (existing != null)
            {
                DestroyObject(existing.gameObject);
                _arenaRoot = null;
            }
        }

        /// <summary>
        /// 运行时先建好容器（含可见外观与碰撞体），不依赖「第一次投放」才创建。
        /// 早期只在 Drop/SpawnAt 里 EnsureArena，导致开局第一帧看不到场地边界与地面。
        /// </summary>
        private void Awake() => EnsureArena();

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

            var limit = Mathf.Max(0f, PlayHalfWidth - tier.Radius);
            var clampedX = Mathf.Clamp(x, -limit, limit);
            var id = _nextId++;
            var jitter = balance.SpawnJitter > 0f ? (id % 2 == 0 ? 1f : -1f) * balance.SpawnJitter * 0.5f : 0f;

            Spawn(id, level, new Vector2(clampedX, balance.DropSpawnY), new Vector2(jitter, 0f));

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

        /// <summary>
        /// 在指定位置直接生成水果。用于测试装配与特殊玩法。
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

        /// <summary>
        /// 落点表面查询（供瞄准预览）：取该 x 处所有水果里最高的、且不高于起点的那颗的顶面；
        /// 没有则落到地面。这样预览线会停在堆叠表面上，而不是穿过去。
        /// </summary>
        public float GetLandingY(float x, float fromY)
        {
            var best = PlayFloorY;

            for (var i = 0; i < _ordered.Count; i++)
            {
                var fruit = _ordered[i];
                if (fruit == null || fruit.IsPending)
                    continue;

                var dx = x - fruit.transform.position.x;
                var radius = fruit.Radius;
                if (Mathf.Abs(dx) >= radius)
                    continue;

                var top = fruit.transform.position.y + Mathf.Sqrt(radius * radius - dx * dx);
                if (top > fromY || top <= best)
                    continue;

                best = top;
            }

            return best;
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

            RemoveInternal(a);
            RemoveInternal(b);

            var resultLevel = level + 1;
            var resultId = _nextId++;

            // V2.31b（需求方要求）：合成结果不再向上蹦，而是左右推开——
            // 交替方向避免总是往同一边挤；横向初速本身会通过碰撞把周围水果挤开、腾出继续合成的空间。
            var side = Balance.MergeResultSideImpulse;
            var sideVelocity = side <= 0f ? Vector2.zero : new Vector2(resultId % 2 == 0 ? side : -side, 0f);

            Spawn(resultId, resultLevel, mid, sideVelocity);

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
            var art = Art;
            body.Initialize(this, fruitId, tier, art.ForLevel(level), art.TintForLevel(level), Material,
                FruitPhysicsConfig.ForTier(balance, level));

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
            var halfWidth = PlayHalfWidth;
            var thickness = balance.WallThickness;
            var bottom = PlayFloorY;
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
                new Vector2(thickness, height), wallColor);
            CreateWall("RightWall", new Vector2(halfWidth + thickness * 0.5f, bottom + height * 0.5f),
                new Vector2(thickness, height), wallColor);
            CreateWall("Floor", new Vector2(0f, bottom - thickness * 0.5f),
                new Vector2(halfWidth * 2f + thickness * 2f, thickness), floorColor);

            _arenaBuilt = true;
        }

        private void CreateWall(string wallName, Vector2 center, Vector2 size, Color color)
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
            go.transform.localScale = Vector3.one;

            var collider = go.GetComponent<BoxCollider2D>();
            if (collider == null)
                collider = go.AddComponent<BoxCollider2D>();

            collider.size = size;
            collider.offset = Vector2.zero;
            collider.sharedMaterial = Material;

            // 方案 B：容器外观默认隐藏（屏幕即边框，边界已外移到屏幕边缘，再画框反而多余）。
            // 碰撞体必须保留——否则水果会直接掉出场地。
            var visual = go.transform.Find("Visual");
            if (!showArenaVisuals)
            {
                if (visual != null)
                    DestroyObject(visual.gameObject);

                return;
            }

            // 视觉放在子节点：碰撞体尺寸用局部单位，若缩放父节点会把碰撞体也一起缩放。
            if (visual == null)
            {
                var child = new GameObject("Visual");
                child.transform.SetParent(go.transform, false);
                visual = child.transform;
            }

            var renderer = visual.GetComponent<SpriteRenderer>();
            if (renderer == null)
                renderer = visual.gameObject.AddComponent<SpriteRenderer>();

            renderer.sprite = WallSprite;
            renderer.color = color;
            renderer.sortingOrder = -10;
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = new Vector3(size.x, size.y, 1f);
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
            var xLimit = PlayHalfWidth + balance.WallThickness + 2f;

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
