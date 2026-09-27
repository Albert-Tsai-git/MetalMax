using Game.Battle;
using Game.Core;
using UnityEngine;

namespace Game.Progression
{
    /// <summary>可加入队伍的角色：初始属性、每级成长与经验曲线。由 Data/characters.csv 生成。</summary>
    public class CharacterData : ScriptableObject
    {
        public string characterId;

        [Header("1 级属性")]
        [Min(1)] public int hp = 50;
        [Min(0)] public int attack = 10;
        [Min(0)] public int defense = 5;
        [Min(0)] public int speed = 10;
        [Range(0, 100)] public int evade = 5;

        [Header("每级成长")]
        [Min(0)] public int hpPerLevel = 5;
        [Min(0)] public int attackPerLevel = 1;
        [Min(0)] public int defensePerLevel = 1;
        [Min(0)] public int speedPerLevel = 1;
        [Min(0)] public int evadePerLevel = 0;

        [Header("战斗中修理（机械师专长，猎人也会一点）")]
        [Tooltip("1 级时一次修理回复的 SP，0 表示不会修理")]
        [Min(0)] public int repair;
        [Tooltip("每级增加的修理量")]
        [Min(0)] public int repairPerLevel;
        [Tooltip("达到该等级后，SP 已满时可把一个“损坏”部件修回正常（大破不能在战斗中修）")]
        [Min(0)] public int partRepairLevel = 99;

        public int RepairAmount(int level) => repair <= 0 ? 0 : repair + repairPerLevel * (level - 1);

        [Header("经验曲线：升到下一级所需 = expBase × 当前等级 ^ expGrowth")]
        [Min(1)] public int maxLevel = 99;
        [Min(1)] public int expBase = 20;
        [Min(1f)] public float expGrowth = 1.5f;

        public string DisplayName => TextDB.Name(characterId);

        /// <summary>从 level 升到 level+1 所需经验；已满级返回 -1</summary>
        public int ExpToNext(int level) =>
            level >= maxLevel ? -1 : Mathf.Max(1, Mathf.RoundToInt(expBase * Mathf.Pow(level, expGrowth)));

        /// <summary>生成 1 级角色</summary>
        public Combatant CreateCombatant() => new()
        {
            id = characterId,
            name = DisplayName,
            side = Side.Player,
            level = 1,
            maxHp = hp, hp = hp, attack = attack, defense = defense, speed = speed, evade = evade,
        };
    }
}
