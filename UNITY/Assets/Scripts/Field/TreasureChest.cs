using System;
using Game.Core;
using Game.Story;
using UnityEngine;

namespace Game.Field
{
    /// <summary>
    /// 宝箱：玩家进入触发区时打开，执行内容效果（Effects 语法，如 gold:+300、item:ITM_Tonic:2、give:部件ID）。
    /// 打开后设置标记 chest:{chestId}，随存档保存，不会再次打开；已打开的宝箱隐藏灰盒外观。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TreasureChest : MonoBehaviour
    {
        public string chestId;
        public string contents;
        [Tooltip("打开后隐藏的外观物体")]
        public GameObject visual;

        /// <summary>宝箱被打开：宝箱 ID、内容效果串</summary>
        public static event Action<string, string> Opened;

        public static string FlagOf(string chestId) => $"chest:{chestId}";

        /// <summary>打开宝箱的纯逻辑：未开过则执行内容并记录</summary>
        public static OpResult Open(PlayerState s, string chestId, string contents)
        {
            if (string.IsNullOrEmpty(chestId)) return OpResult.NotFound;
            if (s.flags.Contains(FlagOf(chestId))) return OpResult.NothingToDo;
            StoryService.SetFlag(s, FlagOf(chestId), true);
            Effects.Apply(contents, s);
            Debug.Log($"[Chest] 打开 {chestId}：{contents}");
            Opened?.Invoke(chestId, contents);
            return OpResult.Ok;
        }

        private void Start() => RefreshVisual();

        private void Reset() => GetComponent<Collider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent(out FieldPlayerController _)) return;
            if (Open(GameSession.Instance.State, chestId, contents) == OpResult.Ok) RefreshVisual();
        }

        private void RefreshVisual()
        {
            if (visual != null) visual.SetActive(!GameSession.Instance.State.flags.Contains(FlagOf(chestId)));
        }
    }
}
