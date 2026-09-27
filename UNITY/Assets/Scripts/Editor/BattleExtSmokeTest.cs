using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Battle;
using Game.Core;
using Game.Core.Save;
using Game.Economy;
using Game.Items;
using Game.Tank;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace Game.EditorTools
{
    /// <summary>
    /// 战斗扩展冒烟测试：敌人 AI 选目标、技能抽取、技能结算（穿透战车、破坏部件、自我回复、连击）、
    /// 道具（持有上限、各类效果、无效使用、战斗中使用）、商店购买道具、存档 v5。
    /// </summary>
    public static class BattleExtSmokeTest
    {
        [MenuItem("Game/战斗扩展冒烟测试")]
        public static void Run()
        {
            GameDB.Reload();
            var rng = new Random(7);
            var party = DemoFactory.CreateParty();
            var hunter = party[0];   // 乘车
            var mech = party[1];     // 步行

            // AI 选目标
            var turret = GameDB.Enemy("ENM_TurretBug").CreateCombatant();
            var ant = GameDB.Enemy("ENM_Ant").CreateCombatant();
            var dog = GameDB.Enemy("ENM_Dog").CreateCombatant();
            Check(Enumerable.Range(0, 50).All(_ => EnemyAI.Decide(turret, party, rng).targets[0] == hunter), "TankHunter 总是打乘车单位");
            Check(Enumerable.Range(0, 50).All(_ => EnemyAI.Decide(ant, party, rng).targets[0] == mech), "HumanHunter 总是打步行单位");
            mech.hp = 1;
            Check(Enumerable.Range(0, 50).All(_ => EnemyAI.Decide(dog, party, rng).targets[0] == mech), "Weakest 打 HP 比例最低的");
            mech.hp = mech.maxHp;

            // 回复技能只在低 HP 时使用；普通攻击与技能按权重混合
            Check(Enumerable.Range(0, 200).All(_ => EnemyAI.Decide(dog, party, rng).type != ActionType.Skill), "满血不用回复技能");
            dog.hp = 1;
            var dogActs = Enumerable.Range(0, 200).Select(_ => EnemyAI.Decide(dog, party, rng)).ToList();
            Check(dogActs.Any(a => a.type == ActionType.Skill && a.targets[0] == dog)
                  && dogActs.Any(a => a.type == ActionType.HumanAttack), "低血时回复自己，且仍会普通攻击");
            var crabActs = Enumerable.Range(0, 300).Select(_ => EnemyAI.Decide(GameDB.Enemy("ENM_Bounty_IronCrab").CreateCombatant(), party, rng)).ToList();
            Check(crabActs.Any(a => a.skill?.skillId == "SKL_CrabPinch") && crabActs.Any(a => a.skill?.skillId == "SKL_BrineSpray")
                  && crabActs.All(a => a.skill?.skillId != "SKL_ShellMend"), "赏金首技能按权重抽取，满血不回复");

            // 技能结算：以一个带战车的“敌人”作靶子，由我方步行角色施放测试技能
            var s = new PlayerState(0) { party = party };
            var dummy = DemoFactory.CreateParty()[0];
            dummy.side = Side.Enemy;
            dummy.speed = 0;
            dummy.maxHp = dummy.hp = 100000;
            dummy.attack = 0; // 靶子反击最多造成 1 点伤害
            mech.speed = 999;

            var pierce = Skill("T_Pierce", power: 100, pierce: true, accuracy: 100);
            int sp0 = dummy.tank.currentSp, hp0 = dummy.hp;
            RunTurn(s, party, dummy, BattleAction.Skill(mech, pierce, dummy));
            Check(dummy.tank.currentSp == sp0 && dummy.hp < hp0, "穿透战车直接伤害乘员");

            var breaker = Skill("T_Break", power: 1, partBreak: 100, accuracy: 100);
            int functional0 = dummy.tank.AllParts().Count(p => p.condition == PartCondition.Normal);
            RunTurn(s, party, dummy, BattleAction.Skill(mech, breaker, dummy));
            Check(dummy.tank.AllParts().Count(p => p.condition == PartCondition.Normal) == functional0 - 1, "技能必定破坏一个部件");

            var multi = Skill("T_Multi", power: 100, hits: 3, accuracy: 100);
            int hits = 0;
            Action<Combatant, Combatant, int, bool> onHit = (a, _, _, _) => { if (a == mech) hits++; };
            BattleEvents.Hit += onHit;
            RunTurn(s, party, dummy, BattleAction.Skill(mech, multi, dummy));
            BattleEvents.Hit -= onHit;
            Check(hits == 3, "三连击命中三次");

            mech.hp = 10;
            RunTurn(s, party, dummy, BattleAction.Skill(mech, Skill("T_Heal", heal: 50), mech));
            Check(mech.hp >= 10 + mech.maxHp / 2 - 1, "自我回复一半 HP（靶子反击至多 1 点）");

            // 道具
            Check(ItemService.Add(s, "ITM_None") == OpResult.NotFound, "未知道具");
            Check(ItemService.Use(s, "ITM_Tonic", mech, mech) == OpResult.NotInInventory, "没有道具不能用");
            Check(ItemService.Add(s, "ITM_Tonic", 99) == OpResult.Ok && ItemService.Add(s, "ITM_Tonic") == OpResult.StackFull, "持有上限 99");
            mech.hp = mech.maxHp;
            Check(ItemService.Use(s, "ITM_Tonic", mech, mech) == OpResult.NothingToDo && ItemService.Count(s, "ITM_Tonic") == 99, "满血使用无效且不消耗");
            mech.hp = 1;
            Check(ItemService.Use(s, "ITM_Tonic", mech, mech) == OpResult.Ok && mech.hp == 51 && ItemService.Count(s, "ITM_Tonic") == 98, "回复药回复 50 并消耗");
            ItemService.Add(s, "ITM_ReviveKit");
            Check(ItemService.Use(s, "ITM_ReviveKit", hunter, mech) == OpResult.NothingToDo, "复活药对活人无效");
            mech.hp = 0;
            Check(ItemService.Use(s, "ITM_ReviveKit", hunter, mech) == OpResult.Ok && mech.hp == 30 && ItemService.Count(s, "ITM_ReviveKit") == 0, "复活并移除空堆叠");
            ItemService.Add(s, "ITM_RepairPack");
            Check(ItemService.Use(s, "ITM_RepairPack", hunter, mech) == OpResult.NothingToDo, "没有战车不能修 SP");
            hunter.tank.currentSp = 0;
            Check(ItemService.Use(s, "ITM_RepairPack", hunter, hunter) == OpResult.Ok && hunter.tank.currentSp == Math.Min(150, hunter.tank.MaxSp), "修理包回复 SP");
            ItemService.Add(s, "ITM_AmmoCrate");
            var cannon = hunter.tank.weapons[0];
            cannon.currentAmmo = 0;
            Check(ItemService.Use(s, "ITM_AmmoCrate", hunter, hunter) == OpResult.Ok && cannon.currentAmmo == ((WeaponData)cannon.data).maxAmmo, "弹药箱补满");

            // 战斗中使用道具
            mech.hp = 1;
            int tonic0 = ItemService.Count(s, "ITM_Tonic");
            RunTurn(s, party, dummy, BattleAction.Item(mech, "ITM_Tonic", mech));
            Check(ItemService.Count(s, "ITM_Tonic") == tonic0 - 1 && mech.hp > 1, "战斗中使用道具");

            // 商店买道具
            var shop = GameDB.Shop("SHP_Zhanqiao");
            var revive = GameDB.Item("ITM_ReviveKit");
            Check(shop.items.Contains(revive), "商店卖道具");
            s.Gold = revive.price * 2;
            Check(ShopService.BuyItem(s, shop, revive, 3) == OpResult.NotEnoughGold, "钱不够买 3 个");
            Check(ShopService.BuyItem(s, shop, revive, 2) == OpResult.Ok && s.Gold == 0 && ItemService.Count(s, revive.itemId) == 2, "买 2 个");
            Check(ShopService.BuyItem(s, shop, GameDB.Item("ITM_Tonic"), 5) is OpResult.StackFull or OpResult.NotEnoughGold, "超上限或没钱");

            // 存档 v5
            string oldDir = SaveSystem.Directory;
            SaveSystem.Directory = Path.Combine(Path.GetTempPath(), "game_battleext_test");
            try
            {
                Check(SaveSystem.Save(0, s, "Field", Vector3.zero) == OpResult.Ok, "存档");
                Check(SaveSystem.Load(0, out _, out var r) == OpResult.Ok && ItemService.Count(r, "ITM_ReviveKit") == 2
                      && ItemService.Count(r, "ITM_Tonic") == ItemService.Count(s, "ITM_Tonic"), "道具还原");
            }
            finally
            {
                SaveSystem.Directory = oldDir;
            }
            Debug.Log("[BattleExtTest] 全部通过");
        }

        /// <summary>只让我方执行给定行动的一个回合（靶子无敌人数据，只会普通攻击）</summary>
        private static void RunTurn(PlayerState s, List<Combatant> party, Combatant dummy, BattleAction action)
        {
            var b = new BattleSystem(party, new List<Combatant> { dummy }, 1) { Inventory = s };
            b.SubmitCommands(new List<BattleAction> { action });
        }

        private static SkillData Skill(string id, int power = 0, int hits = 1, bool pierce = false, int partBreak = 0, int heal = 0, int accuracy = 0)
        {
            var k = ScriptableObject.CreateInstance<SkillData>();
            k.skillId = id;
            k.power = power;
            k.hits = hits;
            k.pierceTank = pierce;
            k.partBreakChance = partBreak;
            k.healPercent = heal;
            k.accuracyBonus = accuracy;
            return k;
        }

        private static void Check(bool ok, string what)
        {
            if (!ok) throw new Exception($"[BattleExtTest] 失败：{what}");
        }
    }
}
