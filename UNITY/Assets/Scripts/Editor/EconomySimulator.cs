using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Battle;
using Game.Core;
using Game.Economy;
using Game.Items;
using Game.Tank;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace Game.EditorTools
{
    /// <summary>
    /// 第一幕进度模拟：1 级、500G、初始战车出发，在野外反复战斗；HP 或 SP 低于一半、或主炮弹药耗尽时回城
    /// （住宿、修理、补弹，钱不够时能补多少补多少）。统计达成各里程碑所需的战斗场数。
    /// 结果写入 docs/balance/economy_latest.md。
    /// </summary>
    public static class EconomySimulator
    {
        private const int Runs = 200;
        private const int MaxFights = 200;
        private const string OutPath = "../docs/balance/economy_latest.md";

        /// <summary>野外遇敌表（与 PrototypeSceneBuilder 的野外遇敌区一致）</summary>
        public static readonly (int weight, string[] members)[] FieldTable =
        {
            (3, new[] { "ENM_Ant", "ENM_Ant" }),
            (2, new[] { "ENM_Dog", "ENM_Dog" }),
            (2, new[] { "ENM_Ant", "ENM_Ant", "ENM_Ant" }),
            (1, new[] { "ENM_Dog", "ENM_Ant" }),
        };

        /// <summary>里程碑：名称与判定</summary>
        private static readonly (string name, Func<PlayerState, bool> reached)[] Milestones =
        {
            ("可买火焰喷射器", s => s.Gold >= Price("WPN_Flamethrower")),
            ("可买电击炮", s => s.Gold >= Price("WPN_ShockCannon")),
            ("升到 Lv3（泵站推荐）", s => s.party[0].level >= 3),
            ("升到 Lv5（巨蟹推荐）", s => s.party[0].level >= 5),
            ("Lv5 且可买电击炮", s => s.party[0].level >= 5 && s.Gold >= Price("WPN_ShockCannon")),
        };

        [MenuItem("Game/第一幕进度模拟（经济）")]
        public static void Run()
        {
            GameDB.Reload();
            var reachedAt = Milestones.Select(_ => new List<int>()).ToArray();
            int defeats = 0, townTrips = 0;
            long upkeep = 0, income = 0;

            for (int seed = 1; seed <= Runs; seed++)
            {
                var rng = new Random(seed * 104729);
                var s = new PlayerState(500) { party = DemoFactory.CreateParty() };
                var done = new bool[Milestones.Length];
                for (int fight = 1; fight <= MaxFights && done.Any(d => !d); fight++)
                {
                    var enemies = Roll(rng).Select((id, i) => GameDB.Enemy(id).CreateCombatant($" {i}")).ToList();
                    var battle = new BattleSystem(s.party, enemies, seed * 1000 + fight) { Inventory = s };
                    while (battle.State == BattleState.WaitingForCommands && battle.Turn <= 60)
                        battle.SubmitCommands(battle.AlivePlayers.Select(p => BattleSimulator.DecideFor(p, battle, s, rng)).ToList());

                    if (battle.State == BattleState.Victory)
                    {
                        Game.Progression.LevelService.AwardBattleExp(s, battle.TotalExp);
                        s.Gold += battle.TotalGold;
                        income += battle.TotalGold;
                    }
                    else
                    {
                        // 与 GameSession.EndBattle 一致：全灭后金钱减半、回城复活
                        defeats++;
                        s.Gold /= 2;
                        foreach (var p in s.party) p.hp = p.maxHp;
                    }
                    foreach (var p in s.party) if (p.hp <= 0) p.hp = 1;

                    if (NeedTown(s))
                    {
                        townTrips++;
                        upkeep += VisitTown(s);
                    }
                    for (int m = 0; m < Milestones.Length; m++)
                        if (!done[m] && Milestones[m].reached(s)) { done[m] = true; reachedAt[m].Add(fight); }
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("# 第一幕进度模拟");
            sb.AppendLine();
            sb.AppendLine($"> 生成：{DateTime.Now:yyyy-MM-dd HH:mm}　|　{Runs} 次　|　起点：Lv1、500G、初始战车（75 炮 + 机枪）　|　野外遇敌表见 EconomySimulator.FieldTable");
            sb.AppendLine();
            sb.AppendLine("| 里程碑 | 达成率 | 所需战斗场数（10% / 中位 / 90%） |");
            sb.AppendLine("|---|---|---|");
            for (int m = 0; m < Milestones.Length; m++)
            {
                var l = reachedAt[m].OrderBy(x => x).ToList();
                string dist = l.Count == 0 ? "—" : $"{Pct(l, 0.1f)} / {Pct(l, 0.5f)} / {Pct(l, 0.9f)}";
                sb.AppendLine($"| {Milestones[m].name} | {(float)l.Count / Runs:P0} | {dist} |");
            }
            sb.AppendLine();
            int totalFights = reachedAt[Milestones.Length - 1].DefaultIfEmpty(MaxFights).Sum();
            sb.AppendLine($"- 平均每场收入 {(float)income / Math.Max(1, totalFights):F0}G；每次回城开销 {(float)upkeep / Math.Max(1, townTrips):F0}G；每 {(float)totalFights / Math.Max(1, townTrips):F1} 场回城一次");
            sb.AppendLine($"- 全灭 {defeats} 次（{Runs} 次模拟合计）");

            string full = Path.GetFullPath(OutPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, sb.ToString(), new UTF8Encoding(false));
            Debug.Log($"[EconSim] 完成，报告：{full}\n{sb}");
        }

        private static int Price(string partId) => GameDB.Part(partId)?.price ?? int.MaxValue;

        private static int Pct(List<int> sorted, float p) => sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * p))];

        private static string[] Roll(Random rng)
        {
            int roll = rng.Next(FieldTable.Sum(t => t.weight));
            foreach (var (w, m) in FieldTable)
            {
                roll -= w;
                if (roll < 0) return m;
            }
            return FieldTable[0].members;
        }

        /// <summary>HP 或 SP 低于一半，或主炮没弹药时回城</summary>
        private static bool NeedTown(PlayerState s)
        {
            var tank = s.party[0].tank;
            bool lowHp = s.party.Any(p => p.hp < p.maxHp / 2);
            bool lowSp = tank.currentSp < tank.MaxSp / 2;
            bool noAmmo = tank.weapons[0] is { HasAmmo: false };
            return lowHp || lowSp || noAmmo;
        }

        /// <summary>回城：住宿、修理（含补装甲）、补弹；钱不够的项跳过。返回花费</summary>
        private static int VisitTown(PlayerState s)
        {
            int before = s.Gold;
            Game.Town.TownService.Rest(s, PlayerState.DefaultTown);
            var tank = s.party[0].tank;
            if (GarageService.Repair(s, tank, out _) == OpResult.Ok) GarageService.FillArmor(tank);
            GarageService.Refill(s, tank, out _);
            return before - s.Gold;
        }
    }
}
