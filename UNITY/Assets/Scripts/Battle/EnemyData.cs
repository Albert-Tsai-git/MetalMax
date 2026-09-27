using System;
using System.Collections.Generic;
using Game.Core;
using Game.Tank;
using UnityEngine;

namespace Game.Battle
{
    [Serializable]
    public class WeightedSkill
    {
        public string skillId;
        [Min(0)] public int weight = 1;
    }

    /// <summary>敌人配置（普通怪和赏金首共用）</summary>
    [CreateAssetMenu(menuName = "Game/Battle/Enemy", fileName = "ENM_")]
    public class EnemyData : ScriptableObject
    {
        public string enemyId;

        public string DisplayName => TextDB.Name(enemyId);

        [Header("属性")]
        [Min(1)] public int maxHp = 50;
        [Min(0)] public int attack = 20;
        [Min(0)] public int defense = 10;
        [Min(0)] public int speed = 10;
        [Range(0, 100)] public int evade = 5;
        [Range(0, 100)] public int accuracy = 85;

        [Header("行动")]
        public AiType ai = AiType.Random;
        [Tooltip("普通攻击的权重，与技能权重一起抽取")]
        [Min(0)] public int attackWeight = 1;
        public List<WeightedSkill> skills = new();

        [Header("属性抗性")]
        public List<Element> weak = new();
        public List<Element> resist = new();
        public List<Element> immune = new();

        public const float WeakRate = 1.5f;
        public const float ResistRate = 0.5f;

        /// <summary>受到该属性攻击的伤害倍率</summary>
        public float ElementRate(Element e) =>
            immune.Contains(e) ? 0f : weak.Contains(e) ? WeakRate : resist.Contains(e) ? ResistRate : 1f;

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
            id = enemyId,
            enemyData = this,
            name = DisplayName + suffix,
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
