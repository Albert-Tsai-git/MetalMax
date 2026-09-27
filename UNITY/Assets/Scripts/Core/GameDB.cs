using System.Collections.Generic;
using Game.Battle;
using Game.Economy;
using Game.Tank;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 运行时数据库：按 ID 读取 CsvDataImporter 生成的数据资产（Resources/GameData/）。
    /// 存档只保存 ID，读档时由此还原引用。
    /// </summary>
    public static class GameDB
    {
        private static Dictionary<string, TankPartData> _parts;
        private static Dictionary<string, EnemyData> _enemies;
        private static Dictionary<string, ShopData> _shops;

        public static TankPartData Part(string id) => Find(ref _parts, "GameData/Parts", id);
        public static EnemyData Enemy(string id) => Find(ref _enemies, "GameData/Enemies", id);
        public static ShopData Shop(string id) => Find(ref _shops, "GameData/Shops", id);

        /// <summary>数据重新导入后清除缓存</summary>
        public static void Reload()
        {
            _parts = null;
            _enemies = null;
            _shops = null;
        }

        private static T Find<T>(ref Dictionary<string, T> map, string folder, string id) where T : ScriptableObject
        {
            if (map == null)
            {
                map = new Dictionary<string, T>();
                foreach (var a in Resources.LoadAll<T>(folder)) map[a.name] = a;
                Debug.Log($"[GameDB] 已加载 {folder}：{map.Count} 条");
            }
            if (string.IsNullOrEmpty(id)) return null;
            if (map.TryGetValue(id, out var v)) return v;
            Debug.LogWarning($"[GameDB] {folder} 中找不到 {id}");
            return null;
        }
    }
}
