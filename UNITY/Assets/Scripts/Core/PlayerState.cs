using System;
using System.Collections.Generic;
using Game.Battle;
using Game.Economy;
using Game.Items;
using Game.Tank;
using Game.Story;
using Game.Town;

namespace Game.Core
{
    /// <summary>下车后战车的停放位置（可存档）</summary>
    [Serializable]
    public class VehicleState
    {
        public bool parked;
        public string scene;
        public UnityEngine.Vector3 position;
        public float yaw;
    }

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
        /// <summary>道具（ID 与数量）</summary>
        public List<ItemStack> items = new();
        /// <summary>战车停放状态：parked 为真表示玩家下车步行，战车停在 scene 的 position</summary>
        public VehicleState vehicle = new();
        /// <summary>最后到访的城镇，全灭后在此复活</summary>
        public string lastTown = DefaultTown;
        /// <summary>赏金首进度（未出现在列表中的视为通缉中）</summary>
        public List<BountyRecord> bounties = new();
        /// <summary>剧情标记</summary>
        public List<string> flags = new();
        /// <summary>任务进度（未出现的任务视为未开始）</summary>
        public List<QuestRecord> quests = new();

        public const string DefaultTown = "TWN_Zhanqiao";

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
