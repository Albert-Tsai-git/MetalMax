using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// 任务：若干顺序步骤，每步有完成条件与完成效果。由 Data/quests.csv 生成。
    /// 文本键：{任务ID}.name、{任务ID}.step{序号}（序号从 0 开始，描述该步目标）。
    /// </summary>
    public class QuestData : ScriptableObject
    {
        [Serializable]
        public class Step
        {
            [Tooltip("完成条件（Conditions 语法）")]
            public string objective;
            [Tooltip("完成后执行的效果（Effects 语法）")]
            public string effects;
        }

        public string questId;
        public List<Step> steps = new();

        public string DisplayName => TextDB.Name(questId);
        public string StepText(int step) => TextDB.Get($"{questId}.step{step}");
    }
}
