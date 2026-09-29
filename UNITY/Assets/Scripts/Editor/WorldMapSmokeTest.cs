using System;
using System.Linq;
using Game.Core;
using Game.UI;
using Game.WorldMap;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>大地图与界面路由冒烟测试：地图配置、边界、地点发现与条件、坐标归一化、界面栈与分页</summary>
    public static class WorldMapSmokeTest
    {
        [MenuItem("Game/大地图与界面冒烟测试")]
        public static void Run()
        {
            GameDB.Reload();
            var field = WorldMapService.MapOfScene(GameSession.FieldSceneName);
            var pump = WorldMapService.MapOfScene(GameSession.PumpStationSceneName);
            Check(field != null && pump != null, "野外与泵站都有地图配置");
            foreach (var scene in PrototypeSceneBuilder.LogicScenes.Where(sc => sc != GameSession.BattleSceneName))
                Check(WorldMapService.MapOfScene(scene) != null, $"逻辑场景 {scene} 有地图配置（I-23）");
            Check(GameDB.AllMapEntries().Where(e => !e.IsMap).All(e => GameDB.MapEntry(e.mapId)?.IsMap == true),
                "每个地点都属于存在的地图");

            // 边界
            var outside = WorldMapService.Clamp(field, new Vector3(100, 1, -100));
            Check(Mathf.Approximately(outside.x, field.Bounds.xMax) && Mathf.Approximately(outside.z, field.Bounds.yMin) && Mathf.Approximately(outside.y, 1), "越界位置被限制在边界内");
            var inside = new Vector3(3, 0, 4);
            Check(WorldMapService.Clamp(field, inside) == inside, "边界内位置不变");
            var n = WorldMapService.Normalize(field, new Vector3(field.Bounds.xMin, 0, field.Bounds.yMax));
            Check(n == new Vector2(0, 1), "左上角归一化为 (0,1)");

            // 发现：靠近才发现；有条件的地点满足条件才发现
            var s = new PlayerState(0);
            var town = GameDB.MapEntry("TWN_Zhanqiao");
            var pumpLoc = GameDB.MapEntry("LOC_PumpStation");
            Check(WorldMapService.Tick(s, field.entryId, new Vector3(25, 0, -25)) == 0, "远处不发现地点");
            Check(WorldMapService.Tick(s, field.entryId, new Vector3(town.position.x, 0, town.position.y)) == 1
                  && WorldMapService.IsFound(s, "TWN_Zhanqiao"), "靠近城镇发现城镇");
            Check(WorldMapService.Tick(s, field.entryId, new Vector3(town.position.x, 0, town.position.y)) == 0, "已发现不重复");
            var atPump = new Vector3(pumpLoc.position.x, 0, pumpLoc.position.y);
            Check(WorldMapService.Tick(s, field.entryId, atPump) == 0, "未听到信号前不发现泵站");
            s.flags.Add("act1_heard_signal");
            Check(WorldMapService.Tick(s, field.entryId, atPump) == 1, "满足条件后发现泵站");
            Check(WorldMapService.KnownLocations(s, field.entryId).Count() == 2, "地图显示两个已发现地点");

            // 界面栈与分页
            UIRouter.CloseAll();
            Check(!UIRouter.BlocksFieldInput, "无界面时不阻断野外操作");
            UIRouter.Open(UIScreen.Menu);
            UIRouter.Open(UIScreen.WorldMap);
            Check(UIRouter.Current == UIScreen.WorldMap && UIRouter.BlocksFieldInput, "打开界面后阻断野外操作");
            UIRouter.Close();
            Check(UIRouter.Current == UIScreen.Menu, "关闭最上层后回到菜单");
            UIRouter.SetTab(MenuTab.Party);
            UIRouter.CycleTab(-1);
            Check(UIRouter.Tab == MenuTab.System, "分页循环切换");
            UIRouter.CloseAll();
            Check(UIRouter.Current == UIScreen.None, "全部关闭");
            Debug.Log("[WorldMapTest] 全部通过");
        }

        private static void Check(bool ok, string what)
        {
            if (!ok) throw new Exception($"[WorldMapTest] 失败：{what}");
        }
    }
}
