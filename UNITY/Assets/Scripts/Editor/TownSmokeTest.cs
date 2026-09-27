using System;
using System.Collections.Generic;
using System.IO;
using Game.Battle;
using Game.Core;
using Game.Core.Save;
using Game.Town;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 城镇与赏金首冒烟测试：进城、住宿、赏金首击败登记与领赏、存档 v2 与 v1 迁移。
    /// </summary>
    public static class TownSmokeTest
    {
        private const string Town = PlayerState.DefaultTown;
        private const string Crab = "ENM_Bounty_IronCrab";

        [MenuItem("Game/城镇冒烟测试")]
        public static void Run()
        {
            GameDB.Reload();
            var town = GameDB.Town(Town);
            Check(town != null && town.hasBountyOffice && GameDB.Shop(town.shopId) != null, "城镇已导入并关联商店");

            var s = new PlayerState(5) { party = DemoFactory.CreateParty(), lastTown = "" };
            int claimed = 0;
            Action<string, int> onClaim = (_, g) => claimed += g;
            TownEvents.BountyClaimed += onClaim;
            try
            {
                // 进城
                Check(TownService.Enter(s, "TWN_None") == OpResult.NotFound && s.lastTown == "", "未知城镇拒绝");
                Check(TownService.Enter(s, Town) == OpResult.Ok && s.lastTown == Town, "进城登记最后到访城镇");

                // 住宿：满血无需住 / 钱不够 / 成功
                Check(TownService.Rest(s, Town) == OpResult.NothingToDo, "满血无需住宿");
                s.party[0].hp = 1;
                Check(TownService.Rest(s, Town) == OpResult.NotEnoughGold && s.party[0].hp == 1, "钱不够不能住宿");
                s.Gold = 100;
                Check(TownService.Rest(s, Town) == OpResult.Ok && s.party[0].hp == s.party[0].maxHp && s.Gold == 100 - town.innPrice, "住宿回满并扣费");

                // 赏金首：普通敌人不登记 / 击败登记 / 领赏 / 重复领取
                var crab = GameDB.Enemy(Crab);
                Check(crab != null && crab.isBounty && crab.bounty > 0, "赏金首数据存在");
                Check(BountyService.StateOf(s, Crab) == BountyState.Wanted, "初始为通缉中");
                BountyService.OnVictory(s, new List<Combatant> { GameDB.Enemy("ENM_Ant").CreateCombatant() });
                Check(s.bounties.Count == 0, "普通敌人不登记");
                BountyService.OnVictory(s, new List<Combatant> { crab.CreateCombatant() });
                Check(BountyService.StateOf(s, Crab) == BountyState.Defeated, "击败后待领赏");
                int before = s.Gold;
                Check(BountyService.ClaimAll(s, Town, out int total) == OpResult.Ok && total == crab.bounty && s.Gold == before + crab.bounty, "领取赏金");
                Check(BountyService.StateOf(s, Crab) == BountyState.Claimed && claimed == crab.bounty, "已领赏并触发事件");
                Check(BountyService.ClaimAll(s, Town, out _) == OpResult.NothingToDo, "不能重复领取");
                BountyService.OnVictory(s, new List<Combatant> { crab.CreateCombatant() });
                Check(BountyService.StateOf(s, Crab) == BountyState.Claimed, "已领赏的赏金首不再登记");

                // 存档 v2 往返 与 v1 迁移
                string oldDir = SaveSystem.Directory;
                SaveSystem.Directory = Path.Combine(Path.GetTempPath(), "game_town_test");
                try
                {
                    Check(SaveSystem.Save(0, s, "Field", Vector3.zero) == OpResult.Ok, "存档");
                    Check(SaveSystem.Load(0, out var d, out var r) == OpResult.Ok && d.version == SaveData.CurrentVersion, "读档");
                    Check(r.lastTown == Town && BountyService.StateOf(r, Crab) == BountyState.Claimed, "城镇与赏金进度还原");

                    var v1 = JsonUtility.FromJson<SaveData>(File.ReadAllText(SaveSystem.PathOf(0)));
                    v1.version = 1;
                    v1.lastTown = null;
                    v1.bounties = null;
                    File.WriteAllText(SaveSystem.PathOf(1), JsonUtility.ToJson(v1));
                    Check(SaveSystem.Load(1, out var d1, out var r1) == OpResult.Ok && d1.version == SaveData.CurrentVersion
                          && r1.lastTown == PlayerState.DefaultTown && r1.bounties.Count == 0, "v1 存档迁移到当前版本");
                }
                finally
                {
                    SaveSystem.Directory = oldDir;
                }
                Debug.Log("[TownTest] 全部通过");
            }
            finally
            {
                TownEvents.BountyClaimed -= onClaim;
            }
        }

        private static void Check(bool ok, string what)
        {
            if (!ok) throw new Exception($"[TownTest] 失败：{what}");
        }
    }
}
