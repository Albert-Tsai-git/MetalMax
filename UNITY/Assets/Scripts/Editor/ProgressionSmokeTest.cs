using System;
using System.IO;
using Game.Core;
using Game.Core.Save;
using Game.Progression;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>角色成长冒烟测试：经验曲线、连升多级、属性成长、倒下不得经验、满级封顶、存档 v3 与迁移。</summary>
    public static class ProgressionSmokeTest
    {
        [MenuItem("Game/成长冒烟测试")]
        public static void Run()
        {
            GameDB.Reload();
            var data = GameDB.Character("CHR_Hunter");
            Check(data != null && GameDB.Character("CHR_Mechanic") != null, "角色数据已导入");

            var s = new PlayerState(0) { party = DemoFactory.CreateParty() };
            var hunter = s.party[0];
            var mech = s.party[1];
            Check(hunter.level == 1 && hunter.maxHp == data.hp && hunter.tank != null, "初始队伍来自角色数据");

            int ups = 0;
            Action<Game.Battle.Combatant, int> onUp = (_, _) => ups++;
            ProgressionEvents.LeveledUp += onUp;
            try
            {
                int need1 = data.ExpToNext(1), need2 = data.ExpToNext(2);
                Check(need1 > 0 && need2 > need1, "经验曲线递增");
                Check(LevelService.GainExp(hunter, need1 - 1) == 0 && hunter.level == 1, "差 1 点不升级");
                Check(LevelService.GainExp(hunter, 0) == 0, "0 经验无效果");

                // 一次获得足以连升两级的经验
                hunter.exp = 0;
                int hp0 = hunter.maxHp, atk0 = hunter.attack;
                Check(LevelService.GainExp(hunter, need1 + need2 + 3) == 2 && hunter.level == 3 && hunter.exp == 3, "连升两级并保留余量");
                Check(hunter.maxHp == hp0 + 2 * data.hpPerLevel && hunter.attack == atk0 + 2 * data.attackPerLevel, "两级属性成长");
                Check(ups == 2, "逐级触发升级事件");
                Check(LevelService.ExpRemaining(hunter) == data.ExpToNext(3) - 3, "剩余经验正确");

                // 战斗经验：倒下的队员不获得
                mech.hp = 0;
                int mechExp = mech.exp, total0 = s.exp;
                LevelService.AwardBattleExp(s, 5);
                Check(mech.exp == mechExp && s.exp == total0 + 5, "倒下队员不得经验，总经验累计");

                // 满级封顶
                hunter.level = data.maxLevel - 1;
                hunter.exp = 0;
                LevelService.GainExp(hunter, int.MaxValue / 2);
                Check(hunter.level == data.maxLevel && hunter.exp == 0 && LevelService.ExpRemaining(hunter) == -1, "满级封顶");

                // 存档 v3 往返与 v2 迁移
                string oldDir = SaveSystem.Directory;
                SaveSystem.Directory = Path.Combine(Path.GetTempPath(), "game_progression_test");
                try
                {
                    hunter.level = 7;
                    hunter.exp = 11;
                    Check(SaveSystem.Save(0, s, "Field", Vector3.zero) == OpResult.Ok, "存档");
                    Check(SaveSystem.Load(0, out _, out var r) == OpResult.Ok && r.party[0].level == 7 && r.party[0].exp == 11, "等级经验还原");
                    var v2 = JsonUtility.FromJson<SaveData>(File.ReadAllText(SaveSystem.PathOf(0)));
                    v2.version = 2;
                    File.WriteAllText(SaveSystem.PathOf(1), JsonUtility.ToJson(v2));
                    Check(SaveSystem.Load(1, out _, out var r2) == OpResult.Ok && r2.party[0].level == 1 && r2.party[0].exp == 0, "v2 存档迁移为 1 级");
                }
                finally
                {
                    SaveSystem.Directory = oldDir;
                }
                Debug.Log("[ProgressionTest] 全部通过");
            }
            finally
            {
                ProgressionEvents.LeveledUp -= onUp;
            }
        }

        private static void Check(bool ok, string what)
        {
            if (!ok) throw new Exception($"[ProgressionTest] 失败：{what}");
        }
    }
}
