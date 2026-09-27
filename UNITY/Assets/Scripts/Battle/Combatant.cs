using System;
using Game.Tank;

namespace Game.Battle
{
    public enum Side { Player, Enemy }

    /// <summary>
    /// 战斗单位。玩家角色可以乘坐战车：乘车时受到的伤害由战车承担，攻击使用战车武器。
    /// </summary>
    [Serializable]
    public class Combatant
    {
        /// <summary>数据 ID（ENM_ / CHR_），表现层据此加载模型与文本；演示单位可为空</summary>
        public string id;
        public string name;
        public Side side;
        /// <summary>等级与当前等级内的经验（玩家角色用）</summary>
        public int level = 1;
        public int exp;
        public int maxHp;
        public int hp;
        public int attack;
        public int defense;
        public int speed;
        [UnityEngine.Range(0, 100)] public int evade = 5;

        /// <summary>所属战车，可以为空</summary>
        public TankLoadout tank;
        /// <summary>当前是否在车上</summary>
        public bool inTank;

        /// <summary>击败后的奖励（敌人用）</summary>
        public int expReward;
        public int goldReward;

        public bool IsAlive => hp > 0;

        /// <summary>乘车且战车还能战斗</summary>
        public bool IsTankActive => inTank && tank != null && !tank.IsDestroyed;

        /// <summary>单位还能行动（人活着即可；车全毁时人会被迫下车）</summary>
        public bool CanAct => IsAlive;

        /// <summary>当前总回避：乘车时加上 C 装置加成</summary>
        public int TotalEvade
        {
            get
            {
                int e = evade;
                if (IsTankActive && tank.cUnit is { IsFunctional: true } c)
                    e += (int)(((CUnitData)c.data).evadeBonus * c.PerformanceRate);
                return e;
            }
        }

        /// <summary>当前总防御：乘车时加上所有可用部件防御</summary>
        public int TotalDefense
        {
            get
            {
                if (!IsTankActive) return defense;
                int d = 0;
                foreach (var p in tank.AllParts())
                    d += (int)(p.data.defense * p.PerformanceRate);
                return d;
            }
        }

        /// <summary>每回合行动次数：乘车时由 C 装置决定</summary>
        public int ActionsPerTurn =>
            IsTankActive && tank.cUnit is { IsFunctional: true } c ? ((CUnitData)c.data).actionsPerTurn : 1;

        public override string ToString() => name;
    }
}
