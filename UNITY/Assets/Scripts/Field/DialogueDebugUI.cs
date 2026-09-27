using Game.Core;
using Game.Story;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Field
{
    /// <summary>
    /// 原型调试用对话框与任务列表（IMGUI）。空格 / 回车推进，数字键选择。正式界面由 Codex 在表现层实现后删除。
    /// </summary>
    public class DialogueDebugUI : MonoBehaviour
    {
        private void Update()
        {
            var r = StoryService.ActiveDialogue;
            var kb = Keyboard.current;
            if (r == null || kb == null) return;
            if (r.Choices.Count == 0)
            {
                if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) r.Continue();
                return;
            }
            for (int i = 0; i < r.Choices.Count && i < 9; i++)
                if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) { r.Choose(i); return; }
        }

        private void OnGUI()
        {
            var s = GameSession.Instance.State;
            // 任务列表
            var y = Screen.height - 20 - 22 * s.quests.Count;
            foreach (var q in s.quests)
            {
                var data = GameDB.Quest(q.id);
                string step = q.state == QuestState.Completed ? "完成" : data?.StepText(q.step);
                GUI.Label(new Rect(10, y, 600, 22), $"{data?.DisplayName ?? q.id}：{step}");
                y += 22;
            }

            var r = StoryService.ActiveDialogue;
            if (r == null) return;
            GUILayout.BeginArea(new Rect(40, Screen.height - 260, Screen.width - 80, 220), GUI.skin.box);
            if (r.SpeakerName != "") GUILayout.Label($"<b>{r.SpeakerName}</b>");
            GUILayout.Label(r.Text);
            if (r.Choices.Count == 0) GUILayout.Label("（空格继续）");
            for (int i = 0; i < r.Choices.Count; i++)
                if (GUILayout.Button($"{i + 1}. {r.ChoiceText(i)}")) r.Choose(i);
            GUILayout.EndArea();
        }
    }
}
