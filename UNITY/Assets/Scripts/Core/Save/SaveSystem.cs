using System;
using System.IO;
using UnityEngine;

namespace Game.Core.Save
{
    /// <summary>
    /// 存档读写：Application.persistentDataPath/save_{槽位}.json。
    /// 先写临时文件再替换，避免写到一半断电导致存档损坏。
    /// </summary>
    public static class SaveSystem
    {
        public const int SlotCount = 3;

        /// <summary>存档完成：槽位、结果</summary>
        public static event Action<int, OpResult> Saved;
        /// <summary>读档完成：槽位、结果</summary>
        public static event Action<int, OpResult> Loaded;

        /// <summary>测试时可改为临时目录</summary>
        public static string Directory { get; set; } = Application.persistentDataPath;

        public static string PathOf(int slot) => System.IO.Path.Combine(Directory, $"save_{slot}.json");

        public static bool Exists(int slot) => ValidSlot(slot) && File.Exists(PathOf(slot));

        public static OpResult Save(int slot, PlayerState state, string scene, Vector3 position)
        {
            var r = DoSave(slot, state, scene, position);
            Saved?.Invoke(slot, r);
            return r;
        }

        public static OpResult Load(int slot, out SaveData data, out PlayerState state)
        {
            var r = DoLoad(slot, out data, out state);
            Loaded?.Invoke(slot, r);
            return r;
        }

        /// <summary>只读取存档摘要（时间、金钱等），供存档列表显示</summary>
        public static SaveData Peek(int slot)
        {
            if (!Exists(slot)) return null;
            try { return JsonUtility.FromJson<SaveData>(File.ReadAllText(PathOf(slot))); }
            catch { return null; }
        }

        public static OpResult Delete(int slot)
        {
            if (!Exists(slot)) return OpResult.SlotEmpty;
            File.Delete(PathOf(slot));
            return OpResult.Ok;
        }

        private static OpResult DoSave(int slot, PlayerState state, string scene, Vector3 position)
        {
            if (!ValidSlot(slot)) return OpResult.SlotNotFound;
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                string json = JsonUtility.ToJson(SaveSerializer.ToSave(state, scene, position), true);
                string path = PathOf(slot), tmp = path + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
                Debug.Log($"[Save] 已存档到槽位 {slot}：{path}");
                return OpResult.Ok;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] 存档失败：{e.Message}");
                return OpResult.IoError;
            }
        }

        private static OpResult DoLoad(int slot, out SaveData data, out PlayerState state)
        {
            data = null;
            state = null;
            if (!ValidSlot(slot)) return OpResult.SlotNotFound;
            if (!File.Exists(PathOf(slot))) return OpResult.SlotEmpty;
            try
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(PathOf(slot)));
                if (data == null) return OpResult.Corrupted;
                state = SaveSerializer.FromSave(data);
                Debug.Log($"[Save] 已读取槽位 {slot}（{data.savedAt}）");
                return OpResult.Ok;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] 读档失败：{e.Message}");
                return OpResult.Corrupted;
            }
        }

        private static bool ValidSlot(int slot) => slot >= 0 && slot < SlotCount;
    }
}
