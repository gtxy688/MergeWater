using UnityEngine;

namespace MergeWater.Presentation
{
    /// <summary>
    /// 飘字（R22）：连击/得分飘字，punch 缩放后上浮淡出。
    ///
    /// <para><b>运行时不生成 UI 对象</b>（需求方 2026-09-12 要求）：飘字条目全部是
    /// <c>Main.unity</c> 里预先建好的对象（`Presentation/FloatingText/Pool` 下的若干条），
    /// 运行时只做「取一条、激活、播完归还」。早期实现每次飘字都 <c>new GameObject</c> +
    /// <c>AddComponent&lt;TextMesh&gt;</c>，既违反这条约束，也在连击时产生 GC 峰值。</para>
    /// </summary>
    public sealed class FloatingTextSpawner : MonoBehaviour
    {
        [Tooltip("池子容量；超出时复用最早的那条（同时最多显示这么多条飘字）。")]
        [SerializeField] private int poolSize = 12;

        [Tooltip("单条飘字对象（预先建好，挂 TextMeshPro + FloatingTextItem）。")]
        [SerializeField] private FloatingTextItem[] pool;

        // 轮转游标：飘字是短命特效，直接覆盖最早的一条比排队更符合「即时反馈」。
        private int _next;

        /// <summary>池子里可用的条目数（供测试断言「预置而非运行时生成」）。</summary>
        public int PoolCount => pool?.Length ?? 0;

        public void Configure(FloatingTextItem[] items) => pool = items;

        /// <summary>字体的统一来源：场景里预置的 TMP 文本已引用 SIMYOU SDF，这里不再运行时解析字体。</summary>
        public void Spawn(Vector2 worldPosition, string text, Color color, float sizeMultiplier = 1f)
        {
            if (string.IsNullOrEmpty(text))
                return;

            if (pool == null || pool.Length == 0)
            {
                Debug.LogWarning("[FloatingTextSpawner] 飘字池为空：请用 MergeWater/Build Main Scene 重建场景。");
                return;
            }

            var item = pool[_next];
            _next = (_next + 1) % pool.Length;

            if (item == null)
                return;

            item.transform.position = new Vector3(worldPosition.x, worldPosition.y, 0f);
            item.Play(text, color, sizeMultiplier);
        }
    }
}
