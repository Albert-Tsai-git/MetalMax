using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Game.Battle;
using Game.Core;
using Game.Economy;
using Game.Items;
using Game.Progression;
using Game.Story;
using Game.Tank;
using Game.Town;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 数值表导入：读取工程根目录 Data/ 下的 CSV（Excel 另存为“CSV UTF-8”），
    /// 生成或更新 Assets/Resources/GameData/ 下的 ScriptableObject。
    /// 以 id 列作为资产文件名：已有资产只更新字段，GUID 不变，场景里的引用不会断。
    /// 文本表 Data/Text/text_{lang}.csv（key,text）生成 Assets/Resources/Text/TextTable_{lang}.asset。
    /// </summary>
    public static class CsvDataImporter
    {
        private const string DataDir = "Data";
        private const string OutDir = "Assets/Resources/GameData";
        private const string TextDir = "Data/Text";
        private const string TextOutDir = "Assets/Resources/Text";

        [MenuItem("Game/导入数值表 (CSV)")]
        public static void ImportAll()
        {
            int n = 0;
            n += Import("skills.csv", $"{OutDir}/Skills", CreateSkill);
            n += Import("items.csv", $"{OutDir}/Items", CreateItem);
            n += Import("parts.csv", $"{OutDir}/Parts", CreatePart);
            n += Import("enemies.csv", $"{OutDir}/Enemies", CreateEnemy);
            n += Import("characters.csv", $"{OutDir}/Characters", CreateCharacter);
            n += ImportShops();
            n += ImportQuests();
            n += ImportDialogues();
            n += Import("towns.csv", $"{OutDir}/Towns", CreateTown);
            n += ImportTexts();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            TextDB.SetLanguage(TextDB.Language); // 清除运行时文本缓存
            GameDB.Reload();
            Debug.Log($"[Import] 完成，共导入 {n} 条");
        }

        /// <summary>通用导入：每一行交给 factory 生成或填充资产</summary>
        private static int Import(string file, string outDir,
            Func<Row, ScriptableObject, ScriptableObject> factory)
        {
            string path = Path.Combine(DataDir, file);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[Import] 未找到 {path}，跳过");
                return 0;
            }
            Directory.CreateDirectory(outDir);

            var rows = ReadCsv(path);
            int count = 0;
            foreach (var row in rows)
            {
                string id = row.Str("id");
                if (string.IsNullOrWhiteSpace(id)) continue;
                try
                {
                    string assetPath = $"{outDir}/{id}.asset";
                    var existing = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                    var so = factory(row, existing);
                    if (so == null) continue;

                    if (existing == null) AssetDatabase.CreateAsset(so, assetPath);
                    else if (existing != so)
                    {
                        // 类型变了（比如引擎改成了武器），只能删掉重建
                        AssetDatabase.DeleteAsset(assetPath);
                        AssetDatabase.CreateAsset(so, assetPath);
                    }
                    EditorUtility.SetDirty(so);
                    count++;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Import] {file} 第 {row.Line} 行 ({id}) 出错：{e.Message}");
                }
            }
            Debug.Log($"[Import] {file} → {outDir}：{count} 条");
            return count;
        }

        /// <summary>按某一列分组读取表格行（保持原顺序）</summary>
        private static Dictionary<string, List<Row>> GroupRows(string path, string key)
        {
            var groups = new Dictionary<string, List<Row>>();
            foreach (var r in ReadCsv(path))
            {
                string id = r.Str(key);
                if (id == "") continue;
                if (!groups.TryGetValue(id, out var list)) groups[id] = list = new List<Row>();
                list.Add(r);
            }
            return groups;
        }

        private static T LoadOrCreate<T>(string assetPath, out bool isNew) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            isNew = a == null;
            return a ?? ScriptableObject.CreateInstance<T>();
        }

        /// <summary>任务表 quests.csv：quest_id,step,objective,effects；同一任务按行顺序为步骤</summary>
        private static int ImportQuests()
        {
            string path = Path.Combine(DataDir, "quests.csv");
            if (!File.Exists(path)) { Debug.LogWarning($"[Import] 未找到 {path}，跳过"); return 0; }
            string dir = $"{OutDir}/Quests";
            Directory.CreateDirectory(dir);
            var groups = GroupRows(path, "quest_id");
            foreach (var (id, rows) in groups)
            {
                string assetPath = $"{dir}/{id}.asset";
                var q = LoadOrCreate<QuestData>(assetPath, out bool isNew);
                q.questId = id;
                q.name = id;
                q.steps = rows.OrderBy(r => r.Int("step"))
                    .Select(r => new QuestData.Step { objective = r.Str("objective"), effects = r.Str("effects") }).ToList();
                if (isNew) AssetDatabase.CreateAsset(q, assetPath);
                EditorUtility.SetDirty(q);
            }
            Debug.Log($"[Import] quests.csv → {dir}：{groups.Count} 个任务");
            return groups.Count;
        }

        /// <summary>
        /// 对话表 Text/dialogue.csv：dialogue_id,node_id,type,speaker,text_key,next,condition,effects。
        /// type=line 为台词节点（第一行台词为入口）；type=choice 为所属 node_id 的选项（text_key、next、condition）。
        /// </summary>
        private static int ImportDialogues()
        {
            string path = Path.Combine(TextDir, "dialogue.csv");
            if (!File.Exists(path)) { Debug.LogWarning($"[Import] 未找到 {path}，跳过"); return 0; }
            string dir = $"{OutDir}/Dialogues";
            Directory.CreateDirectory(dir);
            var groups = GroupRows(path, "dialogue_id");
            foreach (var (id, rows) in groups)
            {
                string assetPath = $"{dir}/{id}.asset";
                var d = LoadOrCreate<DialogueData>(assetPath, out bool isNew);
                d.dialogueId = id;
                d.name = id;
                d.nodes = new List<DialogueData.Node>();
                foreach (var r in rows.Where(r => r.Str("type", "line") == "line"))
                    d.nodes.Add(new DialogueData.Node
                    {
                        nodeId = r.Str("node_id"), speaker = r.Str("speaker"), textKey = r.Str("text_key"),
                        next = r.Str("next"), condition = r.Str("condition"), effects = r.Str("effects"),
                    });
                foreach (var r in rows.Where(r => r.Str("type") == "choice"))
                {
                    var node = d.Find(r.Str("node_id"));
                    if (node == null) { Debug.LogError($"[Import] dialogue.csv 第 {r.Line} 行：选项所属节点 {r.Str("node_id")} 不存在"); continue; }
                    node.choices.Add(new DialogueData.Choice { textKey = r.Str("text_key"), next = r.Str("next"), condition = r.Str("condition") });
                }
                foreach (var n in d.nodes)
                {
                    foreach (var target in n.choices.Select(c => c.next).Append(n.next))
                        if (target != "" && d.Find(target) == null)
                            Debug.LogError($"[Import] 对话 {id} 节点 {n.nodeId} 指向不存在的节点 {target}");
                }
                if (isNew) AssetDatabase.CreateAsset(d, assetPath);
                EditorUtility.SetDirty(d);
            }
            Debug.Log($"[Import] dialogue.csv → {dir}：{groups.Count} 段对话");
            return groups.Count;
        }

        /// <summary>商店表 shops.csv：shop_id,part_id，一行一件商品；同一商店的行合并为一个资产</summary>
        private static int ImportShops()
        {
            string path = Path.Combine(DataDir, "shops.csv");
            if (!File.Exists(path)) { Debug.LogWarning($"[Import] 未找到 {path}，跳过"); return 0; }
            string dir = $"{OutDir}/Shops";
            Directory.CreateDirectory(dir);

            // part_id 列：ITM_ 开头为道具，其余为部件
            var groups = new Dictionary<string, List<TankPartData>>();
            var itemGroups = new Dictionary<string, List<ItemData>>();
            foreach (var r in ReadCsv(path))
            {
                string shopId = r.Str("shop_id"), goodsId = r.Str("part_id");
                if (shopId == "") continue;
                if (!groups.ContainsKey(shopId)) { groups[shopId] = new List<TankPartData>(); itemGroups[shopId] = new List<ItemData>(); }
                if (goodsId.StartsWith("ITM_"))
                {
                    var item = AssetDatabase.LoadAssetAtPath<ItemData>($"{OutDir}/Items/{goodsId}.asset");
                    if (item == null) { Debug.LogError($"[Import] shops.csv 第 {r.Line} 行：道具 {goodsId} 不存在"); continue; }
                    itemGroups[shopId].Add(item);
                    continue;
                }
                var part = AssetDatabase.LoadAssetAtPath<TankPartData>($"{OutDir}/Parts/{goodsId}.asset");
                if (part == null) { Debug.LogError($"[Import] shops.csv 第 {r.Line} 行：部件 {goodsId} 不存在"); continue; }
                groups[shopId].Add(part);
            }
            foreach (var (shopId, goods) in groups)
            {
                string assetPath = $"{dir}/{shopId}.asset";
                var shop = AssetDatabase.LoadAssetAtPath<ShopData>(assetPath);
                bool isNew = shop == null;
                if (isNew) shop = ScriptableObject.CreateInstance<ShopData>();
                shop.shopId = shopId;
                shop.goods = goods;
                shop.items = itemGroups[shopId];
                shop.name = shopId;
                if (isNew) AssetDatabase.CreateAsset(shop, assetPath);
                EditorUtility.SetDirty(shop);
            }
            Debug.Log($"[Import] shops.csv → {dir}：{groups.Count} 个商店");
            return groups.Count;
        }

        /// <summary>导入所有语言的文本表，并检查每个数据 ID 都有 .name 键</summary>
        private static int ImportTexts()
        {
            if (!Directory.Exists(TextDir))
            {
                Debug.LogWarning($"[Import] 未找到 {TextDir}，跳过文本表");
                return 0;
            }
            Directory.CreateDirectory(TextOutDir);
            var ids = new HashSet<string>();
            foreach (var file in new[] { "parts.csv", "enemies.csv", "characters.csv", "skills.csv", "items.csv" })
            {
                string path = Path.Combine(DataDir, file);
                if (File.Exists(path)) foreach (var r in ReadCsv(path)) if (r.Str("id") != "") ids.Add(r.Str("id"));
            }
            string townsPath = Path.Combine(DataDir, "towns.csv");
            if (File.Exists(townsPath)) foreach (var r in ReadCsv(townsPath)) if (r.Str("id") != "") ids.Add(r.Str("id"));
            string shopsPath = Path.Combine(DataDir, "shops.csv");
            if (File.Exists(shopsPath)) foreach (var r in ReadCsv(shopsPath)) if (r.Str("shop_id") != "") ids.Add(r.Str("shop_id"));

            int total = 0;
            foreach (var path in Directory.GetFiles(TextDir, "text_*.csv"))
            {
                string lang = Path.GetFileNameWithoutExtension(path).Substring("text_".Length);
                string assetPath = $"{TextOutDir}/TextTable_{lang}.asset";
                var table = AssetDatabase.LoadAssetAtPath<TextTable>(assetPath);
                bool isNew = table == null;
                if (isNew) table = ScriptableObject.CreateInstance<TextTable>();

                table.entries.Clear();
                var seen = new HashSet<string>();
                foreach (var r in ReadCsv(path))
                {
                    string key = r.Str("key");
                    if (key == "") continue;
                    if (!seen.Add(key)) { Debug.LogError($"[Import] {path} 第 {r.Line} 行键重复：{key}"); continue; }
                    table.entries.Add(new TextTable.Entry { key = key, text = r.Str("text") });
                }
                foreach (var id in ids)
                    if (!seen.Contains(id + ".name")) Debug.LogWarning($"[Import] {path} 缺少 {id}.name");

                if (isNew) AssetDatabase.CreateAsset(table, assetPath);
                EditorUtility.SetDirty(table);
                Debug.Log($"[Import] {path} → {assetPath}：{table.entries.Count} 条");
                total += table.entries.Count;
            }
            return total;
        }

        #region 各表字段映射

        private static T ParseEnum<T>(string s, T fallback, Row r) where T : struct
        {
            if (Enum.TryParse(s, true, out T v)) return v;
            Debug.LogError($"[Import] 第 {r.Line} 行：无法识别的值 {s}（{typeof(T).Name}）");
            return fallback;
        }

        private static T Reuse<T>(ScriptableObject existing) where T : ScriptableObject =>
            existing as T ?? ScriptableObject.CreateInstance<T>();

        private static ScriptableObject CreatePart(Row r, ScriptableObject existing)
        {
            TankPartData p;
            switch (r.Str("type").ToLowerInvariant())
            {
                case "chassis":
                    var c = Reuse<ChassisData>(existing);
                    c.weaponHoles = r.List("holes").Select(s => (WeaponType)Enum.Parse(typeof(WeaponType), s, true)).ToArray();
                    c.weaponMountNames = r.List("mounts").ToArray();
                    c.seats = r.Int("seats", 1);
                    p = c;
                    break;
                case "engine":
                    var e = Reuse<EngineData>(existing);
                    e.loadCapacity = r.Float("load");
                    e.loadPerUpgrade = r.Float("load_up", 1f);
                    p = e;
                    break;
                case "cunit":
                    var u = Reuse<CUnitData>(existing);
                    u.hitBonus = r.Int("hit");
                    u.evadeBonus = r.Int("evade");
                    u.actionsPerTurn = r.Int("actions", 1);
                    p = u;
                    break;
                case "weapon":
                    var w = Reuse<WeaponData>(existing);
                    w.weaponType = (WeaponType)Enum.Parse(typeof(WeaponType), r.Str("weapon_type"), true);
                    w.range = (AttackRange)Enum.Parse(typeof(AttackRange), r.Str("range", "Single"), true);
                    w.attack = r.Int("attack");
                    w.accuracy = r.Int("accuracy", 80);
                    w.maxAmmo = r.Int("ammo", -1);
                    w.attackPerUpgrade = r.Int("attack_up", 10);
                    w.element = ParseEnum(r.Str("element", "Normal"), Element.Normal, r);
                    w.ammoPrice = r.Int("ammo_price", 10);
                    p = w;
                    break;
                default:
                    throw new Exception($"未知部件类型 '{r.Str("type")}'");
            }

            p.partId = r.Str("id");
            p.weight = r.Float("weight");
            p.price = r.Int("price");
            p.defense = r.Int("defense");
            p.maxUpgradeLevel = r.Int("max_up", 5);
            p.weightPerUpgrade = r.Float("weight_up", 0.2f);
            p.upgradeCostBase = r.Int("up_cost", 200);
            p.name = p.partId;
            return p;
        }

        private static ScriptableObject CreateCharacter(Row r, ScriptableObject existing)
        {
            var c = Reuse<CharacterData>(existing);
            c.characterId = r.Str("id");
            c.hp = r.Int("hp", 50);
            c.attack = r.Int("attack");
            c.defense = r.Int("defense");
            c.speed = r.Int("speed");
            c.evade = r.Int("evade", 5);
            c.hpPerLevel = r.Int("hp_up");
            c.attackPerLevel = r.Int("attack_up");
            c.defensePerLevel = r.Int("defense_up");
            c.speedPerLevel = r.Int("speed_up");
            c.evadePerLevel = r.Int("evade_up");
            c.repair = r.Int("repair");
            c.repairPerLevel = r.Int("repair_up");
            c.partRepairLevel = r.Int("part_repair_level", 99);
            c.maxLevel = r.Int("max_level", 99);
            c.expBase = r.Int("exp_base", 20);
            c.expGrowth = r.Float("exp_growth", 1.5f);
            c.name = c.characterId;
            return c;
        }

        private static ScriptableObject CreateSkill(Row r, ScriptableObject existing)
        {
            var k = Reuse<SkillData>(existing);
            k.skillId = r.Str("id");
            k.power = r.Int("power", 100);
            k.hits = Math.Max(1, r.Int("hits", 1));
            k.range = (AttackRange)Enum.Parse(typeof(AttackRange), r.Str("range", "Single"), true);
            k.accuracyBonus = r.Int("accuracy_bonus");
            k.partBreakChance = r.Int("part_break");
            k.pierceTank = r.Bool("pierce_tank");
            k.element = ParseEnum(r.Str("element", "Normal"), Element.Normal, r);
            k.healPercent = r.Int("heal_percent");
            k.name = k.skillId;
            return k;
        }

        private static ScriptableObject CreateItem(Row r, ScriptableObject existing)
        {
            var i = Reuse<ItemData>(existing);
            i.itemId = r.Str("id");
            i.price = r.Int("price", 10);
            i.kind = (ItemKind)Enum.Parse(typeof(ItemKind), r.Str("kind"), true);
            i.amount = r.Int("amount");
            i.allAllies = r.Bool("all");
            i.name = i.itemId;
            return i;
        }

        private static ScriptableObject CreateTown(Row r, ScriptableObject existing)
        {
            var t = Reuse<TownData>(existing);
            t.townId = r.Str("id");
            t.innPrice = r.Int("inn_price", 10);
            t.shopId = r.Str("shop_id");
            t.hasGarage = r.Bool("garage");
            t.hasBountyOffice = r.Bool("bounty_office");
            t.name = t.townId;
            return t;
        }

        private static ScriptableObject CreateEnemy(Row r, ScriptableObject existing)
        {
            var e = Reuse<EnemyData>(existing);
            e.enemyId = r.Str("id");
            e.maxHp = r.Int("hp");
            e.attack = r.Int("attack");
            e.defense = r.Int("defense");
            e.speed = r.Int("speed");
            e.evade = r.Int("evade", 5);
            e.accuracy = r.Int("accuracy", 85);
            e.expReward = r.Int("exp");
            e.goldReward = r.Int("gold");
            e.isBounty = r.Bool("bounty_flag");
            e.bounty = r.Int("bounty");
            e.ai = (AiType)Enum.Parse(typeof(AiType), r.Str("ai", "Random"), true);
            e.weak = r.List("weak").Select(x => ParseEnum(x, Element.Normal, r)).ToList();
            e.resist = r.List("resist").Select(x => ParseEnum(x, Element.Normal, r)).ToList();
            e.immune = r.List("immune").Select(x => ParseEnum(x, Element.Normal, r)).ToList();
            e.attackWeight = r.Int("attack_weight", 1);
            // skills 列：SKL_A:3|SKL_B:1（冒号后为权重，省略为 1）
            e.skills = r.List("skills").Select(x =>
            {
                var kv = x.Split(':');
                var skill = AssetDatabase.LoadAssetAtPath<SkillData>($"{OutDir}/Skills/{kv[0]}.asset");
                if (skill == null) Debug.LogError($"[Import] enemies.csv 第 {r.Line} 行：技能 {kv[0]} 不存在");
                return new WeightedSkill { skillId = kv[0], weight = kv.Length > 1 && int.TryParse(kv[1], out int w) ? w : 1 };
            }).ToList();
            e.name = e.enemyId;
            return e;
        }

        #endregion

        #region CSV 解析

        /// <summary>一行数据，按表头列名取值；空单元格返回默认值</summary>
        private class Row
        {
            public int Line;
            public Dictionary<string, string> Cells;

            public string Str(string key, string def = "") =>
                Cells.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : def;

            public int Int(string key, int def = 0) =>
                int.TryParse(Str(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : def;

            public float Float(string key, float def = 0f) =>
                float.TryParse(Str(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : def;

            public bool Bool(string key) =>
                Str(key).ToLowerInvariant() is "1" or "true" or "yes" or "是";

            /// <summary>用 | 分隔的列表，例如 MainCannon|SubGun</summary>
            public IEnumerable<string> List(string key) =>
                Str(key).Split('|', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());
        }

        private static List<Row> ReadCsv(string path)
        {
            // Excel 导出的 CSV 可能带 BOM，File.ReadAllText 会自动处理
            var records = ParseCsv(File.ReadAllText(path, Encoding.UTF8));
            var result = new List<Row>();
            if (records.Count == 0) return result;

            var header = records[0].Select(h => h.Trim().ToLowerInvariant()).ToArray();
            for (int i = 1; i < records.Count; i++)
            {
                var rec = records[i];
                // 跳过空行和以 # 开头的注释行
                if (rec.All(string.IsNullOrWhiteSpace) || rec[0].TrimStart().StartsWith("#")) continue;
                var cells = new Dictionary<string, string>();
                for (int c = 0; c < header.Length && c < rec.Count; c++) cells[header[c]] = rec[c];
                result.Add(new Row { Line = i + 1, Cells = cells });
            }
            return result;
        }

        /// <summary>支持双引号包裹、引号转义（""）和字段内换行的 CSV 解析</summary>
        private static List<List<string>> ParseCsv(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else sb.Append(ch);
                }
                else if (ch == '"') inQuotes = true;
                else if (ch == ',') { row.Add(sb.ToString()); sb.Clear(); }
                else if (ch == '\r') { }
                else if (ch == '\n') { row.Add(sb.ToString()); sb.Clear(); rows.Add(row); row = new List<string>(); }
                else sb.Append(ch);
            }
            if (sb.Length > 0 || row.Count > 0) { row.Add(sb.ToString()); rows.Add(row); }
            return rows;
        }

        #endregion
    }
}
