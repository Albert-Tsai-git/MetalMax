using Game.Battle;
using Game.Core;
using UnityEngine;

namespace Game.Progression
{
    /// <summary>经验与升级。纯逻辑。</summary>
    public static class LevelService
    {
        /// <summary>战斗胜利：存活的队员各获得全额经验（倒下的队员不获得）</summary>
        public static void AwardBattleExp(PlayerState s, int exp)
        {
            s.exp += exp;
            foreach (var c in s.party)
                if (c.IsAlive) GainExp(c, exp);
        }

        /// <summary>获得经验并按角色数据升级，返回升了几级。没有角色数据的单位只累计经验。</summary>
        public static int GainExp(Combatant c, int amount)
        {
            if (amount <= 0) return 0;
            var data = GameDB.Character(c.id);
            if (data == null)
            {
                c.exp += amount;
                return 0;
            }

            int gained = 0;
            c.exp += amount;
            ProgressionEvents.RaiseExpGained(c, amount);
            for (int need = data.ExpToNext(c.level); need > 0 && c.exp >= need; need = data.ExpToNext(c.level))
            {
                c.exp -= need;
                c.level++;
                ApplyGrowth(c, data);
                gained++;
                Debug.Log($"[Level] {c.id} 升到 Lv{c.level}");
                ProgressionEvents.RaiseLeveledUp(c, c.level);
            }
            if (data.ExpToNext(c.level) < 0) c.exp = 0; // 满级不再累计
            return gained;
        }

        /// <summary>剩余升级所需经验；满级或无角色数据返回 -1</summary>
        public static int ExpRemaining(Combatant c)
        {
            var data = GameDB.Character(c.id);
            int need = data?.ExpToNext(c.level) ?? -1;
            return need < 0 ? -1 : need - c.exp;
        }

        private static void ApplyGrowth(Combatant c, CharacterData d)
        {
            c.maxHp += d.hpPerLevel;
            if (c.IsAlive) c.hp += d.hpPerLevel;
            c.attack += d.attackPerLevel;
            c.defense += d.defensePerLevel;
            c.speed += d.speedPerLevel;
            c.evade = Mathf.Min(100, c.evade + d.evadePerLevel);
        }
    }
}
