using System;

namespace Game.Story
{
    public enum QuestState { Active, Completed }

    /// <summary>单个任务的进度（可存档）</summary>
    [Serializable]
    public class QuestRecord
    {
        public string id;
        public int step;
        public QuestState state;
    }
}
