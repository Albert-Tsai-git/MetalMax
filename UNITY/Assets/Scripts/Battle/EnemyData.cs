using UnityEngine;

namespace Game.Battle
{
    /// <summary>敌人配置（普通怪和赏金首共用）</summary>
    [CreateAssetMenu(menuName = "Game/Battle/Enemy", fileName = "ENM_")]
    public class EnemyData : ScriptableObject
    {
        public string enemyId;
        public string displayName;
        public GameObject modelPrefab;

        [Header("属性")]
        [Min(1)] public int maxHp = 50;
        [Min(0)] public int attack = 20;
        [Min(0)] public int defense = 10;
        [Min(0)] public int speed = 10;
        [Range(0, 100)] public int evade = 5;
        [Range(0, 100)] public int accuracy = 85;

        [Header("奖励")]
        [Min(0)] public int expReward = 10;
        [Min(0)] public int goldReward = 20;

        [Header("赏金首")]
        public bool isBounty;
        [Tooltip("赏金，在赏金办事处领取")]
        [Min(0)] public int bounty;

        /// <summary>生成一个战斗单位</summary>
        public Combatant CreateCombatant(string suffix = "") => new()
        {
            name = displayName + suffix,
            side = Side.Enemy,
            maxHp = maxHp,
            hp = maxHp,
            attack = attack,
            defense = defense,
            speed = speed,
            evade = evade,
            expReward = expReward,
            goldReward = goldReward,
        };
    }
}
