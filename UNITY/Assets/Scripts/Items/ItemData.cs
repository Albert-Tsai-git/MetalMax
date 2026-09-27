using System;
using Game.Core;
using UnityEngine;

namespace Game.Items
{
    /// <summary>道具效果类型</summary>
    public enum ItemKind
    {
        HealHp,     // 回复 HP（amount 点）
        Revive,     // 复活倒下的队员并回复 amount 点 HP
        RepairSp,   // 回复战车 SP（amount 点）
        Refill,     // 补满战车全部武器弹药
    }

    /// <summary>道具：由 Data/items.csv 生成，文本键 {ID}.name / {ID}.desc</summary>
    public class ItemData : ScriptableObject
    {
        public string itemId;
        [Min(0)] public int price = 10;
        public ItemKind kind;
        [Min(0)] public int amount;
        [Tooltip("作用于我方全体")]
        public bool allAllies;

        public string DisplayName => TextDB.Name(itemId);
        public string Description => TextDB.Desc(itemId);
    }

    /// <summary>背包中的一种道具及数量（可存档）</summary>
    [Serializable]
    public class ItemStack
    {
        public string id;
        public int count;
    }
}
