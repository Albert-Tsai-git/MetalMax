using Game.Core;
using UnityEngine;

namespace Game.WorldMap
{
    public enum MapEntryKind { Map, Town, Dungeon, Landmark }

    /// <summary>
    /// 大地图条目：由 Data/worldmap.csv 生成。
    /// Map 为一张地图（对应一个逻辑场景，含可活动边界）；其余为地图上的地点（位置、发现半径、发现条件）。
    /// </summary>
    public class WorldMapData : ScriptableObject
    {
        public string entryId;
        public MapEntryKind kind;
        [Tooltip("地点所在地图 ID（Map 自身为空）")]
        public string mapId;
        [Tooltip("Map 对应的逻辑场景名")]
        public string scene;
        [Tooltip("Map：边界中心；地点：位置（世界坐标 XZ）")]
        public Vector2 position;
        [Tooltip("Map：边界尺寸；地点：x 为发现半径")]
        public Vector2 size;
        [Tooltip("地点可被发现的条件（Conditions 语法），空为总是")]
        public string condition;

        public bool IsMap => kind == MapEntryKind.Map;
        public Rect Bounds => new(position - size / 2f, size);
        public float DiscoverRadius => size.x;
        public string DisplayName => TextDB.Name(entryId);
    }
}
