using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 玩家可见文本的唯一读取入口。键格式见 docs/INTERFACE.md。
    /// 缺失的键返回键名本身，便于发现漏填。
    /// </summary>
    public static class TextDB
    {
        public const string DefaultLanguage = "zh";

        private static Dictionary<string, string> _map;
        private static readonly HashSet<string> _missing = new();

        public static string Language { get; private set; } = DefaultLanguage;

        public static void SetLanguage(string lang)
        {
            Language = lang;
            _map = null;
            _missing.Clear();
        }

        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            EnsureLoaded();
            if (_map.TryGetValue(key, out var text)) return text;
            if (_missing.Add(key)) Debug.LogWarning($"[Text] 缺少文本键 {key}（语言 {Language}）");
            return key;
        }

        /// <summary>数据 ID 的名称，键为 {ID}.name</summary>
        public static string Name(string id) => Get(id + ".name");

        /// <summary>数据 ID 的说明，键为 {ID}.desc；没有说明时返回空串</summary>
        public static string Desc(string id)
        {
            EnsureLoaded();
            return _map.TryGetValue(id + ".desc", out var text) ? text : "";
        }

        private static void EnsureLoaded()
        {
            if (_map != null) return;
            _map = new Dictionary<string, string>();
            var table = Resources.Load<TextTable>($"Text/TextTable_{Language}");
            if (table == null)
            {
                Debug.LogWarning($"[Text] 未找到 Resources/Text/TextTable_{Language}，请先执行 Game/导入数值表");
                return;
            }
            foreach (var e in table.entries) _map[e.key] = e.text;
            Debug.Log($"[Text] 已加载 {Language} 文本 {_map.Count} 条");
        }
    }
}
