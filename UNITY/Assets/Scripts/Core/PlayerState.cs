using System;
using System.Collections.Generic;
using Game.Battle;
using Game.Economy;
using Game.Tank;

namespace Game.Core
{
    /// <summary>
    /// 玩家的全部可存档状态：队伍、金钱、经验、背包。纯数据，不依赖场景，便于测试与存档。
    /// </summary>
    [Serializable]
    public class PlayerState
    {
        public List<Combatant> party = new();
        public int exp;
        /// <summary>未装备的备用部件</summary>
        public List<PartInstance> inventory = new();

        private int _gold;
        public int Gold
        {
            get => _gold;
            set
            {
                int old = _gold;
                _gold = value;
                EconomyEvents.RaiseGoldChanged(old, _gold);
            }
        }

        public PlayerState(int gold = 0) => _gold = gold;

        /// <summary>扣钱；不足时不扣并返回 false</summary>
        public bool TrySpend(int amount)
        {
            if (amount < 0 || Gold < amount) return false;
            Gold -= amount;
            return true;
        }

        public void AddToInventory(PartInstance p)
        {
            inventory.Add(p);
            EconomyEvents.RaiseInventoryChanged();
        }

        public bool RemoveFromInventory(PartInstance p)
        {
            if (!inventory.Remove(p)) return false;
            EconomyEvents.RaiseInventoryChanged();
            return true;
        }
    }
}
