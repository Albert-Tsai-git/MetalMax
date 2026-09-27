using System;
using Game.Battle;
using Game.Core;
using Game.Economy;
using Game.Items;
using UnityEngine;

namespace Game.Equipment
{
    /// <summary>
    /// 人类装备：装备/卸下（与装备袋交换）、商店买卖。装备袋 PlayerState.gearBag 按 ID 计数。
    /// </summary>
    public static class EquipmentService
    {
        /// <summary>装备变化（角色）</summary>
        public static event Action<Combatant> Changed;

        public static int BagCount(PlayerState s, string gearId) => s.gearBag.Find(g => g.id == gearId)?.count ?? 0;

        public static void AddToBag(PlayerState s, string gearId, int count = 1)
        {
            var st = s.gearBag.Find(g => g.id == gearId);
            if (st == null) s.gearBag.Add(new ItemStack { id = gearId, count = count });
            else st.count += count;
        }

        private static bool TakeFromBag(PlayerState s, string gearId)
        {
            var st = s.gearBag.Find(g => g.id == gearId);
            if (st == null || st.count <= 0) return false;
            if (--st.count == 0) s.gearBag.Remove(st);
            return true;
        }

        /// <summary>从装备袋装备到角色；同位置原有装备放回装备袋</summary>
        public static OpResult Equip(PlayerState s, Combatant member, string gearId)
        {
            var g = GameDB.Gear(gearId);
            if (g == null || member == null) return OpResult.NotFound;
            if (!g.CanEquip(member.id)) return OpResult.CannotEquip;
            if (!TakeFromBag(s, gearId)) return OpResult.NotInInventory;
            var old = member.GearIn(g.slot);
            if (old != null)
            {
                member.gear.Remove(old.gearId);
                AddToBag(s, old.gearId);
            }
            member.gear.Add(gearId);
            Debug.Log($"[Equip] {member.id} 装备 {gearId}" + (old != null ? $"（换下 {old.gearId}）" : ""));
            Changed?.Invoke(member);
            return OpResult.Ok;
        }

        /// <summary>卸下某位置的装备放回装备袋</summary>
        public static OpResult Unequip(PlayerState s, Combatant member, GearSlot slot)
        {
            var old = member?.GearIn(slot);
            if (old == null) return OpResult.SlotEmpty;
            member.gear.Remove(old.gearId);
            AddToBag(s, old.gearId);
            Debug.Log($"[Equip] {member.id} 卸下 {old.gearId}");
            Changed?.Invoke(member);
            return OpResult.Ok;
        }

        public static int SellPrice(GearData g) => Mathf.FloorToInt(g.price * ShopService.SellRate);

        public static OpResult Buy(PlayerState s, ShopData shop, GearData g)
        {
            if (shop == null || g == null || !shop.gear.Contains(g)) return OpResult.NotInShop;
            if (!s.TrySpend(g.price)) return OpResult.NotEnoughGold;
            AddToBag(s, g.gearId);
            Debug.Log($"[Shop] {shop.shopId} 买入装备 {g.gearId}，余额 {s.Gold}G");
            return OpResult.Ok;
        }

        /// <summary>卖出装备袋中的装备（身上的须先卸下）</summary>
        public static OpResult Sell(PlayerState s, string gearId)
        {
            var g = GameDB.Gear(gearId);
            if (g == null) return OpResult.NotFound;
            if (!TakeFromBag(s, gearId)) return OpResult.NotInInventory;
            s.Gold += SellPrice(g);
            Debug.Log($"[Shop] 卖出装备 {gearId}，获得 {SellPrice(g)}G，余额 {s.Gold}G");
            return OpResult.Ok;
        }
    }
}
