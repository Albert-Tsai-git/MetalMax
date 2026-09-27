using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Story;
using UnityEngine;

namespace Game.WorldMap
{
    /// <summary>
    /// 大地图逻辑：地图边界、地点发现、坐标归一化（供地图界面绘制）。
    /// 已发现地点记为旗标 found:{地点ID}，随存档保存。
    /// </summary>
    public static class WorldMapService
    {
        public const string FoundPrefix = "found:";

        /// <summary>发现新地点</summary>
        public static event Action<WorldMapData> Discovered;

        public static WorldMapData MapOfScene(string scene) =>
            GameDB.AllMapEntries().FirstOrDefault(e => e.IsMap && e.scene == scene);

        public static IEnumerable<WorldMapData> Locations(string mapId) =>
            GameDB.AllMapEntries().Where(e => !e.IsMap && e.mapId == mapId);

        public static bool IsFound(PlayerState s, string entryId) => s.flags.Contains(FoundPrefix + entryId);

        /// <summary>地图界面上可显示的地点：已发现的</summary>
        public static IEnumerable<WorldMapData> KnownLocations(PlayerState s, string mapId) =>
            Locations(mapId).Where(l => IsFound(s, l.entryId));

        /// <summary>把位置限制在地图边界内（没有地图配置时原样返回）</summary>
        public static Vector3 Clamp(WorldMapData map, Vector3 pos)
        {
            if (map == null) return pos;
            var b = map.Bounds;
            return new Vector3(Mathf.Clamp(pos.x, b.xMin, b.xMax), pos.y, Mathf.Clamp(pos.z, b.yMin, b.yMax));
        }

        /// <summary>世界坐标 → 地图内 0~1 坐标（左下为 0,0），供界面绘制</summary>
        public static Vector2 Normalize(WorldMapData map, Vector3 pos)
        {
            if (map == null) return Vector2.zero;
            var b = map.Bounds;
            return new Vector2(Mathf.InverseLerp(b.xMin, b.xMax, pos.x), Mathf.InverseLerp(b.yMin, b.yMax, pos.z));
        }

        /// <summary>玩家移动时调用：进入发现半径且条件满足的地点记为已发现，返回本次新发现数</summary>
        public static int Tick(PlayerState s, string mapId, Vector3 pos)
        {
            int n = 0;
            foreach (var l in Locations(mapId))
            {
                if (IsFound(s, l.entryId)) continue;
                if (Vector2.Distance(l.position, new Vector2(pos.x, pos.z)) > l.DiscoverRadius) continue;
                if (!Conditions.Evaluate(l.condition, s)) continue;
                s.flags.Add(FoundPrefix + l.entryId);
                n++;
                Debug.Log($"[WorldMap] 发现地点 {l.entryId}");
                Discovered?.Invoke(l);
            }
            return n;
        }
    }
}
