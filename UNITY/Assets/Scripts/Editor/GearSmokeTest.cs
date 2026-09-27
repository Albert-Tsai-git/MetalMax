using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Battle;
using Game.Core;
using Game.Core.Save;
using Game.Equipment;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>人类装备冒烟测试：初始装备、装备/换装/卸下、角色限制、步行与乘车属性、商店买卖、群体武器与属性、存档 v7 与迁移</summary>
    public static class GearSmokeTest
    {
        [MenuItem("Game/人类装备冒烟测试")]
        public static void Run()
        {
            GameDB.Reload();
            var s = new PlayerState(0) { party = DemoFactory.CreateParty() };
            var hunter = s.party.First(p => p.id == "CHR_Hunter");
            var mech = s.party.First(p => p.id == "CHR_Mechanic");

            // 初始装备
            Check(hunter.GearIn(GearSlot.Weapon)?.gearId == "EQP_Knife" && hunter.GearIn(GearSlot.Body)?.gearId == "EQP_Jacket",
                "猎人初始带小刀、夹克");
            Check(hunter.FootAttack == hunter.attack + 6, "步行攻击 = 基础 + 武器");

            // 乘车时人类防具不生效，步行时生效
            hunter.inTank = true;
            int tankDef = hunter.TotalDefense;
            hunter.inTank = false;
            Check(hunter.TotalDefense == hunter.defense + 2 + 5, "步行防御 = 基础 + 头巾 + 夹克");
            Check(tankDef != hunter.TotalDefense, "乘车时用战车防御");

            // 买入、装备、换装、卸下
            var shop = GameDB.Shop("SHP_Zhanqiao");
            s.Gold = 5000;
            Check(EquipmentService.Buy(s, shop, GameDB.Gear("EQP_Pistol")) == OpResult.Ok
                  && EquipmentService.BagCount(s, "EQP_Pistol") == 1 && s.Gold == 4700, "买手枪进装备袋");
            Check(EquipmentService.Equip(s, hunter, "EQP_Pistol") == OpResult.Ok
                  && hunter.GearIn(GearSlot.Weapon).gearId == "EQP_Pistol"
                  && EquipmentService.BagCount(s, "EQP_Knife") == 1 && EquipmentService.BagCount(s, "EQP_Pistol") == 0,
                "装备手枪，小刀回到装备袋");
            Check(hunter.gear.Count(id => GameDB.Gear(id).slot == GearSlot.Weapon) == 1, "每个位置只有一件");
            Check(EquipmentService.Equip(s, hunter, "EQP_Pistol") == OpResult.NotInInventory, "袋中没有不能装备");
            Check(EquipmentService.Buy(s, shop, GameDB.Gear("EQP_Flamegun")) == OpResult.Ok
                  && EquipmentService.Equip(s, hunter, "EQP_Flamegun") == OpResult.CannotEquip, "猎人不能装备火焰枪");
            Check(EquipmentService.Equip(s, mech, "EQP_Flamegun") == OpResult.Ok, "机械师可装备火焰枪");
            Check(EquipmentService.Unequip(s, hunter, GearSlot.Head) == OpResult.Ok && hunter.GearIn(GearSlot.Head) == null
                  && EquipmentService.Unequip(s, hunter, GearSlot.Head) == OpResult.SlotEmpty, "卸下头巾");
            int gold = s.Gold;
            Check(EquipmentService.Sell(s, "EQP_Knife") == OpResult.Ok && s.Gold == gold + 25, "卖出小刀半价");
            Check(EquipmentService.Sell(s, "EQP_Pistol") == OpResult.NotInInventory, "身上装备不能直接卖");

            // 群体属性武器：火焰枪打整组，蚂蚁弱火
            mech.inTank = false;
            mech.speed = 999;
            var ants = Enumerable.Range(0, 3).Select(_ => GameDB.Enemy("ENM_Ant").CreateCombatant()).ToList();
            foreach (var e in ants) { e.maxHp = e.hp = 9999; e.evade = 0; e.attack = 0; }
            var bs = new BattleSystem(new List<Combatant> { mech }, ants, 7);
            int hits = 0;
            BattleEvents.Hit += OnHit;
            try
            {
                for (int i = 0; i < 5; i++)
                    bs.SubmitCommands(new List<BattleAction> { BattleAction.Attack(mech, ants[0]) });
            }
            finally { BattleEvents.Hit -= OnHit; }
            Check(ants.All(e => e.hp < e.maxHp), "火焰枪命中整组");
            Check(hits > 5, $"群体攻击多次命中（{hits}）");

            // 存档 v7 与 v6 迁移
            string oldDir = SaveSystem.Directory;
            SaveSystem.Directory = Path.Combine(Path.GetTempPath(), "game_gear_test");
            try
            {
                Check(SaveSystem.Save(0, s, "Field", Vector3.zero) == OpResult.Ok, "存档");
                Check(SaveSystem.Load(0, out _, out var r) == OpResult.Ok
                      && r.party.First(p => p.id == "CHR_Hunter").GearIn(GearSlot.Weapon)?.gearId == "EQP_Pistol"
                      && r.gearBag.Sum(g => g.count) == s.gearBag.Sum(g => g.count), "装备与装备袋还原");
                var v6 = JsonUtility.FromJson<SaveData>(File.ReadAllText(SaveSystem.PathOf(0)));
                v6.version = 6;
                foreach (var c in v6.party) c.gear = null;
                v6.gearBag = null;
                File.WriteAllText(SaveSystem.PathOf(1), JsonUtility.ToJson(v6));
                Check(SaveSystem.Load(1, out _, out var r6) == OpResult.Ok
                      && r6.party.First(p => p.id == "CHR_Hunter").GearIn(GearSlot.Weapon)?.gearId == "EQP_Knife"
                      && r6.gearBag.Count == 0, "v6 存档迁移补发初始装备");
            }
            finally
            {
                SaveSystem.Directory = oldDir;
            }
            Debug.Log("[GearTest] 全部通过");

            void OnHit(Combatant src, Combatant dst, int dmg, bool crit) { if (src == mech) hits++; }
        }

        private static void Check(bool ok, string what)
        {
            if (!ok) throw new Exception($"[GearTest] 失败：{what}");
        }
    }
}
