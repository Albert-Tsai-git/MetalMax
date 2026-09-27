using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Story
{
    /// <summary>对话：节点图。由 Data/Text/dialogue.csv 生成（内容归 Codex，格式见 docs/INTERFACE.md I-16）。</summary>
    public class DialogueData : ScriptableObject
    {
        [Serializable]
        public class Choice
        {
            public string textKey;
            public string next;
            public string condition;
        }

        [Serializable]
        public class Node
        {
            public string nodeId;
            [Tooltip("说话人数据 ID（CHR_ 等），旁白为空")]
            public string speaker;
            public string textKey;
            [Tooltip("没有选项时的下一节点，空为结束")]
            public string next;
            [Tooltip("进入条件，不满足则直接跳到 next")]
            public string condition;
            [Tooltip("显示本节点时执行的效果")]
            public string effects;
            public List<Choice> choices = new();
        }

        public string dialogueId;
        public List<Node> nodes = new();

        public Node Find(string nodeId) => nodes.Find(n => n.nodeId == nodeId);
    }
}
