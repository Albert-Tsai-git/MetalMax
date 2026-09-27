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
    /// 泵站迷宫冒烟测试：
    /// 1) 场景连通性：所有传送口的目标场景在 Build Settings 中、目标出生点存在；宝箱 ID 唯一；迷宫遇敌组有效；赏金首只在迷宫。
    /// 2) 第一幕流程（纯逻辑）：开场 → 泵站开放 → 宝箱 → 击败铁钳巨蟹 → 回报 → 任务完成。
    /// </summary>
    public static class DungeonSmokeTest
    {
        [MenuItem("Game/迷宫冒烟测试")]
        public static void Run()
        {
            GameDB.Reload();
            var buildScenes = EditorBuildSettings.scenes.Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path)).ToList();
            foreach (var name in PrototypeSceneBuilder.LogicScenes)
                Check(buildScenes.Contains(name), $"{name} 在 Build Settings 中");

            // 扫描各逻辑场景
            var spawns = new Dictionary<string, List<string>>();
            var portals = new List<(string scene, string target, string spawn, string condition)>();
            var chests = new List<(string id, string contents)>();
            var triggers = new Dictionary<string, (string condition, string effects)>();
            var bountyScenes = new List<string>();
            foreach (var name in PrototypeSceneBuilder.LogicScenes)
            {
                EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath(name), OpenSceneMode.Single);
                spawns[name] = Find<SpawnPoint>().Select(s => s.spawnId).ToList();
                portals.AddRange(Find<ScenePortal>().Select(p => (name, p.targetScene, p.targetSpawnId, p.condition)));
                chests.AddRange(Find<TreasureChest>().Select(c => (c.chestId, c.contents)));
                foreach (var t in Find<StoryTrigger>()) triggers[t.triggerId] = (t.condition, t.effects);
                if (Find<BountyZone>().Any(b => b.bounty != null && b.bounty.enemyId == "ENM_Bounty_IronCrab")) bountyScenes.Add(name);
                foreach (var z in Find<EncounterZone>())
                    Check(z.groups.All(g => g.members.All(m => m != null)), $"{name} 的 {z.name} 遇敌组无空成员");
                Check(Find<ArtSceneLoader>().Any(), $"{name} 含 ArtSceneLoader");
            }
            Check(portals.Count >= 2, "至少有进出泵站两个传送口");
            foreach (var (scene, target, spawn, _) in portals)
                Check(buildScenes.Contains(target) && spawns.TryGetValue(target, out var list) && list.Contains(spawn),
                    $"{scene} 的传送口指向 {target}/{spawn} 存在");
            Check(chests.Count == 3 && chests.Select(c => c.id).Distinct().Count() == chests.Count, "3 个宝箱且 ID 唯一");
            Check(bountyScenes.SequenceEqual(new[] { GameSession.PumpStationSceneName }), "铁钳巨蟹只在泵站");
            Check(triggers.ContainsKey("act1_intro") && !triggers.ContainsKey("act1_report"), "第一幕开场触发器存在，回报不走镇口触发器");
            // 回报由曲婆对话的条件节点承接
            var qupo = GameDB.Dialogue("DLG_Qupo");
            var reportNode = qupo?.nodes.Find(n => n.effects != null && n.effects.Contains("set:act1_reported"));
            Check(reportNode != null && !string.IsNullOrEmpty(reportNode.condition), "曲婆对话含条件回报节点");

            // 第一幕流程（纯逻辑，按场景中的实际配置执行）
            StoryService.AbortDialogue();
            var s = new PlayerState(0) { party = DemoFactory.CreateParty() };
            var pumpPortal = portals.First(p => p.target == GameSession.PumpStationSceneName);
            Check(!Conditions.Evaluate(pumpPortal.condition, s), "开场前泵站未开放");
            Effects.Apply(triggers["act1_intro"].effects, s);
            Check(Conditions.Evaluate(pumpPortal.condition, s), "开场后泵站开放");
            Check(!Conditions.Evaluate(reportNode.condition, s), "未得到日志时不触发回报");

            foreach (var (id, contents) in chests)
                Check(TreasureChest.Open(s, id, contents) == OpResult.Ok, $"打开宝箱 {id}");
            Check(TreasureChest.Open(s, chests[0].id, chests[0].contents) == OpResult.NothingToDo, "宝箱不能重复打开");
            Check(s.Gold == 300 && ItemService.Count(s, "ITM_RepairPack") == 2 && ItemService.Count(s, "ITM_ReviveKit") == 1
                  && ItemService.Count(s, "ITM_Tonic") == 3, "宝箱内容到账");

            BountyService.OnVictory(s, new List<Combatant> { GameDB.Enemy("ENM_Bounty_IronCrab").CreateCombatant() });
            Check(s.flags.Contains("act1_got_log") && Conditions.Evaluate(reportNode.condition, s), "击败巨蟹得到日志，可回报");
            Effects.Apply(reportNode.effects, s);
            Check(StoryService.Quest(s, "QST_Act1_Signal")?.state == QuestState.Completed, "回报后第一幕任务完成");
            Check(!Conditions.Evaluate(reportNode.condition, s), "回报后不再触发");

            // 场景传送的出生点只取一次
            SceneTravel.SetPendingSpawn("entrance");
            Check(SceneTravel.TryConsumeSpawn(out var sp) && sp == "entrance" && !SceneTravel.TryConsumeSpawn(out _), "出生点只取一次");
            Debug.Log("[DungeonTest] 全部通过");
        }

        private static IEnumerable<T> Find<T>() where T : Object =>
            Object.FindObjectsByType<T>(FindObjectsInactive.Include);

        private static void Check(bool ok, string what)
        {
            if (!ok) throw new Exception($"[DungeonTest] 失败：{what}");
        }
    }
}
