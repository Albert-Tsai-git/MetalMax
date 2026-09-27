using System;
using System.IO;
using Game.Core;
using Game.Core.Save;
using Game.Economy;
using Game.Tank;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 存档冒烟测试：在临时目录中存档、读档、比对，并覆盖空槽、越界槽、损坏文件、缺失部件 ID。
    /// </summary>
    public static class SaveSmokeTest
    {
        [MenuItem("Game/存档冒烟测试")]
        public static void Run()
        {
            string oldDir = SaveSystem.Directory;
            string dir = Path.Combine(Path.GetTempPath(), "game_save_test");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            SaveSystem.Directory = dir;
            try
            {
                GameDB.Reload();
                var s = new PlayerState(1234) { exp = 56, party = DemoFactory.CreateParty() };
                var tank = s.party[0].tank;
                tank.weapons[0].upgradeLevel = 2;
                tank.weapons[0].currentAmmo = 3;
                tank.engine.condition = PartCondition.Damaged;
                tank.currentSp -= 100;
                s.inventory.Add(new PartInstance(GameDB.Part("WPN_SE_Missile")) { upgradeLevel = 1 });

                Check(SaveSystem.Load(0, out _, out _) == OpResult.SlotEmpty, "空槽读档返回 SlotEmpty");
                Check(SaveSystem.Save(9, s, "Field", Vector3.zero) == OpResult.SlotNotFound, "越界槽位拒绝");
                Check(SaveSystem.Save(0, s, "Field", new Vector3(1, 2, 3)) == OpResult.Ok, "存档成功");
                Check(SaveSystem.Save(0, s, "Field", new Vector3(1, 2, 3)) == OpResult.Ok, "覆盖存档成功");

                Check(SaveSystem.Load(0, out var data, out var r) == OpResult.Ok, "读档成功");
                Check(data.scene == "Field" && data.position == new Vector3(1, 2, 3), "场景与位置还原");
                Check(r.Gold == 1234 && r.exp == 56 && r.party.Count == s.party.Count, "金钱、经验、队伍人数还原");
                var rt = r.party[0].tank;
                Check(rt != null && r.party[0].inTank && r.party[1].tank == null, "战车归属还原");
                Check(rt.chassis.data == tank.chassis.data && rt.weapons.Count == tank.weapons.Count, "底盘与武器孔还原");
                Check(rt.weapons[0].upgradeLevel == 2 && rt.weapons[0].currentAmmo == 3, "改造等级与弹药还原");
                Check(rt.engine.condition == PartCondition.Damaged && rt.currentSp == tank.currentSp, "损坏状态与 SP 还原");
                Check(r.inventory.Count == 1 && r.inventory[0].data.partId == "WPN_SE_Missile" && r.inventory[0].upgradeLevel == 1, "背包还原");
                Check(Math.Abs(rt.TotalWeight - tank.TotalWeight) < 0.001f, "总重一致");

                // 缺失部件 ID：跳过而不是崩溃
                string json = File.ReadAllText(SaveSystem.PathOf(0)).Replace("\"WPN_SE_Missile\"", "\"WPN_Removed\"");
                File.WriteAllText(SaveSystem.PathOf(1), json);
                Check(SaveSystem.Load(1, out _, out var r1) == OpResult.Ok && r1.inventory.Count == 0, "缺失部件被跳过");

                // 损坏文件
                File.WriteAllText(SaveSystem.PathOf(2), "{ 这不是 JSON");
                Check(SaveSystem.Load(2, out _, out _) == OpResult.Corrupted, "损坏存档返回 Corrupted");

                Check(SaveSystem.Delete(2) == OpResult.Ok && !SaveSystem.Exists(2), "删除存档");
                Debug.Log("[SaveTest] 全部通过");
            }
            finally
            {
                SaveSystem.Directory = oldDir;
            }
        }

        private static void Check(bool ok, string what)
        {
            if (!ok) throw new Exception($"[SaveTest] 失败：{what}");
        }
    }
}
