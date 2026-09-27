using Game.Core;
using Game.Story;
using UnityEngine;

namespace Game.Field
{
    /// <summary>NPC：靠近后按 E 对话（对话内容由 Codex 在 dialogue.csv 中编写）</summary>
    public class NpcTalk : Interactable
    {
        [Tooltip("NPC 数据 ID（文本键 {ID}.name）")]
        public string npcId;
        public string dialogueId;
        [Tooltip("可对话条件（Conditions 语法），空为总是")]
        public string condition;

        public override string PromptKey => "UI.Interact.Talk";

        public override bool CanInteract(PlayerState s) => Conditions.Evaluate(condition, s);

        public override void Interact(PlayerState s)
        {
            if (StoryService.StartDialogue(s, dialogueId) == null)
                Debug.LogWarning($"[Npc] {npcId} 的对话 {dialogueId} 不存在或已有对话进行中");
        }
    }
}
