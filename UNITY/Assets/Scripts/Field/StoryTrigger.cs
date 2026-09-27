using Game.Core;
using Game.Story;
using UnityEngine;

namespace Game.Field
{
    /// <summary>
    /// 剧情触发区：玩家进入且条件成立时，开始对话或直接执行效果。
    /// once 为真时触发后设置标记 trigger:{triggerId}，之后不再触发（随存档保存）。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class StoryTrigger : MonoBehaviour
    {
        [Tooltip("唯一 ID，用于一次性标记")]
        public string triggerId;
        [Tooltip("触发条件（Conditions 语法），空为总是")]
        public string condition;
        [Tooltip("要开始的对话 ID，空则只执行效果")]
        public string dialogueId;
        [Tooltip("触发时执行的效果（Effects 语法）")]
        public string effects;
        public bool once = true;

        private string DoneFlag => $"trigger:{triggerId}";

        private void Reset() => GetComponent<Collider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent(out FieldPlayerController _)) return;
            var s = GameSession.Instance.State;
            if (once && s.flags.Contains(DoneFlag)) return;
            if (!Conditions.Evaluate(condition, s)) return;
            // 对话内容缺失时只记录警告，效果照常执行，避免剧情流程卡死
            if (!string.IsNullOrEmpty(dialogueId) && StoryService.StartDialogue(s, dialogueId) == null)
                Debug.LogWarning($"[Story] 触发器 {triggerId} 无法开始对话 {dialogueId}（对话缺失或已有对话进行中）");
            Debug.Log($"[Story] 触发器 {triggerId}");
            if (once) StoryService.SetFlag(s, DoneFlag, true);
            Effects.Apply(effects, s);
        }
    }
}
