using System;
using System.Collections.Generic;
using Game.Battle;
using Game.Core;
using UnityEngine;

namespace Game.Field
{
    /// <summary>
    /// 遇敌区域：定义这片区域的遇敌率和敌人组合。
    /// 挂在带 Trigger Collider 的物体上；玩家在区域内时，由 RandomEncounter 使用它。
    /// 没有配置 EnemyData 资产时，使用内置的演示敌人。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class EncounterZone : MonoBehaviour
    {
        [Serializable]
        public class EnemyGroup
        {
            public EnemyData[] members;
            [Min(1)] public int weight = 1;
        }

        [Tooltip("每走 1 米触发遇敌的概率")]
        [Range(0f, 1f)] public float encounterRatePerMeter = 0.03f;
        [Tooltip("刚结束战斗后的安全距离（米），这段距离内不会遇敌")]
        [Min(0f)] public float safeDistance = 5f;
        public List<EnemyGroup> groups = new();

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        /// <summary>按权重随机抽取一组敌人</summary>
        public List<Combatant> RollEnemies(System.Random rng)
        {
            var valid = groups.FindAll(g => g.members is { Length: > 0 });
            if (valid.Count == 0) return DemoEnemies(rng);

            int total = 0;
            foreach (var g in valid) total += g.weight;
            int roll = rng.Next(total);
            foreach (var g in valid)
            {
                roll -= g.weight;
                if (roll >= 0) continue;
                var list = new List<Combatant>();
                for (int i = 0; i < g.members.Length; i++)
                    if (g.members[i] != null)
                        list.Add(g.members[i].CreateCombatant(g.members.Length > 1 ? $" {(char)('A' + i)}" : ""));
                return list;
            }
            return DemoEnemies(rng);
        }

        private static List<Combatant> DemoEnemies(System.Random rng)
        {
            int count = rng.Next(1, 4);
            var list = new List<Combatant>();
            for (int i = 0; i < count; i++)
                list.Add(new Combatant
                {
                    id = "ENM_Ant", name = $"{TextDB.Name("ENM_Ant")} {(char)('A' + i)}", side = Side.Enemy,
                    maxHp = 40, hp = 40, attack = 22, defense = 6, speed = 10 + i,
                    expReward = 10, goldReward = 20,
                });
            return list;
        }
    }
}
