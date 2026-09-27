using System;

namespace Game.Story
{
    /// <summary>
    /// 剧情事件（docs/INTERFACE.md I-16）。表现层只订阅；OnEnable 订阅、OnDisable 取消。
    /// </summary>
    public static class StoryEvents
    {
        /// <summary>剧情标记变化：标记名、是否设置</summary>
        public static event Action<string, bool> FlagChanged;
        /// <summary>任务开始：任务 ID</summary>
        public static event Action<string> QuestStarted;
        /// <summary>任务推进到新步骤：任务 ID、新步骤序号（从 0 开始）</summary>
        public static event Action<string, int> QuestAdvanced;
        /// <summary>任务完成：任务 ID</summary>
        public static event Action<string> QuestCompleted;
        /// <summary>对话开始：对话 ID</summary>
        public static event Action<DialogueRunner> DialogueStarted;
        /// <summary>显示对话节点（台词与可选项已就绪）</summary>
        public static event Action<DialogueRunner> DialogueNode;
        /// <summary>对话结束：对话 ID</summary>
        public static event Action<string> DialogueEnded;

        internal static void RaiseFlagChanged(string f, bool on) => FlagChanged?.Invoke(f, on);
        internal static void RaiseQuestStarted(string id) => QuestStarted?.Invoke(id);
        internal static void RaiseQuestAdvanced(string id, int step) => QuestAdvanced?.Invoke(id, step);
        internal static void RaiseQuestCompleted(string id) => QuestCompleted?.Invoke(id);
        internal static void RaiseDialogueStarted(DialogueRunner r) => DialogueStarted?.Invoke(r);
        internal static void RaiseDialogueNode(DialogueRunner r) => DialogueNode?.Invoke(r);
        internal static void RaiseDialogueEnded(string id) => DialogueEnded?.Invoke(id);
    }
}
