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
    /// 第二幕（RunAct2）：Lv6、4000G、第一幕毕业战车（轻型 + V12 + 电击炮 + 火焰喷射器）在白盐带外缘战斗，
    /// 盐滩与管线两个遇敌区各半，回盐井聚落补给；结果写入 docs/balance/economy_act2_latest.md。
    /// 第三幕（RunAct3）：Lv11、10000G、第二幕毕业战车（重型 + V12 + 追踪 C + 电击 + 冷冻 + 导弹）在盐盆中心战斗，
    /// 回路灯车队营地补给；结果写入 docs/balance/economy_act3_latest.md。
    /// </summary>
    public static class EconomySimulator
    {
        private const int Runs = 200;
        private const int MaxFights = 200;

        /// <summary>野外遇敌表（与 PrototypeSceneBuilder 的野外遇敌区一致）</summary>
        public static readonly (int weight, string[] members)[] FieldTable =
        {
            (3, new[] { "ENM_Ant", "ENM_Ant" }),
            (2, new[] { "ENM_Dog", "ENM_Dog" }),
            (2, new[] { "ENM_Ant", "ENM_Ant", "ENM_Ant" }),
            (1, new[] { "ENM_Dog", "ENM_Ant" }),
        };

        /// <summary>白盐带外缘遇敌表：盐滩 + 管线（与 PrototypeSceneBuilder.BuildSaltBelt 一致，两区各半）</summary>
        public static readonly (int weight, string[] members)[] SaltBeltTable =
        {
            (3, new[] { "ENM_ScorpionSwarm", "ENM_ScorpionSwarm", "ENM_ScorpionSwarm", "ENM_ScorpionSwarm", "ENM_ScorpionSwarm" }),
            (2, new[] { "ENM_ScrapDrone", "ENM_ScrapDrone" }),
            (2, new[] { "ENM_Raider", "ENM_Raider", "ENM_Raider" }),
            (1, new[] { "ENM_Raider", "ENM_Raider", "ENM_ScrapDrone" }),
            (3, new[] { "ENM_SaltCrawler" }),
            (2, new[] { "ENM_PipeSentry", "ENM_PipeSentry" }),
            (2, new[] { "ENM_SaltCrawler", "ENM_ScorpionSwarm", "ENM_ScorpionSwarm", "ENM_ScorpionSwarm" }),
            (1, new[] { "ENM_PipeSentry", "ENM_Raider", "ENM_Raider" }),
        };

        private static readonly (string name, Func<PlayerState, bool> reached)[] Act2Milestones =
        {
            ("可买追踪 C 装置", s => s.Gold >= Price("TNK_CUnit_Tracker")),
            ("可买冷冻炮", s => s.Gold >= Price("WPN_CryoGun")),
            ("可买音波炮", s => s.Gold >= Price("WPN_SonicBlaster")),
            ("可买 105 炮", s => s.Gold >= Price("WPN_Cannon_105")),
            ("可买重型底盘", s => s.Gold >= Price("TNK_Chassis_Heavy")),
            ("升到 Lv8", s => s.party[0].level >= 8),
            ("升到 Lv10（巨虫推荐）", s => s.party[0].level >= 10),
            ("Lv10 且可买重型底盘 + 冷冻炮", s => s.party[0].level >= 10 && s.Gold >= Price("TNK_Chassis_Heavy") + Price("WPN_CryoGun")),
        };

        /// <summary>盐盆中心遇敌表：风暴盐原 + 管沟（与 PrototypeSceneBuilder.BuildSaltBasin 一致，两区各半）</summary>
        public static readonly (int weight, string[] members)[] SaltBasinTable =
        {
            (3, new[] { "ENM_SandShark", "ENM_SandShark" }),
            (2, new[] { "ENM_StormCaller", "ENM_StormCaller" }),
            (2, new[] { "ENM_Scavenger", "ENM_Scavenger", "ENM_Scavenger", "ENM_Scavenger" }),
            (1, new[] { "ENM_StormCaller", "ENM_Scavenger", "ENM_Scavenger" }),
            (3, new[] { "ENM_GuardBot", "ENM_GuardBot", "ENM_GuardBot" }),
            (2, new[] { "ENM_Juggernaut" }),
            (2, new[] { "ENM_Juggernaut", "ENM_GuardBot", "ENM_GuardBot" }),
            (1, new[] { "ENM_SandShark", "ENM_SandShark", "ENM_StormCaller" }),
        };

        private static readonly (string name, Func<PlayerState, bool> reached)[] Act3Milestones =
        {
            ("可买等离子弧", s => s.Gold >= Price("WPN_PlasmaArc")),
            ("可买涡轮引擎", s => s.Gold >= Price("TNK_Engine_Turbo")),
            ("可买轨道炮", s => s.Gold >= Price("WPN_Railgun")),
            ("可买突击底盘", s => s.Gold >= Price("TNK_Chassis_Assault")),
            ("升到 Lv13", s => s.party[0].level >= 13),
            ("升到 Lv15（闸卫推荐）", s => s.party[0].level >= 15),
            ("Lv15 且可买轨道炮 + 等离子弧", s => s.party[0].level >= 15 && s.Gold >= Price("WPN_Railgun") + Price("WPN_PlasmaArc")),
        };

        /// <summary>一幕的进度模拟配置</summary>
        private class Act
        {
            public string title, outPath, startDesc, town;
            public Func<PlayerState> start;
            public (int weight, string[] members)[] table;
            public (string name, Func<PlayerState, bool> reached)[] milestones;
        }

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
        public static void Run() => RunAct(new Act
        {
            title = "第一幕", outPath = "../docs/balance/economy_latest.md", town = PlayerState.DefaultTown,
            startDesc = "Lv1、500G、初始战车（75 炮 + 机枪）　|　野外遇敌表见 EconomySimulator.FieldTable",
            start = () => new PlayerState(500) { party = DemoFactory.CreateParty() },
            table = FieldTable, milestones = Milestones,
        });

        [MenuItem("Game/第二幕进度模拟（经济）")]
        public static void RunAct2() => RunAct(new Act
        {
            title = "第二幕", outPath = "../docs/balance/economy_act2_latest.md", town = "TWN_Saltwell",
            startDesc = "Lv6、4000G、轻型 + V12 + 电击炮 + 火焰喷射器　|　遇敌表见 EconomySimulator.SaltBeltTable",
            start = Act2Start,
            table = SaltBeltTable, milestones = Act2Milestones,
        });

        [MenuItem("Game/第三幕进度模拟（经济）")]
        public static void RunAct3() => RunAct(new Act
        {
            title = "第三幕", outPath = "../docs/balance/economy_act3_latest.md", town = "TWN_Lampcamp",
            startDesc = "Lv11、10000G、重型 + V12 + 追踪 C + 电击炮 + 冷冻炮 + 导弹　|　遇敌表见 EconomySimulator.SaltBasinTable",
            start = () => StartAt(11, 10000, new[] { "TNK_Chassis_Heavy", "TNK_Engine_V12", "TNK_CUnit_Tracker" },
                new[] { "WPN_ShockCannon", "WPN_CryoGun", "WPN_SE_Missile" }),
            table = SaltBasinTable, milestones = Act3Milestones,
        });

        /// <summary>第二幕起点：队伍升到 Lv6，第一幕毕业战车</summary>
        private static PlayerState Act2Start() =>
            StartAt(6, 4000, new[] { "TNK_Chassis_Light", "TNK_Engine_V12", "TNK_CUnit_Basic" },
                new[] { "WPN_ShockCannon", "WPN_Flamethrower" });

        /// <summary>中途起点：队伍升到指定等级，按底盘/引擎/C 装置与按武器孔顺序的武器组装战车</summary>
        private static PlayerState StartAt(int level, int gold, string[] body, string[] weapons)
        {
            var s = new PlayerState(gold) { party = DemoFactory.CreateParty() };
            foreach (var p in s.party)
            {
                var data = GameDB.Character(p.id);
                for (int lv = 1; lv < level; lv++) Game.Progression.LevelService.GainExp(p, data.ExpToNext(p.level));
                p.hp = p.maxHp;
            }
            var tank = new TankLoadout { tankName = $"Lv{level}" };
            foreach (var id in body)
                tank.TryEquip(new PartInstance(GameDB.Part(id)), 0, out _);
            for (int i = 0; i < weapons.Length; i++)
                if (tank.TryEquip(new PartInstance(GameDB.Part(weapons[i])), i, out _) != OpResult.Ok)
                    throw new Exception($"[EconSim] 无法在第 {i} 孔装 {weapons[i]}");
            tank.FillArmor();
            s.party[0].tank = tank;
            s.party[0].inTank = true;
            return s;
        }

        private static void RunAct(Act act)
        {
            GameDB.Reload();
            var milestones = act.milestones;
            var reachedAt = milestones.Select(_ => new List<int>()).ToArray();
            int defeats = 0, townTrips = 0, totalFights = 0;
            long upkeep = 0, income = 0;

            for (int seed = 1; seed <= Runs; seed++)
            {
                var rng = new Random(seed * 104729);
                var s = act.start();
                var done = new bool[milestones.Length];
                for (int fight = 1; fight <= MaxFights && done.Any(d => !d); fight++)
                {
                    totalFights++;
                    var enemies = Roll(act.table, rng).Select((id, i) => GameDB.Enemy(id).CreateCombatant($" {i}")).ToList();
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

                    // 全灭后在最后城镇复活，与平时回城一样修理补给
                    if (battle.State != BattleState.Victory || NeedTown(s))
                    {
                        townTrips++;
                        upkeep += VisitTown(s, act.town);
                    }
                    for (int m = 0; m < milestones.Length; m++)
                        if (!done[m] && milestones[m].reached(s)) { done[m] = true; reachedAt[m].Add(fight); }
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"# {act.title}进度模拟");
            sb.AppendLine();
            sb.AppendLine($"> 生成：{DateTime.Now:yyyy-MM-dd HH:mm}　|　{Runs} 次　|　起点：{act.startDesc}");
            sb.AppendLine();
            sb.AppendLine("| 里程碑 | 达成率 | 所需战斗场数（10% / 中位 / 90%） |");
            sb.AppendLine("|---|---|---|");
            for (int m = 0; m < milestones.Length; m++)
            {
                var l = reachedAt[m].OrderBy(x => x).ToList();
                string dist = l.Count == 0 ? "—" : $"{Pct(l, 0.1f)} / {Pct(l, 0.5f)} / {Pct(l, 0.9f)}";
                sb.AppendLine($"| {milestones[m].name} | {(float)l.Count / Runs:P0} | {dist} |");
            }
            sb.AppendLine();
            sb.AppendLine($"- 平均每场收入 {(float)income / Math.Max(1, totalFights):F0}G；每次回城开销 {(float)upkeep / Math.Max(1, townTrips):F0}G；每 {(float)totalFights / Math.Max(1, townTrips):F1} 场回城一次");
            sb.AppendLine($"- 全灭 {defeats} 次，共 {totalFights} 场（{Runs} 次模拟合计，全灭率 {(float)defeats / Math.Max(1, totalFights):P1}）");

            string full = Path.GetFullPath(act.outPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, sb.ToString(), new UTF8Encoding(false));
            Debug.Log($"[EconSim] 完成，报告：{full}\n{sb}");
        }

        private static int Price(string partId) => GameDB.Part(partId)?.price ?? int.MaxValue;

        private static int Pct(List<int> sorted, float p) => sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * p))];

        private static string[] Roll((int weight, string[] members)[] table, Random rng)
        {
            int roll = rng.Next(table.Sum(t => t.weight));
            foreach (var (w, m) in table)
            {
                roll -= w;
                if (roll < 0) return m;
            }
            return table[0].members;
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
        private static int VisitTown(PlayerState s, string town)
        {
            int before = s.Gold;
            Game.Town.TownService.Rest(s, town);
            var tank = s.party[0].tank;
            if (GarageService.Repair(s, tank, out _) == OpResult.Ok) GarageService.FillArmor(tank);
            GarageService.Refill(s, tank, out _);
            return before - s.Gold;
        }
    }
}
