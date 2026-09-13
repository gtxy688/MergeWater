using MergeWater.Core;
using UnityEngine;

namespace MergeWater.Field
{
    /// <summary>水果物理参数（由 <see cref="GameBalance"/> 派生，避免逐字段传参）。</summary>
    public readonly struct FruitPhysicsConfig
    {
        public readonly float MaxSpeed;
        public readonly float SettleLinear;
        public readonly float SettleAngular;
        public readonly float SettleGrace;
        public readonly float LinearDrag;
        public readonly float AngularDrag;
        public readonly float GravityScale;

        public FruitPhysicsConfig(GameBalance balance)
        {
            MaxSpeed = balance.MaxLinearVelocity;
            SettleLinear = balance.DangerSettleLinearSpeed;
            SettleAngular = balance.DangerSettleAngularSpeed;
            SettleGrace = balance.DangerSettleGraceSeconds;
            LinearDrag = balance.FruitLinearDrag;
            AngularDrag = balance.FruitAngularDrag;
            GravityScale = 1f; // 按等级在 Initialize 中覆盖（V2.28）
        }

        private FruitPhysicsConfig(GameBalance balance, float gravityScale)
        {
            MaxSpeed = balance.MaxLinearVelocity;
            SettleLinear = balance.DangerSettleLinearSpeed;
            SettleAngular = balance.DangerSettleAngularSpeed;
            SettleGrace = balance.DangerSettleGraceSeconds;
            LinearDrag = balance.FruitLinearDrag;
            AngularDrag = balance.FruitAngularDrag;
            GravityScale = gravityScale;
        }

        public static FruitPhysicsConfig ForTier(GameBalance balance, int level) =>
            new FruitPhysicsConfig(balance, balance.GetGravityScale(level));
    }

    /// <summary>
    /// 单颗水果实体。物理体与碰撞体在根节点（localScale 保持 1，半径直接取自 V1），
    /// 视觉在子节点上缩放，便于表现层做挤压动画而不影响碰撞。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class FruitBody : MonoBehaviour
    {
        private GameField _field;
        private float _maxSpeed;
        private float _settleLinear;
        private float _settleAngular;
        private float _settleGrace;

        public int FruitId { get; private set; }
        public int Level { get; private set; }
        public float Radius { get; private set; }
        public bool IsPending { get; private set; }

        /// <summary>连续静止时长。越线判定用它过滤「刚生成 / 仍在弹跳」的水果（V2.9）。</summary>
        public float SettledDuration { get; private set; }

        public Rigidbody2D Body { get; private set; }
        public CircleCollider2D Collider { get; private set; }

        public Transform Visual { get; private set; }

        public void Initialize(GameField field, int fruitId, in FruitTierDefinition tier, Sprite sprite, Color color,
            PhysicsMaterial2D material, in FruitPhysicsConfig physics)
        {
            _field = field;
            FruitId = fruitId;
            Level = tier.Level;
            Radius = tier.Radius;
            _maxSpeed = physics.MaxSpeed;
            _settleLinear = physics.SettleLinear;
            _settleAngular = physics.SettleAngular;
            _settleGrace = physics.SettleGrace;
            SettledDuration = 0f;

            Body = GetComponent<Rigidbody2D>();
            Collider = GetComponent<CircleCollider2D>();

            Collider.radius = tier.Radius;
            Collider.offset = Vector2.zero;
            Collider.sharedMaterial = material;

            Body.mass = tier.Mass;
            Body.bodyType = RigidbodyType2D.Dynamic;
            Body.gravityScale = physics.GravityScale; // V2.28
            Body.drag = physics.LinearDrag;
            Body.angularDrag = physics.AngularDrag;
            Body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            Body.interpolation = RigidbodyInterpolation2D.Interpolate;
            Body.sleepMode = RigidbodySleepMode2D.StartAwake;

            EnsureVisual(sprite, color);
        }

        public void SetPending(bool pending)
        {
            IsPending = pending;
            if (Collider != null)
                Collider.enabled = !pending;
        }

        /// <summary>吸附期间直接搬位置，避免物理把两颗水果推开。</summary>
        public void MoveTo(Vector2 position)
        {
            Body.velocity = Vector2.zero;
            Body.angularVelocity = 0f;
            Body.position = position;
            transform.position = position;
        }

        /// <summary>用于暂停/广告期间冻结：记录并清零速度，切到 Kinematic。</summary>
        public void Freeze(out Vector2 linear, out float angular)
        {
            linear = Body.velocity;
            angular = Body.angularVelocity;
            Body.velocity = Vector2.zero;
            Body.angularVelocity = 0f;
            Body.bodyType = RigidbodyType2D.Kinematic;
        }

        public void Thaw(Vector2 linear, float angular)
        {
            Body.bodyType = RigidbodyType2D.Dynamic;
            Body.velocity = linear;
            Body.angularVelocity = angular;
            SettledDuration = 0f;
        }

        /// <summary>合成结果的小幅弹起，避免立刻与邻近水果再次重叠。</summary>
        public void Pop(float upwardImpulse)
        {
            Body.velocity = Vector2.up * upwardImpulse;
            SettledDuration = 0f;
        }

        /// <summary>是否已达到「稳定静止」门槛，用于越线判定。</summary>
        public bool IsSettled => SettledDuration >= _settleGrace;

        private void FixedUpdate()
        {
            if (Body.bodyType != RigidbodyType2D.Dynamic)
                return;

            var velocity = Body.velocity;
            var speed = velocity.magnitude;
            if (_maxSpeed > 0f && speed > _maxSpeed)
                Body.velocity = velocity * (_maxSpeed / speed);

            var angularSpeed = Mathf.Abs(Body.angularVelocity);
            if (angularSpeed > _settleAngular * 20f)
                Body.angularVelocity = Mathf.Sign(Body.angularVelocity) * _settleAngular * 20f;

            if (speed <= _settleLinear && angularSpeed <= _settleAngular)
                SettledDuration += Time.fixedDeltaTime;
            else
                SettledDuration = 0f;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            _field?.OnFruitCollision(this, collision);
        }

        /// <summary>
        /// 碰撞停留也尝试合成（V2.32）。同级水果可能因为一次合成被取消而持续贴合却不再触发「进入」事件，
        /// 只监听进入会导致「贴着但不合成」；<see cref="GameField"/> 内部有去重，重复调用是安全的。
        /// </summary>
        private void OnCollisionStay2D(Collision2D collision)
        {
            _field?.OnFruitCollision(this, collision);
        }

        private void EnsureVisual(Sprite sprite, Color color)
        {
            var existing = transform.Find("Visual");
            if (existing == null)
            {
                var go = new GameObject("Visual");
                go.transform.SetParent(transform, false);
                existing = go.transform;
                go.AddComponent<SpriteRenderer>();
            }

            Visual = existing;
            Visual.localPosition = Vector3.zero;
            Visual.localRotation = Quaternion.identity;

            // 视觉直径必须等于碰撞直径。贴图的世界尺寸 = 像素 ÷ PPU，所以必须先归一化：
            // 直接把 localScale 写成 Radius×2 只在「贴图恰好 1×1 世界单位」时成立，
            // 换成 1280px @ PPU 100 的正式素材就会大 12.8 倍、把整个场地糊住（实测 0.36 → 0.9216）。
            // 按贴图实际世界尺寸换算后，换任何尺寸/PPU 的素材都不必再管导入设置。
            var size = sprite != null ? (Vector2)sprite.bounds.size : Vector2.one;
            var scaleX = size.x > 1e-4f ? Radius * 2f / size.x : Radius * 2f;
            var scaleY = size.y > 1e-4f ? Radius * 2f / size.y : Radius * 2f;
            Visual.localScale = new Vector3(scaleX, scaleY, 1f);

            var spriteRenderer = Visual.GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = 100 + Level;
        }
    }
}
