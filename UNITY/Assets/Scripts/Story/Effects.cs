using Game.Core;
using Game.Tank;
using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// 剧情效果（语法见 docs/INTERFACE.md I-16）。多个效果用 ; 分隔，按顺序执行：
    ///   set:标记 / clear:标记
    ///   gold:+数量 / gold:-数量（不足时扣到 0）
    ///   give:部件ID          放入背包
    ///   item:道具ID[:数量]   获得道具（默认 1 个）
    ///   quest:start:任务ID
    ///   heal                 全员 HP 回满
    /// 执行后自动检查任务进度。
    /// </summary>
    public static class Effects
    {
        public static void Apply(string expr, PlayerState s)
        {
            if (string.IsNullOrWhiteSpace(expr)) return;
            foreach (var raw in expr.Split(';'))
            {
                var e = raw.Trim();
                if (e == "") continue;
                if (e.StartsWith("set:")) StoryService.SetFlag(s, e.Substring(4), true);
                else if (e.StartsWith("clear:")) StoryService.SetFlag(s, e.Substring(6), false);
                else if (e.StartsWith("gold:") && int.TryParse(e.Substring(5), out int g)) s.Gold = Mathf.Max(0, s.Gold + g);
                else if (e.StartsWith("give:"))
                {
                    var data = GameDB.Part(e.Substring(5));
                    if (data != null) s.AddToInventory(new PartInstance(data));
                }
                else if (e.StartsWith("item:"))
                {
                    var parts = e.Substring(5).Split(':');
                    int count = parts.Length > 1 && int.TryParse(parts[1], out int c) ? c : 1;
                    if (Game.Items.ItemService.Add(s, parts[0], count) != OpResult.Ok)
                        Debug.LogWarning($"[Story] 无法获得道具 {e}");
                }
                else if (e.StartsWith("quest:start:")) StoryService.StartQuest(s, e.Substring(12));
                else if (e == "heal") foreach (var c in s.party) c.hp = c.maxHp;
                else Debug.LogError($"[Story] 无法识别的效果：{e}");
            }
            StoryService.Refresh(s);
        }
    }
}
