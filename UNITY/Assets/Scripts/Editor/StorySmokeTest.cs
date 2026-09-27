using System;
using System.Collections.Generic;
using System.IO;
using Game.Battle;
using Game.Core;
using Game.Core.Save;
using Game.Story;
using Game.Town;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 剧情框架冒烟测试：条件与效果语法、对话推进/条件跳过/选项过滤/成环保护、第一幕任务推进、存档 v4 与迁移。
    /// </summary>
    public static class StorySmokeTest
    {
        private const string Quest = "QST_Act1_Signal";

        [MenuItem("Game/剧情冒烟测试")]
        public static void Run()
        {
            GameDB.Reload();
            StoryService.AbortDialogue();
            var s = new PlayerState(100) { party = DemoFactory.CreateParty() };

            // 条件
            Check(Conditions.Evaluate("", s), "空条件成立");
            Check(!Conditions.Evaluate("flag:a", s) && Conditions.Evaluate("!flag:a", s), "标记与取反");
            Check(Conditions.Evaluate("gold>=100 & level>=1 & town:TWN_Zhanqiao", s), "金钱、等级、城镇组合");
            Check(!Conditions.Evaluate("gold>=101", s), "金钱不足");
            Check(Conditions.Evaluate("has:WPN_Cannon_75", s) && !Conditions.Evaluate("has:WPN_SE_Missile", s), "拥有部件（含战车上）");
            Check(Conditions.Evaluate($"quest:{Quest}=none", s), "任务未开始");
            Check(Conditions.Evaluate("bounty:ENM_Bounty_IronCrab=wanted", s), "赏金首通缉中");
            Check(!Conditions.Evaluate("unknown:x", s), "无法识别的条件不成立");

            // 效果
            Effects.Apply("set:a; gold:+50; give:WPN_SE_Missile; clear:b", s);
            Check(s.flags.Contains("a") && s.Gold == 150 && Conditions.Evaluate("has:WPN_SE_Missile", s), "设置标记、加钱、给部件");
            Effects.Apply("gold:-999; clear:a", s);
            Check(s.Gold == 0 && !s.flags.Contains("a"), "扣钱到 0、清除标记");

            // 对话：条件跳过、选项过滤、效果执行、选择后结束
            var dlg = ScriptableObject.CreateInstance<DialogueData>();
            dlg.dialogueId = "DLG_Test";
            dlg.nodes = new List<DialogueData.Node>
            {
                new() { nodeId = "n0", textKey = "t0", next = "n1", condition = "flag:never" },
                new() { nodeId = "n1", speaker = "CHR_Hunter", textKey = "t1", effects = "set:talked",
                        choices = new List<DialogueData.Choice>
                        {
                            new() { textKey = "c_rich", next = "n2", condition = "gold>=1000" },
                            new() { textKey = "c_yes", next = "n2" },
                            new() { textKey = "c_no", next = "" },
                        } },
                new() { nodeId = "n2", textKey = "t2", effects = $"quest:start:{Quest}" },
            };
            var r = StoryService.StartDialogue(s, dlg);
            Check(r != null && r.Current.nodeId == "n1", "条件不满足的节点被跳过");
            Check(StoryService.StartDialogue(s, dlg) == null, "对话进行中不能再开");
            Check(s.flags.Contains("talked") && r.Choices.Count == 2, "进入节点执行效果并过滤选项");
            Check(r.Continue() == OpResult.ChoiceRequired, "有选项时必须选择");
            Check(r.Choose(5) == OpResult.NotFound, "越界选项");
            Check(r.Choose(0) == OpResult.Ok && r.Current.nodeId == "n2", "选择后跳转");
            Check(StoryService.Quest(s, Quest) != null, "对话效果开始任务");
            Check(r.Continue() == OpResult.Ok && r.IsEnded && StoryService.ActiveDialogue == null, "对话结束");

            // 成环保护
            var loop = ScriptableObject.CreateInstance<DialogueData>();
            loop.dialogueId = "DLG_Loop";
            loop.nodes = new List<DialogueData.Node>
            {
                new() { nodeId = "a", next = "b", condition = "flag:never" },
                new() { nodeId = "b", next = "a", condition = "flag:never" },
            };
            var lr = StoryService.StartDialogue(s, loop);
            Check(lr != null && lr.IsEnded && StoryService.ActiveDialogue == null, "条件成环时安全结束");

            // 第一幕任务：听到信号 → 击败铁钳巨蟹（自动得到日志）→ 回报
            var q = StoryService.Quest(s, Quest);
            Check(q.step == 0 && q.state == QuestState.Active, "任务第 0 步");
            Check(StoryService.StartQuest(s, Quest) == OpResult.NothingToDo, "不能重复开始");
            StoryService.SetFlag(s, "act1_heard_signal", true);
            StoryService.Refresh(s);
            Check(q.step == 1, "听到信号后推进");
            BountyService.OnVictory(s, new List<Combatant> { GameDB.Enemy("ENM_Bounty_IronCrab").CreateCombatant() });
            Check(q.step == 2 && s.flags.Contains("act1_got_log"), "击败赏金首后推进并执行步骤效果");
            Effects.Apply("set:act1_reported", s);
            Check(q.state == QuestState.Completed && s.flags.Contains("act1_done"), "回报后任务完成");

            // 存档 v4 往返与 v3 迁移
            string oldDir = SaveSystem.Directory;
            SaveSystem.Directory = Path.Combine(Path.GetTempPath(), "game_story_test");
            try
            {
                Check(SaveSystem.Save(0, s, "Field", Vector3.zero) == OpResult.Ok, "存档");
                Check(SaveSystem.Load(0, out _, out var rs) == OpResult.Ok && rs.flags.Contains("act1_done")
                      && StoryService.Quest(rs, Quest)?.state == QuestState.Completed, "标记与任务还原");
                var v3 = JsonUtility.FromJson<SaveData>(File.ReadAllText(SaveSystem.PathOf(0)));
                v3.version = 3;
                v3.flags = null;
                v3.quests = null;
                File.WriteAllText(SaveSystem.PathOf(1), JsonUtility.ToJson(v3));
                Check(SaveSystem.Load(1, out _, out var r3) == OpResult.Ok && r3.flags.Count == 0 && r3.quests.Count == 0, "v3 存档迁移");
            }
            finally
            {
                SaveSystem.Directory = oldDir;
            }
            Debug.Log("[StoryTest] 全部通过");
        }

        private static void Check(bool ok, string what)
        {
            if (!ok) throw new Exception($"[StoryTest] 失败：{what}");
        }
    }
}
