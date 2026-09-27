using System;
using System.Collections.Generic;
using System.Linq;
using Game.Battle;
using Game.Core;
using Game.Field;
using Game.Items;
using Game.Story;
using Game.Town;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>
    /// 第三幕冒烟测试：
    /// 1) 数据：新敌人技能、路灯车队营地商店与城镇配置有效；结局效果引用的部件与道具存在。
    /// 2) 场景：盐盆与主控站的遇敌组、传送口出生点、两个赏金首都在主控站。
    /// 3) 流程（纯逻辑）：第二幕完成 → 盐盆开放 → 开场 → 进营地 → 主控站开放 → 恢复供能 → 疏浚机 → 闸卫七号 →
    ///    三种结局分别完成任务并给出各自回报；抉择触发器在选择前可重复进入、选择后不再触发。
    /// </summary>
    public static class Act3SmokeTest
    {
        private static readonly string[] NewEnemies =
        {
            "ENM_SandShark", "ENM_GuardBot", "ENM_Juggernaut", "ENM_StormCaller", "ENM_Scavenger",
            "ENM_Bounty_Dredger", "ENM_Bounty_GateWarden",
        };

        private static readonly string[] NewParts =
            { "TNK_Chassis_Assault", "TNK_Engine_Turbo", "WPN_Railgun", "WPN_PlasmaArc" };

        [MenuItem("Game/第三幕冒烟测试")]
        public static void Run()
        {
            GameDB.Reload();

            // 数据
            foreach (var id in NewEnemies)
            {
                var e = GameDB.Enemy(id);
                Check(e != null, $"敌人 {id} 存在");
                Check(e.skills.All(k => GameDB.Skill(k.skillId) != null), $"{id} 的技能都存在");
            }
            var town = GameDB.Town("TWN_Lampcamp");
            Check(town != null && town.fieldScene == GameSession.SaltBasinSceneName, "路灯车队营地位于盐盆中心");
            var shop = GameDB.Shop(town.shopId);
            Check(shop != null, "营地商店存在");
            foreach (var id in NewParts)
                Check(shop.goods.Any(g => g != null && g.partId == id), $"营地出售 {id}");
            Check(GameDB.Part("TNK_CUnit_Twin") != null, "结局奖励的双核 C 装置存在");

            // 抉择对话（Codex 编写）已存在时，三个结局效果都要出现在对话节点上
            var choice = GameDB.Dialogue("DLG_Act3_Choice");
            if (choice != null)
                foreach (var fx in Endings.All)
                    Check(choice.nodes.Any(n => n.effects == fx), $"抉择对话含结局效果 {fx}");
            else Debug.LogWarning("[Act3Test] 抉择对话 DLG_Act3_Choice 尚未提供，跳过对话检查");

            // 场景
            var spawns = new Dictionary<string, List<string>>();
            var portals = new List<(string scene, string target, string spawn, string condition)>();
            var triggers = new Dictionary<string, Trig>();
            var bountyScenes = new Dictionary<string, string>();
            foreach (var name in PrototypeSceneBuilder.LogicScenes)
            {
                EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath(name), OpenSceneMode.Single);
                spawns[name] = Find<SpawnPoint>().Select(s => s.spawnId).ToList();
                portals.AddRange(Find<ScenePortal>().Select(p => (name, p.targetScene, p.targetSpawnId, p.condition)));
                foreach (var t in Find<StoryTrigger>()) triggers[t.triggerId] = new Trig(t.triggerId, t.condition, t.effects, t.once);
                foreach (var b in Find<BountyZone>().Where(b => b.bounty != null)) bountyScenes[b.bounty.enemyId] = name;
                foreach (var z in Find<EncounterZone>())
                    Check(z.groups.All(g => g.members.All(m => m != null)), $"{name} 的 {z.name} 遇敌组无空成员");
                if (name == GameSession.SaltBasinSceneName)
                    Check(Find<TownGate>().Any(g => g.townId == "TWN_Lampcamp"), "盐盆有营地入口");
            }
            foreach (var (scene, target, spawn, _) in portals)
                Check(spawns.TryGetValue(target, out var list) && list.Contains(spawn), $"{scene} 的传送口指向 {target}/{spawn} 存在");
            Check(bountyScenes.GetValueOrDefault("ENM_Bounty_Dredger") == GameSession.ControlStationSceneName
                  && bountyScenes.GetValueOrDefault("ENM_Bounty_GateWarden") == GameSession.ControlStationSceneName, "两个赏金首都在主控站");
            Check(!triggers["act3_choice"].once, "抉择触发器可重复进入");

            // 流程：三种结局各走一遍
            var toBasin = portals.First(p => p.target == GameSession.SaltBasinSceneName && p.scene == GameSession.SaltBeltSceneName);
            var toStation = portals.First(p => p.target == GameSession.ControlStationSceneName);
            foreach (var ending in Endings.All)
            {
                StoryService.AbortDialogue();
                var s = new PlayerState(0) { party = DemoFactory.CreateParty() };
                Check(!Conditions.Evaluate(toBasin.condition, s), "第二幕未完成时盐盆未开放");
                StoryService.SetFlag(s, "act2_done", true);
                Check(Conditions.Evaluate(toBasin.condition, s), "第二幕完成后盐盆开放");

                Fire(triggers["act3_intro"], s);
                Check(StoryService.Quest(s, "QST_Act3_Floodgate")?.state == QuestState.Active, "第三幕任务开始");
                Check(!Conditions.Evaluate(toStation.condition, s), "进营地前主控站未开放");
                Fire(triggers["act3_lampcamp"], s);
                Check(Conditions.Evaluate(toStation.condition, s), "进营地后主控站开放");

                Fire(triggers["act3_power"], s);
                Check(StoryService.Quest(s, "QST_Act3_Floodgate").step == 3, "恢复供能后等待疏浚机");
                BountyService.OnVictory(s, new List<Combatant> { GameDB.Enemy("ENM_Bounty_Dredger").CreateCombatant() });
                Check(s.flags.Contains("act3_filters_restored"), "击败疏浚机后滤芯恢复");
                Check(!Conditions.Evaluate(triggers["act3_choice"].condition, s), "击败闸卫前不能抉择");
                BountyService.OnVictory(s, new List<Combatant> { GameDB.Enemy("ENM_Bounty_GateWarden").CreateCombatant() });
                Check(Conditions.Evaluate(triggers["act3_choice"].condition, s), "击败闸卫后可抉择");

                int gold = s.Gold;
                Effects.Apply(ending, s);
                Check(StoryService.Quest(s, "QST_Act3_Floodgate")?.state == QuestState.Completed && s.flags.Contains("act3_done"), $"结局 {ending} 完成第三幕");
                Check(s.Gold > gold, "结局给出金钱回报");
                Check(!Conditions.Evaluate(triggers["act3_choice"].condition, s), "抉择后不再触发");
                if (ending == Endings.Split)
                    Check(Conditions.Evaluate("has:TNK_CUnit_Twin", s), "拆分结局获得双核 C 装置");
                if (ending == Endings.Open)
                    Check(ItemService.Count(s, "ITM_ReviveKit") == 5, "公开结局获得补给");
            }

            Debug.Log("[Act3Test] 全部通过");
        }

        /// <summary>触发器配置的副本（场景切换后组件会被销毁）</summary>
        private class Trig
        {
            public readonly string triggerId, condition, effects;
            public readonly bool once;

            public Trig(string triggerId, string condition, string effects, bool once)
            {
                this.triggerId = triggerId;
                this.condition = condition;
                this.effects = effects;
                this.once = once;
            }
        }

        /// <summary>按触发器配置执行（条件成立时应用效果）</summary>
        private static void Fire(Trig t, PlayerState s)
        {
            Check(Conditions.Evaluate(t.condition, s), $"触发器 {t.triggerId} 可触发");
            Effects.Apply(t.effects, s);
        }

        private static IEnumerable<T> Find<T>() where T : Object =>
            Object.FindObjectsByType<T>(FindObjectsInactive.Include);

        private static void Check(bool ok, string what)
        {
            if (!ok) throw new Exception($"[Act3Test] 失败：{what}");
        }
    }
}
