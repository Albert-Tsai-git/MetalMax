using System.Collections.Generic;
using System.Linq;
using Game.Battle;
using Game.Core;
using UnityEngine;

namespace Game.Town
{
    /// <summary>赏金首状态</summary>
    public enum BountyState
    {
        Wanted,     // 通缉中
        Defeated,   // 已击败，待领赏
        Claimed,    // 已领赏
    }

    /// <summary>赏金首：通缉列表、击败登记、领取赏金。赏金首来自 enemies.csv 中 bounty_flag=1 的敌人。</summary>
    public static class BountyService
    {
        /// <summary>全部赏金首（按 ID 排序）</summary>
        public static IEnumerable<EnemyData> All() => GameDB.AllEnemies().Where(e => e.isBounty).OrderBy(e => e.enemyId);

        public static BountyState StateOf(PlayerState s, string enemyId)
        {
            var r = s.bounties.Find(b => b.id == enemyId);
            return r == null ? BountyState.Wanted : r.state;
        }

        /// <summary>战斗胜利后登记被击败的赏金首</summary>
        public static void OnVictory(PlayerState s, IEnumerable<Combatant> enemies)
        {
            foreach (var id in enemies.Select(e => e.id).Where(id => !string.IsNullOrEmpty(id)).Distinct())
            {
                var data = GameDB.Enemy(id);
                if (data == null || !data.isBounty || StateOf(s, id) != BountyState.Wanted) continue;
                Set(s, id, BountyState.Defeated);
                Debug.Log($"[Bounty] 击败赏金首 {id}");
                TownEvents.RaiseBountyDefeated(id);
            }
        }

        /// <summary>在有赏金办事处的城镇领取全部已击败赏金首的赏金</summary>
        public static OpResult ClaimAll(PlayerState s, string townId, out int total)
        {
            total = 0;
            var town = GameDB.Town(townId);
            if (town == null) return OpResult.NotFound;
            if (!town.hasBountyOffice) return OpResult.NoBountyOffice;

            var ready = s.bounties.Where(b => b.state == BountyState.Defeated).ToList();
            if (ready.Count == 0) return OpResult.NothingToDo;
            foreach (var b in ready)
            {
                int gold = GameDB.Enemy(b.id)?.bounty ?? 0;
                b.state = BountyState.Claimed;
                s.Gold += gold;
                total += gold;
                Debug.Log($"[Bounty] 领取 {b.id} 赏金 {gold}G");
                TownEvents.RaiseBountyClaimed(b.id, gold);
            }
            return OpResult.Ok;
        }

        private static void Set(PlayerState s, string id, BountyState state)
        {
            var r = s.bounties.Find(b => b.id == id);
            if (r == null) s.bounties.Add(new BountyRecord { id = id, state = state });
            else r.state = state;
        }
    }

    /// <summary>单个赏金首的进度（可存档）</summary>
    [System.Serializable]
    public class BountyRecord
    {
        public string id;
        public BountyState state;
    }
}
