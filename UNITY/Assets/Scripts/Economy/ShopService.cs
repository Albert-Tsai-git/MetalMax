using Game.Core;
using Game.Items;
using Game.Tank;
using UnityEngine;

namespace Game.Economy
{
    /// <summary>商店买卖。纯逻辑，操作 PlayerState。</summary>
    public static class ShopService
    {
        /// <summary>卖出价 = 定价 × 该比例（数值平衡阶段再调）</summary>
        public const float SellRate = 0.5f;

        public static int SellPrice(PartInstance p) => Mathf.FloorToInt(p.data.price * SellRate);

        /// <summary>买入部件，放入背包</summary>
        public static OpResult Buy(PlayerState s, ShopData shop, TankPartData item, out PartInstance bought)
        {
            bought = null;
            if (shop == null || item == null || !shop.goods.Contains(item)) return OpResult.NotInShop;
            if (!s.TrySpend(item.price)) return OpResult.NotEnoughGold;

            bought = new PartInstance(item);
            s.AddToInventory(bought);
            Debug.Log($"[Shop] {shop.shopId} 买入 {item.partId}，花费 {item.price}G，余额 {s.Gold}G");
            EconomyEvents.RaiseBought(shop.shopId, bought);
            return OpResult.Ok;
        }

        /// <summary>买入道具</summary>
        public static OpResult BuyItem(PlayerState s, ShopData shop, ItemData item, int count = 1)
        {
            if (shop == null || item == null || !shop.items.Contains(item)) return OpResult.NotInShop;
            if (count <= 0) return OpResult.NothingToDo;
            if (ItemService.Count(s, item.itemId) + count > ItemService.MaxStack) return OpResult.StackFull;
            if (!s.TrySpend(item.price * count)) return OpResult.NotEnoughGold;
            ItemService.Add(s, item.itemId, count);
            Debug.Log($"[Shop] {shop.shopId} 买入道具 {item.itemId} ×{count}，余额 {s.Gold}G");
            return OpResult.Ok;
        }

        /// <summary>卖出背包中的部件（已装备的部件须先卸下）</summary>
        public static OpResult Sell(PlayerState s, PartInstance part)
        {
            if (part == null || !s.inventory.Contains(part)) return OpResult.NotInInventory;
            int gain = SellPrice(part);
            s.RemoveFromInventory(part);
            s.Gold += gain;
            Debug.Log($"[Shop] 卖出 {part.data.partId}，获得 {gain}G，余额 {s.Gold}G");
            EconomyEvents.RaiseSold(part, gain);
            return OpResult.Ok;
        }
    }
}
