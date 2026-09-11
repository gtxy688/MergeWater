using MergeWater.Core;
using MergeWater.Field;
using UnityEngine;

namespace MergeWater.Tests.PlayMode
{
    /// <summary>PlayMode 场地测试脚手架：可配置重力与是否生成边界，退出时清理。</summary>
    internal sealed class FieldTestRig
    {
        public readonly GameObject Root;
        public readonly GameField Field;
        public readonly GameBalance Balance;

        public FieldTestRig(float gravity = -60f, bool buildArena = false)
        {
            Balance = GameBalance.CreateDefault();
            Root = new GameObject("FieldTestRig");
            Field = Root.AddComponent<GameField>();
            Field.Configure(Balance, null, null, buildArena);
            Physics2D.gravity = new Vector2(0f, gravity);
        }

        public float Radius(int level) => Balance.GetTier(level).Radius;

        public void Dispose()
        {
            if (Root != null)
                Object.Destroy(Root);

            Physics2D.gravity = new Vector2(0f, -9.81f);
        }
    }
}
