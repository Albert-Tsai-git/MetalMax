using Game.Core;
using Game.Tank;
using UnityEngine;

namespace Game.Battle
{
    /// <summary>敌人技能：由 Data/skills.csv 生成，文本键 {ID}.name</summary>
    public class SkillData : ScriptableObject
    {
        public string skillId;
        [Tooltip("威力，攻击力的百分比（100 = 普通攻击）")]
        [Min(0)] public int power = 100;
        [Tooltip("连击次数，每次单独判定命中")]
        [Min(1)] public int hits = 1;
        public AttackRange range = AttackRange.Single;
        public Element element = Element.Normal;
        [Tooltip("命中修正（百分点）")]
        public int accuracyBonus;
        [Tooltip("命中战车时额外损坏部件的概率（0~100），与 SP 无关")]
        [Range(0, 100)] public int partBreakChance;
        [Tooltip("无视战车直接伤害乘员（毒气、声波等）")]
        public bool pierceTank;
        [Tooltip("自我回复最大 HP 的百分比；大于 0 时为回复技能，不攻击")]
        [Range(0, 100)] public int healPercent;

        public bool IsHeal => healPercent > 0;
        public string DisplayName => TextDB.Name(skillId);
    }

    /// <summary>敌人选择目标的方式</summary>
    public enum AiType
    {
        Random,         // 随机
        Weakest,        // 优先 HP 比例最低的
        TankHunter,     // 优先乘车单位
        HumanHunter,    // 优先未乘车的人
    }
}
