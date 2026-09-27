using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// 对话运行器：逐节点推进，处理条件跳过、效果执行与选项过滤。表现层读取 SpeakerName / Text / Choices，
    /// 调用 Continue() 或 Choose(i) 推进。
    /// </summary>
    public class DialogueRunner
    {
        /// <summary>防止条件跳转成环</summary>
        private const int MaxHops = 64;

        public DialogueData Data { get; }
        public DialogueData.Node Current { get; private set; }
        public bool IsEnded => Current == null;

        private readonly PlayerState _state;
        private List<DialogueData.Choice> _choices = new();

        public DialogueRunner(DialogueData data, PlayerState state)
        {
            Data = data;
            _state = state;
        }

        public string DialogueId => Data.dialogueId;
        public string Speaker => Current?.speaker ?? "";
        public string SpeakerName => string.IsNullOrEmpty(Speaker) ? "" : TextDB.Name(Speaker);
        public string Text => Current == null ? "" : TextDB.Get(Current.textKey);
        /// <summary>当前可选的选项（已按条件过滤）</summary>
        public IReadOnlyList<DialogueData.Choice> Choices => _choices;
        public string ChoiceText(int i) => TextDB.Get(_choices[i].textKey);

        internal void Begin()
        {
            StoryEvents.RaiseDialogueStarted(this);
            Enter(Data.nodes.Count > 0 ? Data.nodes[0].nodeId : null);
        }

        /// <summary>没有选项时推进到下一节点</summary>
        public OpResult Continue()
        {
            if (IsEnded) return OpResult.NothingToDo;
            if (_choices.Count > 0) return OpResult.ChoiceRequired;
            Enter(Current.next);
            return OpResult.Ok;
        }

        /// <summary>选择第 i 个选项</summary>
        public OpResult Choose(int i)
        {
            if (IsEnded) return OpResult.NothingToDo;
            if (i < 0 || i >= _choices.Count) return OpResult.NotFound;
            Enter(_choices[i].next);
            return OpResult.Ok;
        }

        private void Enter(string nodeId)
        {
            for (int hop = 0; hop < MaxHops; hop++)
            {
                var node = string.IsNullOrEmpty(nodeId) ? null : Data.Find(nodeId);
                if (node == null)
                {
                    if (!string.IsNullOrEmpty(nodeId)) Debug.LogError($"[Dialogue] {Data.dialogueId} 找不到节点 {nodeId}");
                    End();
                    return;
                }
                if (!Conditions.Evaluate(node.condition, _state))
                {
                    nodeId = node.next;
                    continue;
                }
                Current = node;
                _choices = node.choices.Where(c => Conditions.Evaluate(c.condition, _state)).ToList();
                Effects.Apply(node.effects, _state);
                StoryEvents.RaiseDialogueNode(this);
                return;
            }
            Debug.LogError($"[Dialogue] {Data.dialogueId} 条件跳转超过 {MaxHops} 次，疑似成环");
            End();
        }

        private void End()
        {
            Current = null;
            _choices.Clear();
            StoryService.OnDialogueEnded(this);
            StoryEvents.RaiseDialogueEnded(Data.dialogueId);
        }
    }
}
