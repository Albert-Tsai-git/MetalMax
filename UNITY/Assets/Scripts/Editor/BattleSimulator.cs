using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Battle;
using Game.Core;
using Game.Items;
using Game.Progression;
using Game.Tank;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace Game.EditorTools
{
    /// <summary>
    /// 批量战斗模拟：多种武器配置 × 多种战斗场景，输出胜率、回合、损耗、弹药与修理费用的收益表，
    /// 用于检查“是否存在全面压制的配置”“每类武器是否有自己的最优场景”。
    /// 第一幕结果写入 docs/balance/sim_latest.md，第二、三幕写入 docs/balance/sim_act{2,3}_latest.md。
    /// </summary>
    public static class BattleSimulator
    {
        private const int Runs = 200;
        private const int MaxTurns = 60;

        /// <summary>武器配置：底盘、引擎与按武器孔顺序的武器</summary>
        private class Loadout
        {
            public string name, chassis, engine, cunit = "TNK_CUnit_Basic";
            public string[] weapons;
        }

        /// <summary>
        /// 战斗场景：单场（enemies）或一整趟迷宫（run = true：按泵站遇敌表随机 runEncounters 场，途中开宝箱，
        /// 最后打 enemies；全程不回城补给，HP/SP/弹药/道具延续）。
        /// </summary>
        private class Scenario
        {
            public string name;
            public string[] enemies;
            public int level;
            public bool run;
            public int runEncounters;
        }

        /// <summary>一组模拟：配置 × 场景，以及“一趟迷宫”使用的遇敌表、宝箱和报告路径</summary>
        private class Suite
        {
            public string title, outPath, chests;
            public (int weight, string[] members)[] table;
            public Loadout[] loadouts;
            public Scenario[] scenarios;
        }

        /// <summary>泵站遇敌表（与 PrototypeSceneBuilder 一致：权重, 成员）</summary>
        private static readonly (int weight, string[] members)[] PumpTable =
        {
            (3, new[] { "ENM_Ant", "ENM_Ant", "ENM_Ant", "ENM_Ant" }),
            (2, new[] { "ENM_TurretBug" }),
            (2, new[] { "ENM_Dog", "ENM_Dog", "ENM_Ant", "ENM_Ant" }),
            (1, new[] { "ENM_TurretBug", "ENM_Ant", "ENM_Ant", "ENM_Ant" }),
        };

        /// <summary>泵站宝箱内容（与场景一致），在一趟中途依次获得</summary>
        private const string PumpChests = "gold:+300; item:ITM_RepairPack:2; item:ITM_ReviveKit:1; item:ITM_Tonic:3";

        /// <summary>出发前自带的道具</summary>
        private const string StartItems = "item:ITM_Tonic:3";

        private class Result
        {
            public int wins, turns, hpLost, spLost, ammoCost, repairCost, reward;
            /// <summary>失败原因：超过回合上限 / 全灭；以及失败时最后一场敌人剩余 HP 比例之和</summary>
            public int timeouts, wipes;
            public float foeHpLeft;
            public float WinRate => (float)wins / Runs;
            public float AvgTurns => (float)turns / Runs;
            /// <summary>净收益 = 战利品 - 弹药费 - 修理费（每场平均）</summary>
            public float Net => (float)(reward - ammoCost - repairCost) / Runs;
        }

        private static readonly Loadout[] Loadouts =
        {
            new() { name = "实弹标准（75炮+机枪）", chassis = "TNK_Chassis_Light", engine = "TNK_Engine_V8", weapons = new[] { "WPN_Cannon_75", "WPN_MG_77" } },
            new() { name = "火焰（75炮+火焰喷射）", chassis = "TNK_Chassis_Light", engine = "TNK_Engine_V8", weapons = new[] { "WPN_Cannon_75", "WPN_Flamethrower" } },
            new() { name = "电击（电击炮+机枪）", chassis = "TNK_Chassis_Light", engine = "TNK_Engine_V12", weapons = new[] { "WPN_ShockCannon", "WPN_MG_77" } },
            new() { name = "导弹（重型+75炮+机枪+导弹）", chassis = "TNK_Chassis_Heavy", engine = "TNK_Engine_V12", weapons = new[] { "WPN_Cannon_75", "WPN_MG_77", "WPN_SE_Missile" } },
            new() { name = "全能（重型+电击+火焰+导弹）", chassis = "TNK_Chassis_Heavy", engine = "TNK_Engine_V12", weapons = new[] { "WPN_ShockCannon", "WPN_Flamethrower", "WPN_SE_Missile" } },
        };

        private static readonly Scenario[] Scenarios =
        {
            new() { name = "蚁群×4", enemies = new[] { "ENM_Ant", "ENM_Ant", "ENM_Ant", "ENM_Ant" }, level = 3 },
            new() { name = "炮台虫×1", enemies = new[] { "ENM_TurretBug" }, level = 3 },
            new() { name = "野狗×2+蚁×2", enemies = new[] { "ENM_Dog", "ENM_Dog", "ENM_Ant", "ENM_Ant" }, level = 3 },
            new() { name = "炮台虫+蚁×3", enemies = new[] { "ENM_TurretBug", "ENM_Ant", "ENM_Ant", "ENM_Ant" }, level = 3 },
            new() { name = "铁钳巨蟹", enemies = new[] { "ENM_Bounty_IronCrab" }, level = 5 },
            new() { name = "泵站一趟（3 场+巨蟹）", enemies = new[] { "ENM_Bounty_IronCrab" }, level = 5, run = true, runEncounters = 3 },
        };

        #region 第二幕

        /// <summary>空城遗址遇敌表（与 PrototypeSceneBuilder.BuildGhostCity 一致）</summary>
        private static readonly (int weight, string[] members)[] GhostCityTable =
        {
            (3, new[] { "ENM_PipeSentry", "ENM_ScrapDrone", "ENM_ScrapDrone" }),
            (2, new[] { "ENM_SaltCrawler", "ENM_PipeSentry" }),
            (2, new[] { "ENM_Raider", "ENM_Raider", "ENM_Raider", "ENM_Raider" }),
            (1, new[] { "ENM_SaltCrawler", "ENM_ScorpionSwarm", "ENM_ScorpionSwarm", "ENM_ScorpionSwarm", "ENM_ScorpionSwarm" }),
        };

        private const string GhostCityChests = "gold:+800; item:ITM_RepairPack:3; item:ITM_AmmoCrate:1; item:ITM_ReviveKit:2; item:ITM_Tonic:5";

        /// <summary>第二幕配置：第一幕毕业配置 + 各新装备的代表配置（每件新装备至少出现一次）</summary>
        private static readonly Loadout[] Act2Loadouts =
        {
            new() { name = "第一幕毕业（轻型+电击+火焰）", chassis = "TNK_Chassis_Light", engine = "TNK_Engine_V12", weapons = new[] { "WPN_ShockCannon", "WPN_Flamethrower" } },
            new() { name = "重炮（重型+105炮+机枪+导弹）", chassis = "TNK_Chassis_Heavy", engine = "TNK_Engine_V12", weapons = new[] { "WPN_Cannon_105", "WPN_MG_77", "WPN_SE_Missile" } },
            new() { name = "追踪导弹（重型+75炮+火焰+导弹+追踪C）", chassis = "TNK_Chassis_Heavy", engine = "TNK_Engine_V12", cunit = "TNK_CUnit_Tracker", weapons = new[] { "WPN_Cannon_75", "WPN_Flamethrower", "WPN_SE_Missile" } },
            new() { name = "音波（重型+电击+火焰+音波）", chassis = "TNK_Chassis_Heavy", engine = "TNK_Engine_V12", weapons = new[] { "WPN_ShockCannon", "WPN_Flamethrower", "WPN_SonicBlaster" } },
            new() { name = "冷冻（重型+电击+冷冻+导弹）", chassis = "TNK_Chassis_Heavy", engine = "TNK_Engine_V12", weapons = new[] { "WPN_ShockCannon", "WPN_CryoGun", "WPN_SE_Missile" } },
        };

        private static readonly Scenario[] Act2Scenarios =
        {
            new() { name = "蝎群×5", enemies = new[] { "ENM_ScorpionSwarm", "ENM_ScorpionSwarm", "ENM_ScorpionSwarm", "ENM_ScorpionSwarm", "ENM_ScorpionSwarm" }, level = 7 },
            new() { name = "无人机×2", enemies = new[] { "ENM_ScrapDrone", "ENM_ScrapDrone" }, level = 7 },
            new() { name = "劫匪×3", enemies = new[] { "ENM_Raider", "ENM_Raider", "ENM_Raider" }, level = 7 },
            new() { name = "盐壳爬行者", enemies = new[] { "ENM_SaltCrawler" }, level = 7 },
            new() { name = "管线哨兵×2", enemies = new[] { "ENM_PipeSentry", "ENM_PipeSentry" }, level = 7 },
            new() { name = "盐沙巨虫", enemies = new[] { "ENM_Bounty_SaltWyrm" }, level = 10 },
            new() { name = "空城一趟（4 场+巨虫）", enemies = new[] { "ENM_Bounty_SaltWyrm" }, level = 10, run = true, runEncounters = 4 },
        };

        #endregion

        #region 第三幕

        /// <summary>主控站遇敌表（与 PrototypeSceneBuilder.BuildControlStation 一致）</summary>
        private static readonly (int weight, string[] members)[] ControlStationTable =
        {
            (3, new[] { "ENM_GuardBot", "ENM_GuardBot", "ENM_GuardBot", "ENM_StormCaller" }),
            (2, new[] { "ENM_Juggernaut", "ENM_GuardBot" }),
            (2, new[] { "ENM_Scavenger", "ENM_Scavenger", "ENM_Scavenger", "ENM_SandShark" }),
            (1, new[] { "ENM_Juggernaut", "ENM_StormCaller", "ENM_StormCaller" }),
        };

        private const string ControlStationChests = "gold:+2000; item:ITM_RepairPack:4; item:ITM_AmmoCrate:2; item:ITM_ReviveKit:3; item:ITM_Tonic:6";

        /// <summary>第三幕配置：第二幕毕业配置 + 各新装备的代表配置</summary>
        private static readonly Loadout[] Act3Loadouts =
        {
            new() { name = "第二幕毕业（重型+电击+冷冻+导弹）", chassis = "TNK_Chassis_Heavy", engine = "TNK_Engine_V12", cunit = "TNK_CUnit_Tracker", weapons = new[] { "WPN_ShockCannon", "WPN_CryoGun", "WPN_SE_Missile" } },
            new() { name = "轨道炮（重型+轨道炮+机枪+音波）", chassis = "TNK_Chassis_Heavy", engine = "TNK_Engine_V12", weapons = new[] { "WPN_Railgun", "WPN_MG_77", "WPN_SonicBlaster" } },
            new() { name = "等离子（重型+105炮+等离子+导弹+追踪C）", chassis = "TNK_Chassis_Heavy", engine = "TNK_Engine_V12", cunit = "TNK_CUnit_Tracker", weapons = new[] { "WPN_Cannon_105", "WPN_PlasmaArc", "WPN_SE_Missile" } },
            new() { name = "突击（突击底盘+涡轮+轨道炮+等离子+音波）", chassis = "TNK_Chassis_Assault", engine = "TNK_Engine_Turbo", weapons = new[] { "WPN_Railgun", "WPN_PlasmaArc", "WPN_SonicBlaster" } },
            new() { name = "音波（重型+105炮+火焰+音波）", chassis = "TNK_Chassis_Heavy", engine = "TNK_Engine_V12", weapons = new[] { "WPN_Cannon_105", "WPN_Flamethrower", "WPN_SonicBlaster" } },
        };

        private static readonly Scenario[] Act3Scenarios =
        {
            new() { name = "沙鲨×2", enemies = new[] { "ENM_SandShark", "ENM_SandShark" }, level = 13 },
            new() { name = "警卫机×3", enemies = new[] { "ENM_GuardBot", "ENM_GuardBot", "ENM_GuardBot" }, level = 13 },
            new() { name = "重装机", enemies = new[] { "ENM_Juggernaut" }, level = 13 },
            new() { name = "风暴术士×2", enemies = new[] { "ENM_StormCaller", "ENM_StormCaller" }, level = 13 },
            new() { name = "拾荒者×4", enemies = new[] { "ENM_Scavenger", "ENM_Scavenger", "ENM_Scavenger", "ENM_Scavenger" }, level = 13 },
            new() { name = "疏浚机", enemies = new[] { "ENM_Bounty_Dredger" }, level = 14 },
            new() { name = "闸卫七号", enemies = new[] { "ENM_Bounty_GateWarden" }, level = 15 },
            new() { name = "主控站一趟（4 场+闸卫）", enemies = new[] { "ENM_Bounty_GateWarden" }, level = 15, run = true, runEncounters = 4 },
        };

        #endregion

        private static readonly Suite Act1 = new()
        {
            title = "第一幕", outPath = "../docs/balance/sim_latest.md", chests = PumpChests,
            table = PumpTable, loadouts = Loadouts, scenarios = Scenarios,
        };

        private static readonly Suite Act2 = new()
        {
            title = "第二幕", outPath = "../docs/balance/sim_act2_latest.md", chests = GhostCityChests,
            table = GhostCityTable, loadouts = Act2Loadouts, scenarios = Act2Scenarios,
        };

        private static readonly Suite Act3 = new()
        {
            title = "第三幕", outPath = "../docs/balance/sim_act3_latest.md", chests = ControlStationChests,
            table = ControlStationTable, loadouts = Act3Loadouts, scenarios = Act3Scenarios,
        };

        [MenuItem("Game/批量战斗模拟（平衡）")]
        public static void Run() => RunSuite(Act1);

        [MenuItem("Game/第二幕批量战斗模拟（平衡）")]
        public static void RunAct2() => RunSuite(Act2);

        [MenuItem("Game/第三幕批量战斗模拟（平衡）")]
        public static void RunAct3() => RunSuite(Act3);

        private static void RunSuite(Suite su)
        {
            GameDB.Reload();
            var table = new Result[su.loadouts.Length, su.scenarios.Length];
            for (int l = 0; l < su.loadouts.Length; l++)
            for (int c = 0; c < su.scenarios.Length; c++)
                table[l, c] = Simulate(su, su.loadouts[l], su.scenarios[c]);

            string report = Report(su, table);
            string full = Path.GetFullPath(su.outPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, report, new UTF8Encoding(false));
            Debug.Log($"[Sim] 模拟完成，报告：{full}\n{report}");
        }

        private static Result Simulate(Suite su, Loadout lo, Scenario sc)
        {
            var r = new Result();
            for (int seed = 1; seed <= Runs; seed++)
            {
                var state = new PlayerState(0) { party = BuildParty(lo, sc.level) };
                Game.Story.Effects.Apply(StartItems, state);
                var tank = state.party[0].tank;
                int hp0 = state.party.Sum(p => p.hp), sp0 = tank.currentSp;
                var rng = new Random(seed * 7919);

                var fights = new List<string[]>();
                if (sc.run)
                    for (int i = 0; i < sc.runEncounters; i++) fights.Add(Roll(su.table, rng));
                fights.Add(sc.enemies);

                bool alive = true;
                int turns = 0, itemsBefore = ItemValue(state);
                for (int f = 0; f < fights.Count && alive; f++)
                {
                    if (sc.run && f == fights.Count / 2) Game.Story.Effects.Apply(su.chests, state); // 中途开宝箱
                    var enemies = fights[f].Select((id, i) => GameDB.Enemy(id).CreateCombatant($" {i}")).ToList();
                    var battle = new BattleSystem(state.party, enemies, seed * 31 + f) { Inventory = state };
                    while (battle.State == BattleState.WaitingForCommands && battle.Turn <= MaxTurns)
                        battle.SubmitCommands(battle.AlivePlayers.Select(p => Decide(p, battle, state, rng)).ToList());
                    turns += battle.Turn;
                    if (battle.State == BattleState.Victory) state.Gold += battle.TotalGold;
                    else
                    {
                        alive = false;
                        if (battle.State == BattleState.WaitingForCommands) r.timeouts++; else r.wipes++;
                        r.foeHpLeft += (float)enemies.Sum(e => Math.Max(0, e.hp)) / Math.Max(1, enemies.Sum(e => e.maxHp));
                    }
                    // 战后倒下的队员保留 1 HP（与游戏一致）
                    foreach (var p in state.party) if (p.hp <= 0) p.hp = 1;
                }

                if (alive) r.wins++;
                r.turns += turns;
                r.hpLost += hp0 - state.party.Sum(p => Math.Max(0, p.hp));
                r.spLost += sp0 - tank.currentSp;
                r.ammoCost += tank.RefillCost();
                r.repairCost += tank.RepairCost() + Math.Max(0, itemsBefore - ItemValue(state)) + (alive ? 0 : 0);
                r.reward += state.Gold;
            }
            return r;
        }

        /// <summary>按权重抽一组遇敌</summary>
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

        /// <summary>持有道具的总价值（用于把消耗的道具计入成本）</summary>
        private static int ItemValue(PlayerState s) => s.items.Sum(i => (GameDB.Item(i.id)?.price ?? 0) * i.count);

        /// <summary>按配置组装战车，队伍升到指定等级</summary>
        private static List<Combatant> BuildParty(Loadout lo, int level)
        {
            var party = DemoFactory.CreateParty();
            foreach (var p in party)
            {
                var data = GameDB.Character(p.id);
                for (int lv = 1; lv < level; lv++) LevelService.GainExp(p, data.ExpToNext(p.level));
                p.hp = p.maxHp;
            }
            var tank = new TankLoadout { tankName = lo.name };
            tank.TryEquip(new PartInstance(GameDB.Part(lo.chassis)), 0, out _);
            tank.TryEquip(new PartInstance(GameDB.Part(lo.engine)), 0, out _);
            tank.TryEquip(new PartInstance(GameDB.Part(lo.cunit)), 0, out _);
            for (int i = 0; i < lo.weapons.Length; i++)
                if (tank.TryEquip(new PartInstance(GameDB.Part(lo.weapons[i])), i, out _) != OpResult.Ok)
                    throw new Exception($"[Sim] 配置 {lo.name} 无法在第 {i} 孔装 {lo.weapons[i]}");
            tank.FillArmor();
            party[0].tank = tank;
            party[0].inTank = true;
            return party;
        }

        /// <summary>
        /// 玩家策略（贪心）：乘车时在各武器与各目标中选“期望伤害 × 属性倍率 × 命中率 − 弹药价值”最高者；
        /// 步行时攻击预计能造成最多伤害的敌人。弹药价值按每发单价折算，让玩家在副炮足够时节省炮弹。
        /// </summary>
        /// <summary>供其他模拟器复用的玩家策略</summary>
        public static BattleAction DecideFor(Combatant p, BattleSystem b, PlayerState s, Random rng) => Decide(p, b, s, rng);

        private static BattleAction Decide(Combatant p, BattleSystem b, PlayerState s, Random rng)
        {
            var foes = b.AliveEnemies.ToList();
            // 支援优先：复活倒下的队友 → 救濒危队友 → 修理 SP 过半受损的战车 → 修理包
            var down = b.players.FirstOrDefault(x => !x.IsAlive);
            if (down != null && ItemService.CanUse(s, "ITM_ReviveKit", down) == OpResult.Ok)
                return BattleAction.Item(p, "ITM_ReviveKit", down);
            var hurt = b.AlivePlayers.FirstOrDefault(x => x.hp < x.maxHp * 0.35f);
            if (hurt != null && ItemService.CanUse(s, "ITM_Tonic", hurt) == OpResult.Ok)
                return BattleAction.Item(p, "ITM_Tonic", hurt);
            var damagedTank = b.players.FirstOrDefault(x => x.tank != null && !x.tank.IsDestroyed && x.tank.currentSp < x.tank.MaxSp * 0.5f);
            int repair = GameDB.Character(p.id)?.RepairAmount(p.level) ?? 0;
            if (damagedTank != null && repair >= 40)
                return BattleAction.Repair(p, damagedTank);
            if (damagedTank != null && damagedTank.tank.currentSp < damagedTank.tank.MaxSp * 0.25f
                && ItemService.CanUse(s, "ITM_RepairPack", damagedTank) == OpResult.Ok)
                return BattleAction.Item(p, "ITM_RepairPack", damagedTank);

            if (!p.IsTankActive)
            {
                var t = foes.OrderByDescending(f => Expected(p.attack, f, Element.Normal, DamageCalculator.HumanBaseAccuracy)).First();
                return BattleAction.Attack(p, t);
            }

            BattleAction best = null;
            float bestScore = float.MinValue;
            foreach (var w in p.tank.weapons.Where(w => w != null && w.IsFunctional && w.HasAmmo))
            {
                var wd = (WeaponData)w.data;
                int acc = DamageCalculator.WeaponAccuracy(p, w);
                foreach (var target in foes)
                {
                    var hit = wd.range switch
                    {
                        AttackRange.Single => new List<Combatant> { target },
                        AttackRange.Group => foes.Where(f => f.groupIndex == target.groupIndex).ToList(),
                        _ => foes,
                    };
                    float score = hit.Sum(h => Expected(w.Attack, h, wd.element, acc)) - (wd.maxAmmo >= 0 ? wd.ammoPrice * 0.2f : 0f);
                    if (score > bestScore) { bestScore = score; best = BattleAction.Fire(p, w, target); }
                    if (wd.range == AttackRange.All) break;
                }
            }
            return best ?? BattleAction.Attack(p, foes[rng.Next(foes.Count)]);
        }

        /// <summary>期望伤害（不超过目标剩余 HP）</summary>
        private static float Expected(int attack, Combatant target, Element element, int accuracy)
        {
            float rate = target.ElementRate(element);
            float dmg = Math.Max(rate > 0 ? 1 : 0, (attack - target.TotalDefense / 2f) * rate);
            float hit = Mathf.Clamp(accuracy - target.TotalEvade, 5, 99) / 100f;
            return Math.Min(dmg, target.hp) * hit;
        }

        private static string Report(Suite su, Result[,] t)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# 批量战斗模拟结果（{su.title}）");
            sb.AppendLine();
            sb.AppendLine($"> 生成：{DateTime.Now:yyyy-MM-dd HH:mm}　|　每格 {Runs} 场　|　队伍：猎人（乘车）+ 机械师（步行），场景指定等级　|　玩家策略：贪心期望伤害；机械师优先复活/救人/修理；自带回复药 ×3");
            sb.AppendLine();
            void Table(string title, Func<Result, string> cell, Func<Result, float> score, bool higherBetter)
            {
                sb.AppendLine($"## {title}");
                sb.AppendLine();
                sb.AppendLine("| 配置 | " + string.Join(" | ", su.scenarios.Select(s => $"{s.name}(Lv{s.level})")) + " |");
                sb.AppendLine("|---|" + string.Concat(Enumerable.Repeat("---|", su.scenarios.Length)));
                for (int l = 0; l < su.loadouts.Length; l++)
                {
                    sb.Append($"| {su.loadouts[l].name} |");
                    for (int c = 0; c < su.scenarios.Length; c++)
                    {
                        var col = Enumerable.Range(0, su.loadouts.Length).Select(i => score(t[i, c])).ToList();
                        float bestVal = higherBetter ? col.Max() : col.Min();
                        bool isBest = Math.Abs(score(t[l, c]) - bestVal) < 0.001f;
                        sb.Append(isBest ? $" **{cell(t[l, c])}** |" : $" {cell(t[l, c])} |");
                    }
                    sb.AppendLine();
                }
                sb.AppendLine();
            }
            Table("胜率", r => $"{r.WinRate:P0}", r => r.WinRate, true);
            Table("失败原因（超时/全灭，失败时敌方剩余 HP）", r => r.wins == Runs ? "—" : $"{r.timeouts}/{r.wipes} {r.foeHpLeft / Math.Max(1, r.timeouts + r.wipes):P0}", r => r.WinRate, true);
            Table("平均回合数（越少越好）", r => $"{r.AvgTurns:F1}", r => r.AvgTurns, false);
            Table("平均 HP 损失（越少越好）", r => $"{(float)r.hpLost / Runs:F0}", r => (float)r.hpLost / Runs, false);
            Table("平均 SP 损失（越少越好）", r => $"{(float)r.spLost / Runs:F0}", r => (float)r.spLost / Runs, false);
            Table("平均弹药费（G）", r => $"{(float)r.ammoCost / Runs:F0}", r => (float)r.ammoCost / Runs, false);
            Table("平均修理与道具消耗（G）", r => $"{(float)r.repairCost / Runs:F0}", r => (float)r.repairCost / Runs, false);
            Table("平均净收益 = 收入 − 弹药 − 修理 − 道具（G，越高越好）", r => $"{r.Net:F0}", r => r.Net, true);

            // 压制检查：以“净收益”和“回合数”两项衡量，某配置在所有场景都不劣于另一配置则后者被压制
            sb.AppendLine("## 压制检查");
            sb.AppendLine();
            bool any = false;
            for (int a = 0; a < su.loadouts.Length; a++)
            for (int d = 0; d < su.loadouts.Length; d++)
            {
                if (a == d) continue;
                bool dominates = Enumerable.Range(0, su.scenarios.Length).All(c =>
                    t[a, c].WinRate >= t[d, c].WinRate && t[a, c].Net >= t[d, c].Net && t[a, c].AvgTurns <= t[d, c].AvgTurns);
                if (!dominates) continue;
                any = true;
                sb.AppendLine($"- ⚠ **{su.loadouts[a].name}** 在所有场景的胜率、净收益、回合数都不劣于 **{su.loadouts[d].name}**");
            }
            if (!any) sb.AppendLine("- 没有配置被全面压制");
            sb.AppendLine();
            sb.AppendLine("## 各场景最优配置（按净收益，胜率不足 90% 的不计）");
            sb.AppendLine();
            for (int c = 0; c < su.scenarios.Length; c++)
            {
                var ok = Enumerable.Range(0, su.loadouts.Length).Where(l => t[l, c].WinRate >= 0.9f).ToList();
                string best = ok.Count == 0 ? "无（所有配置胜率都低于 90%）" : su.loadouts[ok.OrderByDescending(l => t[l, c].Net).First()].name;
                sb.AppendLine($"- {su.scenarios[c].name}：{best}");
            }
            return sb.ToString();
        }
    }
}
