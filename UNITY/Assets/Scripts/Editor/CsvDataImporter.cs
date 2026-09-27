using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Game.Battle;
using Game.Core;
using Game.Tank;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 数值表导入：读取工程根目录 Data/ 下的 CSV（Excel 另存为“CSV UTF-8”），
    /// 生成或更新 Assets/GameData/ 下的 ScriptableObject。
    /// 以 id 列作为资产文件名：已有资产只更新字段，GUID 不变，场景里的引用不会断。
    /// 文本表 Data/Text/text_{lang}.csv（key,text）生成 Assets/Resources/Text/TextTable_{lang}.asset。
    /// </summary>
    public static class CsvDataImporter
    {
        private const string DataDir = "Data";
        private const string OutDir = "Assets/GameData";
        private const string TextDir = "Data/Text";
        private const string TextOutDir = "Assets/Resources/Text";

        [MenuItem("Game/导入数值表 (CSV)")]
        public static void ImportAll()
        {
            int n = 0;
            n += Import("parts.csv", $"{OutDir}/Parts", CreatePart);
            n += Import("enemies.csv", $"{OutDir}/Enemies", CreateEnemy);
            n += ImportTexts();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            TextDB.SetLanguage(TextDB.Language); // 清除运行时文本缓存
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
            foreach (var file in new[] { "parts.csv", "enemies.csv" })
            {
                string path = Path.Combine(DataDir, file);
                if (File.Exists(path)) foreach (var r in ReadCsv(path)) if (r.Str("id") != "") ids.Add(r.Str("id"));
            }

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
