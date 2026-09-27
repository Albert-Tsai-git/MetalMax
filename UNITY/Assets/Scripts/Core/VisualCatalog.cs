using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 按数据 ID 查找美术资源（约定路径见 docs/INTERFACE.md）：
    /// 模型 Resources/Visuals/{ID}，图标 Resources/Icons/{ID}。找不到返回 null。
    /// </summary>
    public static class VisualCatalog
    {
        private static readonly Dictionary<string, GameObject> _models = new();
        private static readonly Dictionary<string, Sprite> _icons = new();

        public static GameObject Model(string id) => Load(_models, "Visuals/", id);
        public static Sprite Icon(string id) => Load(_icons, "Icons/", id);

        private static T Load<T>(Dictionary<string, T> cache, string folder, string id) where T : Object
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (!cache.TryGetValue(id, out var asset))
            {
                asset = Resources.Load<T>(folder + id);
                cache[id] = asset;
            }
            return asset;
        }
    }
}
