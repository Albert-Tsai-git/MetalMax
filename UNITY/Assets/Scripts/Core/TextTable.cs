using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 文本表：由 CsvDataImporter 从 Data/Text/text_{lang}.csv 生成，放在 Resources/Text/ 下。
    /// </summary>
    public class TextTable : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public string key;
            [TextArea] public string text;
        }

        public List<Entry> entries = new();
    }
}
