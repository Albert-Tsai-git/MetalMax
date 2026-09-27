using Game.Core;
using Game.Tank;
using UnityEngine;

namespace Game.Economy
{
    /// <summary>车库：装卸部件、改造、修理、补充装甲。纯逻辑，操作 PlayerState 与 TankLoadout。</summary>
    public static class GarageService
    {
        /// <summary>把背包中的部件装到战车上，换下的旧部件放回背包</summary>
        public static OpResult Equip(PlayerState s, TankLoadout tank, PartInstance part, int holeIndex = 0)
        {
            if (part == null || !s.inventory.Contains(part)) return OpResult.NotInInventory;
            var r = tank.TryEquip(part, holeIndex, out var replaced);
            if (r != OpResult.Ok) return r;

            s.RemoveFromInventory(part);
            if (replaced != null) s.AddToInventory(replaced);
            // 换底盘时不兼容的武器会被卸下，同样放回背包
            foreach (var w in tank.TakeDetachedWeapons()) s.AddToInventory(w);
            return OpResult.Ok;
        }

        /// <summary>卸下 C 装置或武器放回背包；底盘和引擎只能替换</summary>
        public static OpResult Unequip(PlayerState s, TankLoadout tank, PartSlot slot, int holeIndex = 0)
        {
            var r = tank.TryRemove(slot, holeIndex, out var removed);
            if (r != OpResult.Ok) return r;
            s.AddToInventory(removed);
            return OpResult.Ok;
        }

        /// <summary>改造部件（已装备或在背包中均可）；tank 为部件所在战车，背包中的部件传 null</summary>
        public static OpResult Upgrade(PlayerState s, PartInstance part, TankLoadout tank)
        {
            int cost = part.NextUpgradeCost;
            if (cost < 0) return OpResult.MaxUpgrade;
            if (s.Gold < cost) return OpResult.NotEnoughGold;
            if (tank != null && !tank.CanUpgradeWithoutOverweight(part)) return OpResult.Overweight;

            s.TrySpend(cost);
            part.upgradeLevel++;
            tank?.NotifyChanged();
            Debug.Log($"[Garage] {part.data.partId} 改造至 Lv{part.upgradeLevel}，花费 {cost}G");
            EconomyEvents.RaiseUpgraded(part);
            return OpResult.Ok;
        }

        /// <summary>修理全部部件并补满 SP 与弹药；金钱不足时不修理</summary>
        public static OpResult Repair(PlayerState s, TankLoadout tank, out int cost)
        {
            cost = tank.RepairCost();
            if (!s.TrySpend(cost)) return OpResult.NotEnoughGold;
            tank.RepairAll();
            return OpResult.Ok;
        }

        /// <summary>把剩余载重全部换成装甲（原型阶段免费）</summary>
        public static OpResult FillArmor(TankLoadout tank)
        {
            if (tank.chassis == null || tank.engine == null) return OpResult.NoChassis;
            tank.FillArmor();
            return OpResult.Ok;
        }
    }
}
