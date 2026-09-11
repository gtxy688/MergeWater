using MergeWater.Core;
using UnityEngine;

namespace MergeWater.Bootstrap
{
    /// <summary>
    /// 道具使用编排（R11–R15）：库存不足时先领取再使用；炸弹/锤子进入瞄准模式；
    /// 所有路径都保证「失败不扣库存」。
    /// </summary>
    public sealed class ItemUseController : MonoBehaviour
    {
        private GameContext _context;

        public void Initialize(GameContext context)
        {
            _context = context;
        }

        /// <summary>侧边入口点击（由 <c>PanelController.ItemEntryClicked</c> 转发）。</summary>
        public void OnEntryClicked(ItemKind kind)
        {
            var context = _context;
            if (context == null)
                return;

            if (kind == ItemKind.None)
                return;

            var session = context.Session;
            if (session == null || !session.CanUseItem())
            {
                context.Notify("当前不能使用道具");
                return;
            }

            switch (kind)
            {
                case ItemKind.Shake:
                    context.RequestShake();
                    return;

                case ItemKind.Undo:
                    EnsureStockThen(ItemKind.Undo, () => UseUndo());
                    return;

                case ItemKind.Bomb:
                case ItemKind.Hammer:
                    EnsureStockThen(kind, () => BeginAim(kind));
                    return;
            }
        }

        /// <summary>瞄准松手后由 <see cref="GameContext"/> 调用。</summary>
        public void ApplyTargetedItem(ItemKind kind, Vector2 point)
        {
            var context = _context;
            if (context == null)
                return;

            var session = context.Session;
            if (session == null || !session.CanUseItem())
            {
                session?.NotifyItemUsed(kind, ItemUseResult.NotAllowed);
                return;
            }

            var field = context.Field;
            var balance = context.Balance;

            if (kind == ItemKind.Bomb)
            {
                if (!field.HasFruitInRadius(point, balance.BombRadius))
                {
                    session.NotifyItemUsed(kind, ItemUseResult.NoTarget);
                    context.Notify("炸弹范围内没有水果");
                    return;
                }

                if (context.Economy.SpendItem(kind) != SpendResult.Spent)
                {
                    session.NotifyItemUsed(kind, ItemUseResult.NoStock);
                    context.Notify("炸弹数量不足");
                    return;
                }

                var removed = field.RemoveFruitInRadius(point, balance.BombRadius);
                session.NotifyItemUsed(kind, ItemUseResult.Applied);
                context.Feedback?.PlayItemUse(kind);
                context.Notify($"炸弹清除了 {removed} 颗水果");
                context.RefreshBadges();
                return;
            }

            if (kind == ItemKind.Hammer)
            {
                if (!field.HasFruitInRadius(point, balance.HammerMaxRadius))
                {
                    session.NotifyItemUsed(kind, ItemUseResult.NoTarget);
                    context.Notify("锤子范围内没有水果");
                    return;
                }

                if (context.Economy.SpendItem(kind) != SpendResult.Spent)
                {
                    session.NotifyItemUsed(kind, ItemUseResult.NoStock);
                    context.Notify("锤子数量不足");
                    return;
                }

                field.RemoveSingleNearest(point, balance.HammerMaxRadius, out _);
                session.NotifyItemUsed(kind, ItemUseResult.Applied);
                context.Feedback?.PlayItemUse(kind);
                context.Notify("锤子敲碎了 1 颗水果");
                context.RefreshBadges();
            }
        }

        private void EnsureStockThen(ItemKind kind, System.Action onReady)
        {
            var context = _context;
            if (context == null)
                return;

            if (context.Economy.ItemCount(kind) > 0)
            {
                onReady?.Invoke();
                return;
            }

            // 决策 D11：侧边「免费」入口在库存为 0 时先领取，领取成功后自动继续该次使用。
            var decision = context.Economy.CanGrantItem(kind);
            if (!decision.IsAllowed)
            {
                context.Notify(decision.Reason);
                return;
            }

            context.RequestItemGrant(kind, onReady);
        }

        private void UseUndo()
        {
            var context = _context;
            var session = context.Session;
            var field = context.Field;

            if (!field.TryPeekLastDrop(out var record) || record.Merged)
            {
                session.NotifyItemUsed(ItemKind.Undo, ItemUseResult.NoTarget);
                context.Notify("没有可撤销的水果（最后一颗已参与合成）");
                return;
            }

            if (context.Economy.SpendItem(ItemKind.Undo) != SpendResult.Spent)
            {
                session.NotifyItemUsed(ItemKind.Undo, ItemUseResult.NoStock);
                context.Notify("撤销数量不足");
                return;
            }

            field.RemoveFruit(record.FruitId);
            session.NotifyItemUsed(ItemKind.Undo, ItemUseResult.Applied);
            context.Feedback?.PlayItemUse(ItemKind.Undo);
            context.Notify("撤销了最后一颗水果");
            context.RefreshBadges();
        }

        private void BeginAim(ItemKind kind)
        {
            var context = _context;
            var radius = kind == ItemKind.Bomb ? context.Balance.BombRadius : context.Balance.HammerMaxRadius;

            context.Aim.BeginItemAim(kind, radius);
            context.Notify(kind == ItemKind.Bomb ? "点击场地选择炸弹落点" : "点击场地选择要敲碎的水果");
        }
    }
}
