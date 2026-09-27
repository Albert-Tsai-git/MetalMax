using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Battle
{
    /// <summary>
    /// 敌人行动决策：按技能权重选技能（回复技能仅在 HP 低于一半时考虑），按 AI 类型选目标。
    /// </summary>
    public static class EnemyAI
    {
        /// <summary>低于该 HP 比例时才会考虑回复技能</summary>
        public const float HealThreshold = 0.5f;

        public static BattleAction Decide(Combatant enemy, IReadOnlyList<Combatant> players, Random rng)
        {
            if (players.Count == 0) return null;
            var data = enemy.enemyData;
            var skill = data == null ? null : PickSkill(enemy, data, rng);
            if (skill != null && skill.IsHeal) return BattleAction.Skill(enemy, skill, enemy);
            var target = PickTarget(data?.ai ?? AiType.Random, players, rng);
            return skill != null ? BattleAction.Skill(enemy, skill, target) : BattleAction.Attack(enemy, target);
        }

        /// <summary>按权重抽取；抽中普通攻击返回 null</summary>
        private static SkillData PickSkill(Combatant enemy, EnemyData data, Random rng)
        {
            bool lowHp = enemy.hp < enemy.maxHp * HealThreshold;
            var options = new List<(SkillData skill, int weight)> { (null, Math.Max(0, data.attackWeight)) };
            foreach (var w in data.skills)
            {
                var skill = GameDB.Skill(w.skillId);
                if (skill == null || w.weight <= 0 || (skill.IsHeal && !lowHp)) continue;
                options.Add((skill, w.weight));
            }
            int total = options.Sum(o => o.weight);
            if (total <= 0) return null;
            int roll = rng.Next(total);
            foreach (var (skill, weight) in options)
            {
                roll -= weight;
                if (roll < 0) return skill;
            }
            return null;
        }

        private static Combatant PickTarget(AiType ai, IReadOnlyList<Combatant> players, Random rng)
        {
            IEnumerable<Combatant> pool = ai switch
            {
                AiType.Weakest => new[] { players.OrderBy(p => (float)p.hp / p.maxHp).First() },
                AiType.TankHunter => players.Where(p => p.IsTankActive),
                AiType.HumanHunter => players.Where(p => !p.IsTankActive),
                _ => players,
            };
            var list = pool.ToList();
            if (list.Count == 0) list = players.ToList(); // 没有符合偏好的目标时退回随机
            return list[rng.Next(list.Count)];
        }
    }
}
