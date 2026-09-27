using System.Collections.Generic;
using Game.Tank;

namespace Game.Battle
{
    public enum ActionType
    {
        HumanAttack,    // 人类攻击
        TankWeapon,     // 战车武器攻击
        BoardTank,      // 上车
        LeaveTank,      // 下车
        Defend,         // 防御（本回合受伤减半）
        Escape,         // 逃跑
    }

    /// <summary>一次行动指令</summary>
    public class BattleAction
    {
        public Combatant actor;
        public ActionType type;
        /// <summary>目标列表；群攻/全体攻击时由系统展开</summary>
        public List<Combatant> targets = new();
        /// <summary>使用的战车武器（TankWeapon 时有效）</summary>
        public PartInstance weapon;

        public static BattleAction Attack(Combatant actor, Combatant target) =>
            new() { actor = actor, type = ActionType.HumanAttack, targets = { target } };

        public static BattleAction Fire(Combatant actor, PartInstance weapon, Combatant target) =>
            new() { actor = actor, type = ActionType.TankWeapon, weapon = weapon, targets = { target } };

        public static BattleAction Simple(Combatant actor, ActionType type) =>
            new() { actor = actor, type = type };
    }
}
