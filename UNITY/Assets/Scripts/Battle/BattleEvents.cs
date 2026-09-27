using System;
using Game.Tank;

namespace Game.Battle
{
    /// <summary>
    /// 战斗对外事件（docs/INTERFACE.md I-09）。表现层（UI / 动效 / 音频）只订阅，不修改战斗状态。
    /// 静态事件：订阅方须在 OnEnable 订阅、OnDisable 取消。
    /// </summary>
    public static class BattleEvents
    {
        /// <summary>战斗开始，参数为本场战斗（可读取 players / enemies）</summary>
        public static event Action<BattleSystem> Started;
        /// <summary>回合开始，参数为回合数（从 1 开始）</summary>
        public static event Action<int> TurnStarted;
        /// <summary>行动开始：行动者、行动类型、所用武器（非武器行动为 null）</summary>
        public static event Action<Combatant, ActionType, PartInstance> ActionStarted;
        /// <summary>未命中：攻击者、目标</summary>
        public static event Action<Combatant, Combatant> Missed;
        /// <summary>命中：攻击者、目标、伤害值、是否打在战车上</summary>
        public static event Action<Combatant, Combatant, int, bool> Hit;
        /// <summary>部件受损：所属者、部件（PartInstance.condition 为损坏后状态）</summary>
        public static event Action<Combatant, PartInstance> PartDamaged;
        /// <summary>战车失去战斗能力，乘员被迫下车</summary>
        public static event Action<Combatant> TankDisabled;
        /// <summary>上下车：行动者、是否在车上</summary>
        public static event Action<Combatant, bool> BoardChanged;
        /// <summary>单位倒下</summary>
        public static event Action<Combatant> Defeated;
        /// <summary>逃跑判定：行动者、是否成功</summary>
        public static event Action<Combatant, bool> EscapeAttempted;
        /// <summary>使用技能：行动者、技能 ID</summary>
        public static event Action<Combatant, string> SkillUsed;
        /// <summary>使用道具：使用者、道具 ID、指定目标（全体道具为选择的目标或 null）</summary>
        public static event Action<Combatant, string, Combatant> ItemUsed;
        /// <summary>属性生效：目标、属性、倍率（&gt;1 弱点，&lt;1 抗性，0 免疫）</summary>
        public static event Action<Combatant, Element, float> ElementHit;
        /// <summary>战斗中修理：修理者、战车所属者、回复 SP、修好的部件（没有为 null）</summary>
        public static event Action<Combatant, Combatant, int, PartInstance> Repaired;
        /// <summary>HP 回复：单位、回复量</summary>
        public static event Action<Combatant, int> Healed;
        /// <summary>战斗结束：结果、获得经验、获得金钱（非胜利时为 0）</summary>
        public static event Action<BattleState, int, int> Ended;

        internal static void RaiseStarted(BattleSystem b) => Started?.Invoke(b);
        internal static void RaiseTurnStarted(int turn) => TurnStarted?.Invoke(turn);
        internal static void RaiseActionStarted(Combatant a, ActionType t, PartInstance w) => ActionStarted?.Invoke(a, t, w);
        internal static void RaiseMissed(Combatant a, Combatant t) => Missed?.Invoke(a, t);
        internal static void RaiseHit(Combatant a, Combatant t, int dmg, bool onTank) => Hit?.Invoke(a, t, dmg, onTank);
        internal static void RaisePartDamaged(Combatant owner, PartInstance p) => PartDamaged?.Invoke(owner, p);
        internal static void RaiseTankDisabled(Combatant owner) => TankDisabled?.Invoke(owner);
        internal static void RaiseBoardChanged(Combatant a, bool inTank) => BoardChanged?.Invoke(a, inTank);
        internal static void RaiseDefeated(Combatant c) => Defeated?.Invoke(c);
        internal static void RaiseEscapeAttempted(Combatant a, bool ok) => EscapeAttempted?.Invoke(a, ok);
        internal static void RaiseSkillUsed(Combatant a, string id) => SkillUsed?.Invoke(a, id);
        internal static void RaiseItemUsed(Combatant a, string id, Combatant t) => ItemUsed?.Invoke(a, id, t);
        internal static void RaiseElementHit(Combatant t, Element e, float rate) => ElementHit?.Invoke(t, e, rate);
        internal static void RaiseRepaired(Combatant a, Combatant t, int sp, PartInstance p) => Repaired?.Invoke(a, t, sp, p);
        internal static void RaiseHealed(Combatant c, int amount) => Healed?.Invoke(c, amount);
        internal static void RaiseEnded(BattleState r, int exp, int gold) => Ended?.Invoke(r, exp, gold);
    }
}
