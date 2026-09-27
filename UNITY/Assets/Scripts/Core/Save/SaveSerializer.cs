using System;
using System.Collections.Generic;
using Game.Battle;
using Game.Tank;
using UnityEngine;

namespace Game.Core.Save
{
    /// <summary>PlayerState ↔ SaveData 的纯转换，不做文件读写，便于测试</summary>
    public static class SaveSerializer
    {
        public static SaveData ToSave(PlayerState s, string scene, Vector3 position)
        {
            var d = new SaveData
            {
                savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                gold = s.Gold,
                exp = s.exp,
                scene = scene,
                position = position,
                lastTown = s.lastTown,
                bounties = new List<Game.Town.BountyRecord>(s.bounties),
                flags = new List<string>(s.flags),
                quests = new List<Game.Story.QuestRecord>(s.quests),
                items = s.items.ConvertAll(i => new Game.Items.ItemStack { id = i.id, count = i.count }),
                vehicle = new VehicleState { parked = s.vehicle.parked, scene = s.vehicle.scene, position = s.vehicle.position, yaw = s.vehicle.yaw },
            };
            foreach (var c in s.party) d.party.Add(ToSave(c));
            foreach (var p in s.inventory) d.inventory.Add(ToSave(p));
            return d;
        }

        /// <summary>还原玩家状态；找不到的部件 ID 会被跳过并记录警告</summary>
        public static PlayerState FromSave(SaveData d)
        {
            Migrate(d);
            var s = new PlayerState(d.gold) { exp = d.exp, lastTown = d.lastTown };
            if (d.bounties != null) s.bounties.AddRange(d.bounties);
            if (d.flags != null) s.flags.AddRange(d.flags);
            s.vehicle = d.vehicle ?? new VehicleState();
            if (d.quests != null) s.quests.AddRange(d.quests);
            if (d.items != null)
                foreach (var i in d.items)
                    if (i.count > 0 && GameDB.Item(i.id) != null) s.items.Add(i);
                    else Debug.LogWarning($"[Save] 存档中的道具 {i.id} 无效，已跳过");
            foreach (var c in d.party) s.party.Add(FromSave(c));
            foreach (var p in d.inventory)
            {
                var inst = FromSave(p);
                if (inst != null) s.inventory.Add(inst);
            }
            return s;
        }

        /// <summary>旧版本存档升级到当前结构</summary>
        private static void Migrate(SaveData d)
        {
            if (d.version > SaveData.CurrentVersion)
                Debug.LogWarning($"[Save] 存档版本 {d.version} 高于当前 {SaveData.CurrentVersion}，按当前结构读取");
            if (d.version < 2)
            {
                // v1 → v2：新增最后到访城镇与赏金首进度
                d.lastTown = PlayerState.DefaultTown;
                d.bounties ??= new List<Game.Town.BountyRecord>();
            }
            if (d.version < 3)
            {
                // v2 → v3：新增角色等级与经验，旧存档从 1 级开始
                foreach (var c in d.party) { c.level = 1; c.exp = 0; }
            }
            if (d.version < 4)
            {
                // v3 → v4：新增剧情标记与任务进度
                d.flags ??= new List<string>();
                d.quests ??= new List<Game.Story.QuestRecord>();
            }
            if (d.version < 5) d.items ??= new List<Game.Items.ItemStack>(); // v4 → v5：新增道具
            if (d.version < 6) d.vehicle = new VehicleState(); // v5 → v6：新增战车停放，旧存档视为在车上
            if (string.IsNullOrEmpty(d.lastTown)) d.lastTown = PlayerState.DefaultTown;
            d.version = SaveData.CurrentVersion;
        }

        private static CombatantSave ToSave(Combatant c) => new()
        {
            id = c.id,
            level = c.level, exp = c.exp,
            maxHp = c.maxHp, hp = c.hp, attack = c.attack, defense = c.defense, speed = c.speed, evade = c.evade,
            inTank = c.inTank,
            hasTank = c.tank != null,
            tank = c.tank == null ? null : ToSave(c.tank),
        };

        private static Combatant FromSave(CombatantSave c)
        {
            var r = new Combatant
            {
                id = c.id, name = TextDB.Name(c.id), side = Side.Player,
                level = Mathf.Max(1, c.level), exp = Mathf.Max(0, c.exp),
                maxHp = c.maxHp, hp = c.hp, attack = c.attack, defense = c.defense, speed = c.speed, evade = c.evade,
                inTank = c.inTank,
            };
            if (c.hasTank && c.tank != null) r.tank = FromSave(c.tank);
            if (r.tank == null) r.inTank = false;
            return r;
        }

        private static TankSave ToSave(TankLoadout t)
        {
            var d = new TankSave
            {
                tankName = t.tankName,
                chassis = ToSave(t.chassis), engine = ToSave(t.engine), cUnit = ToSave(t.cUnit),
                armorTons = t.armorTons, currentSp = t.currentSp,
            };
            foreach (var w in t.weapons) d.weapons.Add(ToSave(w));
            return d;
        }

        private static TankLoadout FromSave(TankSave d)
        {
            // 直接赋值而不走 TryEquip，保证武器孔顺序、装甲与 SP 原样还原
            var t = new TankLoadout
            {
                tankName = d.tankName,
                chassis = FromSave(d.chassis), engine = FromSave(d.engine), cUnit = FromSave(d.cUnit),
                armorTons = d.armorTons, currentSp = d.currentSp,
            };
            t.weapons = new List<PartInstance>();
            foreach (var w in d.weapons) t.weapons.Add(FromSave(w));
            // 底盘数据若已改变武器孔数量，补齐或截断
            if (t.chassis?.data is ChassisData cd)
                while (t.weapons.Count < cd.weaponHoles.Length) t.weapons.Add(null);
            return t;
        }

        private static PartSave ToSave(PartInstance p) => p == null
            ? new PartSave { id = "" }
            : new PartSave { id = p.data.partId, upgradeLevel = p.upgradeLevel, condition = (int)p.condition, ammo = p.currentAmmo };

        private static PartInstance FromSave(PartSave p)
        {
            if (p == null || string.IsNullOrEmpty(p.id)) return null;
            var data = GameDB.Part(p.id);
            if (data == null)
            {
                Debug.LogWarning($"[Save] 存档中的部件 {p.id} 已不存在，已跳过");
                return null;
            }
            return new PartInstance(data)
            {
                upgradeLevel = Mathf.Clamp(p.upgradeLevel, 0, data.maxUpgradeLevel),
                condition = (PartCondition)Mathf.Clamp(p.condition, 0, (int)PartCondition.Broken),
                currentAmmo = p.ammo,
            };
        }
    }
}
