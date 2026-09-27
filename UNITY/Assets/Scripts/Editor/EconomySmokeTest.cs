using System;
using Game.Core;
using Game.Economy;
using Game.Tank;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 商店与车库冒烟测试：覆盖买入、卖出、装配、换底盘、卸下、改造、修理的正常与异常路径。
    /// 任一断言失败即抛异常。
    /// </summary>
    public static class EconomySmokeTest
    {
        [MenuItem("Game/经济冒烟测试")]
        public static void Run()
        {
            GameDB.Reload();
            var shop = GameDB.Shop("SHP_Zhanqiao");
            Check(shop != null && shop.goods.Count > 0, "商店 SHP_Zhanqiao 已导入");

            var s = new PlayerState(1000);
            var tank = DemoFactory.CreateTank();
            int goldEvents = 0, invEvents = 0;
            Action<int, int> onGold = (_, _) => goldEvents++;
            Action onInv = () => invEvents++;
            EconomyEvents.GoldChanged += onGold;
            EconomyEvents.InventoryChanged += onInv;
            try
            {
                // 买：金钱不足 / 不在商店 / 成功
                var heavy = GameDB.Part("TNK_Chassis_Heavy");
                Check(ShopService.Buy(s, shop, heavy, out _) == OpResult.NotEnoughGold, "金钱不足时拒绝购买");
                Check(s.Gold == 1000 && s.inventory.Count == 0, "拒绝购买不扣钱");
                Check(ShopService.Buy(s, shop, GameDB.Part("TNK_Engine_V8"), out _) == OpResult.NotInShop, "商店不卖的物品拒绝");
                s.Gold = 20000;
                Check(ShopService.Buy(s, shop, heavy, out var heavyInst) == OpResult.Ok, "购买重型底盘");
                Check(s.Gold == 20000 - heavy.price && s.inventory.Contains(heavyInst), "扣钱并进入背包");
                Check(ShopService.Buy(s, shop, GameDB.Part("WPN_SE_Missile"), out var se) == OpResult.Ok, "购买导弹");

                // 装：武器孔类型不符 / 孔不存在 / 换底盘后 SE 可装
                Check(GarageService.Equip(s, tank, se, 1) == OpResult.HoleTypeMismatch, "SE 不能装副炮孔");
                Check(GarageService.Equip(s, tank, se, 5) == OpResult.HoleNotFound, "不存在的武器孔");
                var oldChassis = tank.chassis;
                Check(GarageService.Equip(s, tank, heavyInst, 0) == OpResult.Ok, "换装重型底盘");
                Check(tank.chassis == heavyInst && s.inventory.Contains(oldChassis) && !s.inventory.Contains(heavyInst), "旧底盘回背包");
                Check(tank.weapons.Count == 3 && tank.weapons[0] != null && tank.weapons[1] != null, "兼容武器保留");
                Check(GarageService.Equip(s, tank, se, 2) == OpResult.Ok && tank.weapons[2] == se, "SE 装入第 3 孔");

                // 卸：引擎不能卸 / 成功 / 空位
                Check(GarageService.Unequip(s, tank, PartSlot.Engine) == OpResult.CannotRemove, "引擎不能卸下");
                var mg = tank.weapons[1];
                Check(GarageService.Unequip(s, tank, PartSlot.Weapon, 1) == OpResult.Ok && s.inventory.Contains(mg), "卸下机枪回背包");
                Check(GarageService.Unequip(s, tank, PartSlot.Weapon, 1) == OpResult.SlotEmpty, "空位不能再卸");

                // 卖：已装备的不能卖 / 成功
                Check(ShopService.Sell(s, tank.weapons[0]) == OpResult.NotInInventory, "已装备部件不能直接卖");
                int before = s.Gold;
                Check(ShopService.Sell(s, oldChassis) == OpResult.Ok && s.Gold == before + ShopService.SellPrice(oldChassis), "卖出旧底盘");

                // 改造：成功 / 满级 / 金钱不足
                var cannon = tank.weapons[0];
                Check(GarageService.Upgrade(s, cannon, tank) == OpResult.Ok && cannon.upgradeLevel == 1, "主炮改造 Lv1");
                cannon.upgradeLevel = cannon.data.maxUpgradeLevel;
                Check(GarageService.Upgrade(s, cannon, tank) == OpResult.MaxUpgrade, "满级拒绝改造");
                s.Gold = 0;
                Check(GarageService.Upgrade(s, tank.engine, tank) == OpResult.NotEnoughGold, "没钱不能改造");

                // 修理：没钱时不修 / 有钱修好并扣费
                tank.engine.condition = PartCondition.Broken;
                Check(GarageService.Repair(s, tank, out int cost) == OpResult.NotEnoughGold
                      && tank.engine.condition == PartCondition.Broken, "没钱不修理");
                s.Gold = cost;
                Check(GarageService.Repair(s, tank, out _) == OpResult.Ok && s.Gold == 0 && tank.engine.IsFunctional, "修理成功并扣费");

                Check(goldEvents > 0 && invEvents > 0, "经济事件已触发");
                Debug.Log($"[EconomyTest] 全部通过（金钱事件 {goldEvents}，背包事件 {invEvents}）");
            }
            finally
            {
                EconomyEvents.GoldChanged -= onGold;
                EconomyEvents.InventoryChanged -= onInv;
            }
        }

        private static void Check(bool ok, string what)
        {
            if (!ok) throw new Exception($"[EconomyTest] 失败：{what}");
        }
    }
}
