using Game.Core;
using UnityEngine;

namespace Game.Town
{
    /// <summary>城镇服务：进出城镇、住宿。纯逻辑，操作 PlayerState。</summary>
    public static class TownService
    {
        /// <summary>进入城镇：记为最后到访城镇（全灭后在此复活）</summary>
        public static OpResult Enter(PlayerState s, string townId)
        {
            if (GameDB.Town(townId) == null) return OpResult.NotFound;
            s.lastTown = townId;
            Debug.Log($"[Town] 进入 {townId}");
            TownEvents.RaiseEntered(townId);
            Game.Story.StoryService.Refresh(s);
            return OpResult.Ok;
        }

        public static void Leave(string townId)
        {
            Debug.Log($"[Town] 离开 {townId}");
            TownEvents.RaiseLeft(townId);
        }

        /// <summary>住宿：付费后全员复活并 HP 回满</summary>
        public static OpResult Rest(PlayerState s, string townId)
        {
            var town = GameDB.Town(townId);
            if (town == null) return OpResult.NotFound;
            bool needed = s.party.Exists(p => p.hp < p.maxHp);
            if (!needed) return OpResult.NothingToDo;
            if (!s.TrySpend(town.innPrice)) return OpResult.NotEnoughGold;
            foreach (var p in s.party) p.hp = p.maxHp;
            Debug.Log($"[Town] {townId} 住宿，花费 {town.innPrice}G");
            TownEvents.RaiseRested(townId, town.innPrice);
            return OpResult.Ok;
        }
    }
}
