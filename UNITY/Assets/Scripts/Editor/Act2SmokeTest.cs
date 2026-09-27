using System;
using System.Collections.Generic;
using System.Linq;
using Game.Battle;
using Game.Core;
using Game.Field;
using Game.Town;
using Game.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>
    /// 第二幕冒烟测试：
    /// 1) 数据：新敌人技能、盐井聚落商店与城镇配置有效；每件第二幕新装备在商店中；每种新敌人至少有一种属性弱点或为特定武器设计的属性。
    /// 2) 场景：白盐带与空城的遇敌组、传送口条件、赏金首位置。
    /// 3) 流程（纯逻辑）：第一幕完成 → 白盐带开放 → 开场 → 进镇 → 空城开放 → 击败巨虫 → 校准点 → 第二幕完成；全灭回到盐井聚落所在场景。
    /// </summary>
    public static class Act2SmokeTest
    {
        private static readonly string[] NewEnemies =
            { "ENM_ScorpionSwarm", "ENM_ScrapDrone", "ENM_Raider", "ENM_SaltCrawler", "ENM_PipeSentry", "ENM_Bounty_SaltWyrm" };

        private static readonly string[] NewParts =
            { "TNK_CUnit_Tracker", "WPN_Cannon_105", "WPN_CryoGun", "WPN_SonicBlaster" };

        [MenuItem("Game/第二幕冒烟测试")]
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
            var wyrm = GameDB.Enemy("ENM_Bounty_SaltWyrm");
            Check(wyrm.isBounty && wyrm.bounty > 0, "盐沙巨虫是赏金首");
            var town = GameDB.Town("TWN_Saltwell");
            Check(town != null && town.fieldScene == GameSession.SaltBeltSceneName, "盐井聚落位于白盐带");
            Check(GameSession.TownFieldScene(PlayerState.DefaultTown) == GameSession.FieldSceneName, "栈桥镇位于野外");
            var shop = GameDB.Shop(town.shopId);
            Check(shop != null, "盐井聚落商店存在");
            foreach (var id in NewParts)
                Check(shop.goods.Any(g => g != null && g.partId == id), $"盐井聚落出售 {id}");

            // 场景
            var spawns = new Dictionary<string, List<string>>();
            var portals = new List<(string scene, string target, string spawn, string condition)>();
            var triggers = new Dictionary<string, (string condition, string effects)>();
            var bountyScenes = new List<string>();
            foreach (var name in PrototypeSceneBuilder.LogicScenes)
            {
                EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath(name), OpenSceneMode.Single);
                spawns[name] = Find<SpawnPoint>().Select(s => s.spawnId).ToList();
                portals.AddRange(Find<ScenePortal>().Select(p => (name, p.targetScene, p.targetSpawnId, p.condition)));
                foreach (var t in Find<StoryTrigger>()) triggers[t.triggerId] = (t.condition, t.effects);
                if (Find<BountyZone>().Any(b => b.bounty != null && b.bounty.enemyId == "ENM_Bounty_SaltWyrm")) bountyScenes.Add(name);
                foreach (var z in Find<EncounterZone>())
                    Check(z.groups.All(g => g.members.All(m => m != null)), $"{name} 的 {z.name} 遇敌组无空成员");
                if (name == GameSession.SaltBeltSceneName)
                    Check(Find<TownGate>().Any(g => g.townId == "TWN_Saltwell"), "白盐带有盐井聚落入口");
            }
            foreach (var (scene, target, spawn, _) in portals)
                Check(spawns.TryGetValue(target, out var list) && list.Contains(spawn), $"{scene} 的传送口指向 {target}/{spawn} 存在");
            Check(bountyScenes.SequenceEqual(new[] { GameSession.GhostCitySceneName }), "盐沙巨虫只在空城");

            // 流程
            StoryService.AbortDialogue();
            var s = new PlayerState(0) { party = DemoFactory.CreateParty() };
            var toBelt = portals.First(p => p.target == GameSession.SaltBeltSceneName && p.scene == GameSession.FieldSceneName);
            var toGhost = portals.First(p => p.target == GameSession.GhostCitySceneName);
            Check(!Conditions.Evaluate(toBelt.condition, s), "第一幕未完成时白盐带未开放");
            StoryService.SetFlag(s, "act1_done", true);
            Check(Conditions.Evaluate(toBelt.condition, s), "第一幕完成后白盐带开放");

            Check(Conditions.Evaluate(triggers["act2_intro"].condition, s), "第二幕开场可触发");
            Effects.Apply(triggers["act2_intro"].effects, s);
            Check(StoryService.Quest(s, "QST_Act2_Ledger")?.state == QuestState.Active, "第二幕任务开始");
            Check(!Conditions.Evaluate(toGhost.condition, s), "进镇前空城未开放");
            Check(Conditions.Evaluate(triggers["act2_saltwell"].condition, s), "进镇触发器可触发");
            Effects.Apply(triggers["act2_saltwell"].effects, s);
            Check(Conditions.Evaluate(toGhost.condition, s), "进镇后空城开放");
            Check(!Conditions.Evaluate(triggers["act2_calibration"].condition, s), "击败巨虫前校准点不可读取");

            BountyService.OnVictory(s, new List<Combatant> { wyrm.CreateCombatant() });
            Check(s.flags.Contains("act2_got_ledger") && Conditions.Evaluate(triggers["act2_calibration"].condition, s), "击败巨虫后校准点可读取");
            Effects.Apply(triggers["act2_calibration"].effects, s);
            Check(StoryService.Quest(s, "QST_Act2_Ledger")?.state == QuestState.Completed && s.flags.Contains("act2_done"), "第二幕任务完成");

            Debug.Log("[Act2Test] 全部通过");
        }

        private static IEnumerable<T> Find<T>() where T : Object =>
            Object.FindObjectsByType<T>(FindObjectsInactive.Include);

        private static void Check(bool ok, string what)
        {
            if (!ok) throw new Exception($"[Act2Test] 失败：{what}");
        }
    }
}
