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
                    EnsureStockThen(ItemKind.Undo, () => UseClearField());
                    return;
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

        /// <summary>
        /// 侧边「撤销」位的功能已改为**清屏**（2026-09-13 需求方：「撤销」改为清屏，删除相关撤销逻辑）：
        /// 一次性清空全场水果，不再区分「最后一颗是否参与合成」——配套的
        /// `DropRecord` / `IFieldPort.TryPeekLastDrop` / `GameField.MarkLastDropMerged` 已一并删除。
        /// 内部 id 仍沿用 <see cref="ItemKind.Undo"/>（存档字段 `undoCount`/`undoGrantedToday` 随之保留，
        /// 避免动存档 schema）；玩家可见文案已全部改为「清屏」。
        /// </summary>
        private void UseClearField()
        {
            var context = _context;
            var session = context.Session;
            var field = context.Field;

            if (field.LiveFruitCount == 0)
            {
                session.NotifyItemUsed(ItemKind.Undo, ItemUseResult.NoTarget);
                context.Notify("场上没有行星");
                return;
            }

            if (context.Economy.SpendItem(ItemKind.Undo) != SpendResult.Spent)
            {
                session.NotifyItemUsed(ItemKind.Undo, ItemUseResult.NoStock);
                context.Notify("清屏数量不足");
                return;
            }

            field.ClearAll();
            session.NotifyItemUsed(ItemKind.Undo, ItemUseResult.Applied);
            context.Feedback?.PlayItemUse(ItemKind.Undo);
            context.Notify("已清空场地");
            context.RefreshBadges();
        }

    }
}
