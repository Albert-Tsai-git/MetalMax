using System;
using System.Collections.Generic;
using System.Linq;
using Game.Battle;
using Game.Core;
using Game.Economy;
using UnityEngine;

namespace Game.Items
{
    /// <summary>道具的持有与使用。纯逻辑，战斗内外共用。</summary>
    public static class ItemService
    {
        /// <summary>单种道具最多持有数量</summary>
        public const int MaxStack = 99;

        /// <summary>道具生效：使用者、道具 ID、受影响的单位、数值（回复量，补弹为 0）</summary>
        public static event Action<Combatant, string, Combatant, int> Used;

        public static int Count(PlayerState s, string itemId) => s.items.Find(i => i.id == itemId)?.count ?? 0;

        public static OpResult Add(PlayerState s, string itemId, int count = 1)
        {
            if (GameDB.Item(itemId) == null) return OpResult.NotFound;
            var stack = s.items.Find(i => i.id == itemId);
            if (stack == null) s.items.Add(stack = new ItemStack { id = itemId });
            if (stack.count + count > MaxStack) return OpResult.StackFull;
            stack.count += count;
            EconomyEvents.RaiseInventoryChanged();
            return OpResult.Ok;
        }

        /// <summary>是否能对该目标使用（不消耗）</summary>
        public static OpResult CanUse(PlayerState s, string itemId, Combatant target)
        {
            var item = GameDB.Item(itemId);
            if (item == null) return OpResult.NotFound;
            if (Count(s, itemId) <= 0) return OpResult.NotInInventory;
            var targets = Targets(s, item, target).ToList();
            if (targets.Count == 0) return OpResult.InvalidTarget;
            return targets.Any(t => WouldHelp(item, t)) ? OpResult.Ok : OpResult.NothingToDo;
        }

        /// <summary>使用道具：成功时消耗一个</summary>
        public static OpResult Use(PlayerState s, string itemId, Combatant user, Combatant target)
        {
            var r = CanUse(s, itemId, target);
            if (r != OpResult.Ok) return r;
            var item = GameDB.Item(itemId);
            foreach (var t in Targets(s, item, target).Where(t => WouldHelp(item, t)).ToList())
            {
                int value = Apply(item, t);
                Debug.Log($"[Item] {user?.id} 对 {t.id} 使用 {itemId}（{value}）");
                Used?.Invoke(user, itemId, t, value);
            }
            var stack = s.items.Find(i => i.id == itemId);
            if (--stack.count <= 0) s.items.Remove(stack);
            EconomyEvents.RaiseInventoryChanged();
            return OpResult.Ok;
        }

        private static IEnumerable<Combatant> Targets(PlayerState s, ItemData item, Combatant target)
        {
            if (item.allAllies) return s.party;
            return target != null && s.party.Contains(target) ? new[] { target } : Array.Empty<Combatant>();
        }

        private static bool WouldHelp(ItemData item, Combatant t) => item.kind switch
        {
            ItemKind.HealHp => t.IsAlive && t.hp < t.maxHp,
            ItemKind.Revive => !t.IsAlive,
            ItemKind.RepairSp => t.tank != null && !t.tank.IsDestroyed && t.tank.currentSp < t.tank.MaxSp,
            ItemKind.Refill => t.tank != null && t.tank.weapons.Any(w => w != null && w.data is Game.Tank.WeaponData wd
                                                                        && wd.maxAmmo >= 0 && w.currentAmmo < wd.maxAmmo),
            _ => false,
        };

        private static int Apply(ItemData item, Combatant t)
        {
            switch (item.kind)
            {
                case ItemKind.HealHp:
                case ItemKind.Revive:
                    int before = t.hp;
                    t.hp = Mathf.Min(t.maxHp, t.hp + item.amount);
                    return t.hp - before;
                case ItemKind.RepairSp:
                    int sp = t.tank.currentSp;
                    t.tank.currentSp = Mathf.Min(t.tank.MaxSp, sp + item.amount);
                    t.tank.NotifyChanged();
                    return t.tank.currentSp - sp;
                case ItemKind.Refill:
                    foreach (var w in t.tank.weapons) w?.Refill();
                    t.tank.NotifyChanged();
                    return 0;
            }
            return 0;
        }
    }
}
