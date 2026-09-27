using System.Collections.Generic;
using Game.Core;
using Game.Tank;
using UnityEngine;

namespace Game.Equipment
{
    /// <summary>人类装备位置：武器 + 头、身、手、脚四件防具</summary>
    public enum GearSlot { Weapon, Head, Body, Arms, Legs }

    /// <summary>人类装备：由 Data/gear.csv 生成。只在步行（不乘车）时生效</summary>
    public class GearData : ScriptableObject
    {
        public string gearId;
        public GearSlot slot;
        [Min(0)] public int price;
        public int attack;
        public int defense;
        public int evade;
        [Tooltip("武器攻击范围")]
        public AttackRange range = AttackRange.Single;
        [Tooltip("武器属性")]
        public Element element = Element.Normal;
        [Tooltip("可装备的角色 ID，空为全员")]
        public List<string> users = new();

        public bool CanEquip(string characterId) => users.Count == 0 || users.Contains(characterId);
        public string DisplayName => TextDB.Name(gearId);
    }
}
