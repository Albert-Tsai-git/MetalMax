using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Battle
{
    /// <summary>
    /// 战斗原型测试：挂到空场景的任意物体上，运行后在 Console 看战斗日志。
    /// 不需要任何资产，部件和敌人都在代码里临时创建。
    /// 玩家策略：在车上且有弹药就开主炮，否则用副炮，车毁了用人攻击。
    /// </summary>
    public class BattleTestRunner : MonoBehaviour
    {
        [Tooltip("随机种子，0 表示每次随机")]
        public int seed = 0;
        [Tooltip("最大回合数，防止死循环")]
        public int maxTurns = 50;

        private void Start()
        {
            var hunter = new Combatant
            {
                name = "猎人", side = Side.Player,
                maxHp = 80, hp = 80, attack = 18, defense = 8, speed = 12,
                tank = Game.Core.DemoFactory.CreateTank(), inTank = true,
            };
            var mechanic = new Combatant
            {
                name = "机械师", side = Side.Player,
                maxHp = 60, hp = 60, attack = 12, defense = 6, speed = 9,
            };

            var enemies = new List<Combatant>
            {
                MakeEnemy("变异蚁 A", 40, 22, 6, 11),
                MakeEnemy("变异蚁 B", 40, 22, 6, 10),
                MakeEnemy("炮台虫", 90, 35, 15, 5),
            };

            var battle = new BattleSystem(new List<Combatant> { hunter, mechanic }, enemies, seed);

            while (battle.State == BattleState.WaitingForCommands && battle.Turn <= maxTurns)
            {
                var cmds = battle.AlivePlayers.Select(p => ChooseAction(p, battle)).ToList();
                battle.SubmitCommands(cmds);
            }

            Debug.Log($"[BattleTest] 结果：{battle.State}，共 {battle.Turn} 回合");
        }

        /// <summary>简单的自动指令，用来验证流程</summary>
        private static BattleAction ChooseAction(Combatant p, BattleSystem battle)
        {
            var target = battle.AliveEnemies.OrderBy(e => e.hp).First();
            if (p.IsTankActive)
            {
                var weapon = p.tank.weapons
                    .Where(w => w != null && w.IsFunctional && w.HasAmmo)
                    .OrderByDescending(w => w.Attack)
                    .FirstOrDefault();
                if (weapon != null) return BattleAction.Fire(p, weapon, target);
            }
            return BattleAction.Attack(p, target);
        }

        private static Combatant MakeEnemy(string name, int hp, int atk, int def, int spd) => new()
        {
            name = name, side = Side.Enemy,
            maxHp = hp, hp = hp, attack = atk, defense = def, speed = spd,
            expReward = hp / 4, goldReward = hp / 2,
        };

    }
}
