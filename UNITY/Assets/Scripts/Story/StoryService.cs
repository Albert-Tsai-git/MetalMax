using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Story
{
    /// <summary>剧情状态：标记、任务推进、对话启动。纯逻辑，操作 PlayerState。</summary>
    public static class StoryService
    {
        /// <summary>当前进行中的对话（同一时间只有一段）</summary>
        public static DialogueRunner ActiveDialogue { get; private set; }

        private static int _refreshDepth;

        public static void SetFlag(PlayerState s, string flag, bool on)
        {
            if (string.IsNullOrEmpty(flag) || s.flags.Contains(flag) == on) return;
            if (on) s.flags.Add(flag);
            else s.flags.Remove(flag);
            Debug.Log($"[Story] 标记 {flag} = {on}");
            StoryEvents.RaiseFlagChanged(flag, on);
        }

        public static QuestRecord Quest(PlayerState s, string questId) => s.quests.Find(q => q.id == questId);

        public static OpResult StartQuest(PlayerState s, string questId)
        {
            if (GameDB.Quest(questId) == null) return OpResult.NotFound;
            if (Quest(s, questId) != null) return OpResult.NothingToDo;
            s.quests.Add(new QuestRecord { id = questId, step = 0, state = QuestState.Active });
            Debug.Log($"[Story] 任务开始 {questId}");
            StoryEvents.RaiseQuestStarted(questId);
            Refresh(s);
            return OpResult.Ok;
        }

        /// <summary>
        /// 检查所有进行中任务：当前步骤条件成立则执行其效果并推进，可连续推进多步。
        /// 状态变化（战斗结束、进城、效果执行、读档）后调用。效果内部触发的嵌套调用直接返回，由最外层循环复查。
        /// </summary>
        public static void Refresh(PlayerState s)
        {
            if (_refreshDepth > 0) return;
            _refreshDepth++;
            try
            {
                bool changed = true;
                while (changed)
                {
                    changed = false;
                    foreach (var rec in s.quests.Where(q => q.state == QuestState.Active).ToList())
                    {
                        var data = GameDB.Quest(rec.id);
                        if (data == null || rec.step >= data.steps.Count) continue;
                        var step = data.steps[rec.step];
                        if (!Conditions.Evaluate(step.objective, s)) continue;

                        rec.step++;
                        changed = true;
                        Effects.Apply(step.effects, s);
                        if (rec.step >= data.steps.Count)
                        {
                            rec.state = QuestState.Completed;
                            Debug.Log($"[Story] 任务完成 {rec.id}");
                            StoryEvents.RaiseQuestCompleted(rec.id);
                        }
                        else
                        {
                            Debug.Log($"[Story] 任务 {rec.id} 推进到第 {rec.step} 步");
                            StoryEvents.RaiseQuestAdvanced(rec.id, rec.step);
                        }
                    }
                }
            }
            finally
            {
                _refreshDepth--;
            }
        }

        /// <summary>开始对话；已有对话进行中或对话不存在时返回 null</summary>
        public static DialogueRunner StartDialogue(PlayerState s, string dialogueId) =>
            StartDialogue(s, GameDB.Dialogue(dialogueId));

        public static DialogueRunner StartDialogue(PlayerState s, DialogueData data)
        {
            if (ActiveDialogue != null || data == null) return null;
            ActiveDialogue = new DialogueRunner(data, s);
            ActiveDialogue.Begin();
            return ActiveDialogue;
        }

        /// <summary>中止进行中的对话（切换场景、读档时）</summary>
        public static void AbortDialogue() => ActiveDialogue = null;

        internal static void OnDialogueEnded(DialogueRunner r)
        {
            if (ActiveDialogue == r) ActiveDialogue = null;
        }
    }
}
