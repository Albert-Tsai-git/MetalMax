using System;
using System.Collections.Generic;
using System.Linq;
using Game.Battle;
using Game.Core;
using Game.Economy;
using Game.Tank;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>平衡机制冒烟测试：敌人分组与一组/全体攻击范围、属性弱点/抗性/免疫、弹药补给收费。</summary>
    public static class BalanceMechanicsSmokeTest
    {
        [MenuItem("Game/平衡机制冒烟测试")]
        public static void Run()
        {
            GameDB.Reload();

            // 分组：同种敌人为一组，按首次出现顺序编号
            var party = DemoFactory.CreateParty();
            var hunter = party[0];
            hunter.speed = 999;
            var enemies = new List<Combatant> { Tough("ENM_Ant"), Tough("ENM_Ant"), Tough("ENM_Dog"), Tough("ENM_Ant"), Tough("ENM_TurretBug") };
            var b = new BattleSystem(party, enemies, 3);
            Check(enemies.Select(e => e.groupIndex).SequenceEqual(new[] { 0, 0, 1, 0, 2 }) && b.AliveGroupCount == 3, "同种敌人编为一组");

            // 一组武器只打目标所在组；全体武器打全部
            var mg = hunter.tank.weapons[1];
            var hit = Hits(party, enemies, BattleAction.Fire(hunter, mg, enemies[1]));
            Check(hit.SetEquals(new[] { enemies[0], enemies[1], enemies[3] }.Where(e => e.IsAlive)), "副炮打蚁群整组，不波及其他组");
            hit = Hits(party, enemies, BattleAction.Fire(hunter, mg, enemies[2]));
            Check(hit.SetEquals(new[] { enemies[2] }), "副炮打野狗组只打野狗");
            var se = new PartInstance(GameDB.Part("WPN_SE_Missile"));
            hunter.tank.TryEquip(new PartInstance(GameDB.Part("TNK_Chassis_Heavy")), 0, out _);
            Check(hunter.tank.TryEquip(se, 2, out _) == OpResult.Ok, "换重型底盘装 SE");
            se.currentAmmo = 4;
            hit = Hits(party, enemies, BattleAction.Fire(hunter, se, enemies[0]));
            Check(hit.Count == enemies.Count(e => e.IsAlive), "SE 打全部敌人");

            // 属性倍率
            var ant = GameDB.Enemy("ENM_Ant");
            var turret = GameDB.Enemy("ENM_TurretBug");
            var crab = GameDB.Enemy("ENM_Bounty_IronCrab");
            Check(Mathf.Approximately(ant.ElementRate(Element.Fire), 1.5f), "蚁怕火");
            Check(Mathf.Approximately(turret.ElementRate(Element.Fire), 0.5f) && Mathf.Approximately(turret.ElementRate(Element.Electric), 1.5f), "炮台虫抗火怕电");
            Check(Mathf.Approximately(crab.ElementRate(Element.Electric), 1.5f) && Mathf.Approximately(crab.ElementRate(Element.Normal), 1f), "巨蟹怕电，实弹正常");
            var rng = new System.Random(1);
            Check(DamageCalculator.Damage(100, 0, rng, 0f) == 0 && DamageCalculator.Damage(100, 0, rng, 1.5f) > DamageCalculator.Damage(100, 0, new System.Random(1)), "免疫为 0、弱点伤害更高");

            // 属性事件：火焰喷射器打蚁群
            var flame = new PartInstance(GameDB.Part("WPN_Flamethrower"));
            Check(hunter.tank.TryEquip(flame, 1, out _) == OpResult.Ok, "装火焰喷射器");
            int weakHits = 0;
            Action<Combatant, Element, float> onElem = (_, e, r) => { if (e == Element.Fire && r > 1f) weakHits++; };
            BattleEvents.ElementHit += onElem;
            Hits(party, enemies, BattleAction.Fire(hunter, flame, enemies[0]));
            BattleEvents.ElementHit -= onElem;
            Check(weakHits > 0 && weakHits <= enemies.Count(e => e.IsAlive && e.id == "ENM_Ant"), "命中蚁群时触发弱点事件");

            // 弹药补给：修理不再补弹，补弹按发收费，副炮不收费
            var s = new PlayerState(0) { party = party };
            var tank = hunter.tank;
            var cannon = tank.weapons[0];
            var cannonData = (WeaponData)cannon.data;
            cannon.currentAmmo = 1;
            se.currentAmmo = 0;
            tank.RepairAll();
            Check(cannon.currentAmmo == 1, "修理不补弹药");
            int expect = (cannonData.maxAmmo - 1) * cannonData.ammoPrice + ((WeaponData)se.data).maxAmmo * ((WeaponData)se.data).ammoPrice;
            Check(tank.RefillCost() == expect && expect > 0, "补弹费用 = 缺少发数 × 单价（无限弹药不计）");
            Check(GarageService.Refill(s, tank, out _) == OpResult.NotEnoughGold && cannon.currentAmmo == 1, "钱不够不补");
            s.Gold = expect;
            Check(GarageService.Refill(s, tank, out int cost) == OpResult.Ok && cost == expect && s.Gold == 0
                  && cannon.currentAmmo == cannonData.maxAmmo, "补满并扣费");
            Check(GarageService.Refill(s, tank, out _) == OpResult.NothingToDo, "满弹时无需补给");

            Debug.Log("[BalanceTest] 全部通过");
        }

        /// <summary>高血量、不会被一回合打死的敌人，便于观察命中范围</summary>
        private static Combatant Tough(string id)
        {
            var c = GameDB.Enemy(id).CreateCombatant();
            c.maxHp = c.hp = 100000;
            c.evade = 0;
            c.speed = 0;
            c.attack = 0;
            return c;
        }

        /// <summary>执行一次行动，返回本次被命中的敌人</summary>
        private static HashSet<Combatant> Hits(List<Combatant> party, List<Combatant> enemies, BattleAction action)
        {
            var hit = new HashSet<Combatant>();
            var attempted = new HashSet<Combatant>();
            Action<Combatant, Combatant, int, bool> onHit = (a, t, _, _) => { if (a == action.actor) hit.Add(t); };
            Action<Combatant, Combatant> onMiss = (a, t) => { if (a == action.actor) attempted.Add(t); };
            BattleEvents.Hit += onHit;
            BattleEvents.Missed += onMiss;
            try
            {
                var b = new BattleSystem(party, enemies, 11);
                b.SubmitCommands(new List<BattleAction> { action });
            }
            finally
            {
                BattleEvents.Hit -= onHit;
                BattleEvents.Missed -= onMiss;
            }
            // 未命中的也在攻击范围内：只检验范围，不检验命中率
            hit.UnionWith(attempted);
            return hit;
        }

        private static void Check(bool ok, string what)
        {
            if (!ok) throw new Exception($"[BalanceTest] 失败：{what}");
        }
    }
}
