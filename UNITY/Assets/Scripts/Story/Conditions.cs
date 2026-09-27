using System;
using Game.Core;
using Game.Town;
using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// 剧情条件（语法见 docs/INTERFACE.md I-16）。多个条件用 &amp; 连接，全部成立才成立；空串恒成立。
    ///   flag:名称            标记已设置；前加 ! 表示取反（!flag:名称）
    ///   quest:ID=active|done|none   任务状态
    ///   quest:ID&gt;=步骤    任务进行到第几步及以后（或已完成）
    ///   bounty:敌人ID=wanted|defeated|claimed
    ///   gold&gt;=数量 / level&gt;=等级（队伍最高等级）/ town:城镇ID（最后到访城镇）
    ///   has:部件ID           背包或战车上有该部件
    /// </summary>
    public static class Conditions
    {
        public static bool Evaluate(string expr, PlayerState s)
        {
            if (string.IsNullOrWhiteSpace(expr)) return true;
            foreach (var raw in expr.Split('&'))
            {
                var term = raw.Trim();
                if (term == "") continue;
                bool negate = term.StartsWith("!");
                if (negate) term = term.Substring(1).Trim();
                if (EvaluateTerm(term, s) == negate) return false;
            }
            return true;
        }

        private static bool EvaluateTerm(string t, PlayerState s)
        {
            if (t.StartsWith("flag:")) return s.flags.Contains(t.Substring(5));
            if (t.StartsWith("town:")) return s.lastTown == t.Substring(5);
            if (t.StartsWith("has:")) return HasPart(s, t.Substring(4));
            if (TryCompare(t, "gold>=", out int g)) return s.Gold >= g;
            if (TryCompare(t, "level>=", out int lv)) return s.party.Exists(c => c.level >= lv);
            if (t.StartsWith("quest:"))
            {
                var body = t.Substring(6);
                int ge = body.IndexOf(">=", StringComparison.Ordinal);
                if (ge > 0 && int.TryParse(body.Substring(ge + 2), out int step))
                {
                    var q = StoryService.Quest(s, body.Substring(0, ge));
                    return q != null && (q.state == QuestState.Completed || q.step >= step);
                }
                var (id, want) = Split(body);
                var rec = StoryService.Quest(s, id);
                string state = rec == null ? "none" : rec.state == QuestState.Completed ? "done" : "active";
                return state == want;
            }
            if (t.StartsWith("bounty:"))
            {
                var (id, want) = Split(t.Substring(7));
                return string.Equals(BountyService.StateOf(s, id).ToString(), want, StringComparison.OrdinalIgnoreCase);
            }
            Debug.LogError($"[Story] 无法识别的条件：{t}");
            return false;
        }

        private static bool TryCompare(string t, string prefix, out int value)
        {
            value = 0;
            return t.StartsWith(prefix) && int.TryParse(t.Substring(prefix.Length), out value);
        }

        private static (string, string) Split(string body)
        {
            int eq = body.IndexOf('=');
            return eq < 0 ? (body, "") : (body.Substring(0, eq), body.Substring(eq + 1).ToLowerInvariant());
        }

        private static bool HasPart(PlayerState s, string partId)
        {
            if (s.inventory.Exists(p => p.data.partId == partId)) return true;
            foreach (var c in s.party)
                if (c.tank != null)
                    foreach (var p in c.tank.AllParts())
                        if (p.data.partId == partId) return true;
            return false;
        }
    }
}
