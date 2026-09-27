using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Battle;
using Game.Core;
using Game.Core.Save;
using Game.Field;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>
    /// 步行与交互冒烟测试：战车停放状态存档与迁移、步行遇敌不能乘车、宝箱/NPC 交互条件、场景中交互组件配置。
    /// 实际按键移动与上下车需在 Play 中验证。
    /// </summary>
    public static class FieldModeSmokeTest
    {
        [MenuItem("Game/步行与交互冒烟测试")]
        public static void Run()
        {
            GameDB.Reload();
            var s = new PlayerState(0) { party = DemoFactory.CreateParty() };
            var hunter = s.party[0];

            // 步行遇敌：战车不在身边，不能乘车也不能上车
            hunter.inTank = true;
            hunter.tankAway = true;
            Check(!hunter.IsTankActive && !hunter.CanBoard, "战车不在身边时不能乘车/上车");
            hunter.inTank = false;
            var dummy = GameDB.Enemy("ENM_Ant").CreateCombatant();
            dummy.maxHp = dummy.hp = 99999;
            dummy.attack = 0;
            hunter.speed = 999;
            new BattleSystem(s.party, new List<Combatant> { dummy }, 1)
                .SubmitCommands(new List<BattleAction> { BattleAction.Simple(hunter, ActionType.BoardTank) });
            Check(!hunter.inTank, "战斗中上车指令被拒绝");
            hunter.tankAway = false;
            Check(hunter.CanBoard, "战车在身边时可以上车");

            // 停车状态存档 v6 与 v5 迁移
            string oldDir = SaveSystem.Directory;
            SaveSystem.Directory = Path.Combine(Path.GetTempPath(), "game_fieldmode_test");
            try
            {
                s.vehicle = new VehicleState { parked = true, scene = GameSession.PumpStationSceneName, position = new Vector3(1, 2, 3), yaw = 90 };
                Check(SaveSystem.Save(0, s, "Field", Vector3.zero) == OpResult.Ok, "存档");
                Check(SaveSystem.Load(0, out _, out var r) == OpResult.Ok && r.vehicle.parked
                      && r.vehicle.scene == GameSession.PumpStationSceneName && r.vehicle.position == new Vector3(1, 2, 3)
                      && Mathf.Approximately(r.vehicle.yaw, 90), "停车状态还原");
                var v5 = JsonUtility.FromJson<SaveData>(File.ReadAllText(SaveSystem.PathOf(0)));
                v5.version = 5;
                File.WriteAllText(SaveSystem.PathOf(1), JsonUtility.ToJson(v5));
                Check(SaveSystem.Load(1, out _, out var r5) == OpResult.Ok && !r5.vehicle.parked, "v5 存档迁移为在车上");
            }
            finally
            {
                SaveSystem.Directory = oldDir;
            }

            // 交互条件：宝箱开过后不可交互；NPC 按条件
            var go = new GameObject("TestInteractables");
            try
            {
                var chest = go.AddComponent<TreasureChest>();
                chest.chestId = "test_chest";
                chest.contents = "gold:+5";
                var st = new PlayerState(0);
                Check(chest.CanInteract(st) && chest.PromptKey == "UI.Interact.Open", "未开宝箱可交互");
                chest.Interact(st);
                Check(st.Gold == 5 && !chest.CanInteract(st), "开箱后到账且不可再交互");
                var npc = go.AddComponent<NpcTalk>();
                npc.condition = "flag:met";
                Check(!npc.CanInteract(st), "条件不满足的 NPC 不可对话");
                st.flags.Add("met");
                Check(npc.CanInteract(st) && npc.PromptKey == "UI.Interact.Talk", "条件满足可对话");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            // 场景配置
            EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath(GameSession.FieldSceneName), OpenSceneMode.Single);
            Check(Object.FindAnyObjectByType<FieldPlayerController>()?.GetComponent<FieldInteractor>() != null, "野外玩家有交互组件");
            Check(Object.FindObjectsByType<NpcTalk>(FindObjectsInactive.Include).Any(n => n.npcId == "NPC_Qupo"), "野外有 NPC 曲婆");
            EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath(GameSession.PumpStationSceneName), OpenSceneMode.Single);
            Check(Object.FindAnyObjectByType<FieldPlayerController>()?.GetComponent<FieldInteractor>() != null, "迷宫玩家有交互组件");
            Check(Object.FindObjectsByType<TreasureChest>(FindObjectsInactive.Include).Length == 3, "迷宫 3 个宝箱均为可交互对象");
            Debug.Log("[FieldModeTest] 全部通过");
        }

        private static void Check(bool ok, string what)
        {
            if (!ok) throw new Exception($"[FieldModeTest] 失败：{what}");
        }
    }
}
