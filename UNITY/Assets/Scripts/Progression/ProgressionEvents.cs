using System;
using Game.Battle;

namespace Game.Progression
{
    /// <summary>
    /// 成长事件（docs/INTERFACE.md I-15）。表现层只订阅；OnEnable 订阅、OnDisable 取消。
    /// </summary>
    public static class ProgressionEvents
    {
        /// <summary>获得经验：角色、经验值</summary>
        public static event Action<Combatant, int> ExpGained;
        /// <summary>升级：角色、新等级（一次获得多级时逐级触发）</summary>
        public static event Action<Combatant, int> LeveledUp;

        internal static void RaiseExpGained(Combatant c, int exp) => ExpGained?.Invoke(c, exp);
        internal static void RaiseLeveledUp(Combatant c, int level) => LeveledUp?.Invoke(c, level);
    }
}
