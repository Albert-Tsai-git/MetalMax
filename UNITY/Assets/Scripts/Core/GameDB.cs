using System.Collections.Generic;
using Game.Battle;
using Game.Economy;
using Game.Items;
using Game.Progression;
using Game.Story;
using Game.Tank;
using Game.Town;
using Game.WorldMap;
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
        private static Dictionary<string, TownData> _towns;
        private static Dictionary<string, CharacterData> _characters;
        private static Dictionary<string, QuestData> _quests;
        private static Dictionary<string, SkillData> _skills;
        private static Dictionary<string, ItemData> _items;
        private static Dictionary<string, DialogueData> _dialogues;
        private static Dictionary<string, WorldMapData> _mapEntries;

        public static TankPartData Part(string id) => Find(ref _parts, "GameData/Parts", id);
        public static EnemyData Enemy(string id) => Find(ref _enemies, "GameData/Enemies", id);
        public static ShopData Shop(string id) => Find(ref _shops, "GameData/Shops", id);
        public static TownData Town(string id) => Find(ref _towns, "GameData/Towns", id);
        public static CharacterData Character(string id) => Find(ref _characters, "GameData/Characters", id);
        public static QuestData Quest(string id) => Find(ref _quests, "GameData/Quests", id);
        public static SkillData Skill(string id) => Find(ref _skills, "GameData/Skills", id);
        public static ItemData Item(string id) => Find(ref _items, "GameData/Items", id);
        public static DialogueData Dialogue(string id) => Find(ref _dialogues, "GameData/Dialogues", id);

        public static WorldMapData MapEntry(string id) => Find(ref _mapEntries, "GameData/WorldMap", id);

        public static IEnumerable<WorldMapData> AllMapEntries()
        {
            Find(ref _mapEntries, "GameData/WorldMap", null);
            return _mapEntries.Values;
        }

        public static IEnumerable<EnemyData> AllEnemies()
        {
            Find(ref _enemies, "GameData/Enemies", null);
            return _enemies.Values;
        }

        /// <summary>数据重新导入后清除缓存</summary>
        public static void Reload()
        {
            _parts = null;
            _enemies = null;
            _shops = null;
            _towns = null;
            _characters = null;
            _quests = null;
            _skills = null;
            _items = null;
            _dialogues = null;
            _mapEntries = null;
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
