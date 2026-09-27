using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Tank
{
    /// <summary>
    /// 一辆战车的完整配置与运行时状态：装配、载重、SP、受损、修理、改造。
    /// 纯逻辑类，不依赖场景，方便单元测试。
    /// </summary>
    [Serializable]
    public class TankLoadout
    {
        /// <summary>1 吨剩余载重可兑换的 SP</summary>
        public const int SpPerTon = 100;

        public string tankName = "战车";
        public PartInstance chassis;
        public PartInstance engine;
        public PartInstance cUnit;
        /// <summary>武器列表，下标与底盘的 weaponHoles 对应，空位为 null</summary>
        public List<PartInstance> weapons = new();
        /// <summary>玩家装填的装甲吨数</summary>
        public float armorTons;
        public int currentSp;

        public event Action OnChanged;

        #region 重量与 SP

        /// <summary>部件总重（不含装甲）</summary>
        public float PartsWeight
        {
            get
            {
                float w = 0f;
                if (chassis != null) w += chassis.Weight;
                if (engine != null) w += engine.Weight;
                if (cUnit != null) w += cUnit.Weight;
                foreach (var wp in weapons) if (wp != null) w += wp.Weight;
                return w;
            }
        }

        public float TotalWeight => PartsWeight + armorTons;
        public float LoadCapacity => engine?.LoadCapacity ?? 0f;
        public float FreeLoad => LoadCapacity - TotalWeight;
        public int MaxSp => Mathf.FloorToInt(armorTons * SpPerTon);

        /// <summary>能否行驶：有底盘、引擎可用、且不超重</summary>
        public bool CanMove =>
            chassis != null && engine != null && engine.IsFunctional && TotalWeight <= LoadCapacity;

        /// <summary>把所有剩余载重都换成装甲（修理厂“装甲补满”）</summary>
        public void FillArmor()
        {
            armorTons = Mathf.Max(0f, LoadCapacity - PartsWeight);
            currentSp = MaxSp;
            Debug.Log($"[Tank] {tankName} 装甲补满：{armorTons:F1}t / SP {currentSp}");
            OnChanged?.Invoke();
        }

        #endregion

        #region 装配

        /// <summary>换底盘时因武器孔不兼容被卸下的武器，由调用方取走放回背包</summary>
        private readonly List<PartInstance> _detached = new();

        public List<PartInstance> TakeDetachedWeapons()
        {
            var list = new List<PartInstance>(_detached);
            _detached.Clear();
            return list;
        }

        /// <summary>
        /// 装配部件。武器需要指定武器孔下标，其他部件忽略 holeIndex。
        /// replaced 为被替换下来的旧部件（可放回背包）。
        /// </summary>
        public OpResult TryEquip(PartInstance part, int holeIndex, out PartInstance replaced)
        {
            replaced = null;

            switch (part.data)
            {
                case ChassisData cd:
                    replaced = chassis;
                    chassis = part;
                    // 底盘变了，武器孔要重建；不兼容的武器被卸下
                    var old = weapons;
                    weapons = new List<PartInstance>(new PartInstance[cd.weaponHoles.Length]);
                    for (int i = 0; i < old.Count; i++)
                    {
                        if (old[i] == null) continue;
                        if (i < weapons.Count && ((WeaponData)old[i].data).weaponType == cd.weaponHoles[i])
                            weapons[i] = old[i];
                        else _detached.Add(old[i]);
                    }
                    break;

                case EngineData:
                    replaced = engine;
                    engine = part;
                    break;

                case CUnitData:
                    replaced = cUnit;
                    cUnit = part;
                    break;

                case WeaponData wd:
                    if (chassis == null) return OpResult.NoChassis;
                    var holes = ((ChassisData)chassis.data).weaponHoles;
                    if (holeIndex < 0 || holeIndex >= holes.Length) return OpResult.HoleNotFound;
                    if (holes[holeIndex] != wd.weaponType) return OpResult.HoleTypeMismatch;
                    replaced = weapons[holeIndex];
                    weapons[holeIndex] = part;
                    break;

                default:
                    return OpResult.UnknownPart;
            }

            ClampArmor();
            Debug.Log($"[Tank] {tankName} 装配 {part.data.partId}，总重 {TotalWeight:F1}/{LoadCapacity:F1}t");
            OnChanged?.Invoke();
            return OpResult.Ok;
        }

        /// <summary>卸下 C 装置或武器；底盘和引擎只能替换，不能卸下</summary>
        public OpResult TryRemove(PartSlot slot, int holeIndex, out PartInstance removed)
        {
            removed = null;
            switch (slot)
            {
                case PartSlot.CUnit:
                    removed = cUnit;
                    cUnit = null;
                    break;
                case PartSlot.Weapon:
                    if (holeIndex < 0 || holeIndex >= weapons.Count) return OpResult.HoleNotFound;
                    removed = weapons[holeIndex];
                    weapons[holeIndex] = null;
                    break;
                default:
                    return OpResult.CannotRemove;
            }
            if (removed == null) return OpResult.SlotEmpty;
            Debug.Log($"[Tank] {tankName} 卸下 {removed.data.partId}");
            OnChanged?.Invoke();
            return OpResult.Ok;
        }

        /// <summary>超重时自动削减装甲，保证不超载</summary>
        private void ClampArmor()
        {
            float maxArmor = Mathf.Max(0f, LoadCapacity - PartsWeight);
            if (armorTons > maxArmor) armorTons = maxArmor;
            currentSp = Mathf.Min(currentSp, MaxSp);
        }

        #endregion

        #region 战斗受损

        /// <summary>SP 归零后，每次受击损坏部件的概率</summary>
        public const float PartDamageChance = 0.5f;

        /// <summary>
        /// 战车受到伤害：先扣 SP，SP 不足时溢出伤害按概率损坏随机部件。
        /// 返回本次被损坏的部件（没有则为 null）。
        /// </summary>
        public PartInstance TakeDamage(int damage, System.Random rng)
        {
            int overflow = damage - currentSp;
            currentSp = Mathf.Max(0, currentSp - damage);

            PartInstance hit = null;
            if (overflow > 0 && rng.NextDouble() < PartDamageChance)
            {
                var candidates = new List<PartInstance>();
                foreach (var p in AllParts()) if (p.IsFunctional) candidates.Add(p);
                if (candidates.Count > 0)
                {
                    hit = candidates[rng.Next(candidates.Count)];
                    hit.condition = hit.condition == PartCondition.Normal
                        ? PartCondition.Damaged
                        : PartCondition.Broken;
                    Debug.Log($"[Tank] {tankName} 的 {hit.data.partId} → {hit.condition}");
                }
            }

            OnChanged?.Invoke();
            return hit;
        }

        /// <summary>随机损坏一个仍可用的部件（技能附带效果），没有可损坏的返回 null</summary>
        public PartInstance BreakRandomPart(System.Random rng)
        {
            var candidates = new List<PartInstance>();
            foreach (var p in AllParts()) if (p.IsFunctional) candidates.Add(p);
            if (candidates.Count == 0) return null;
            var hit = candidates[rng.Next(candidates.Count)];
            hit.condition = hit.condition == PartCondition.Normal ? PartCondition.Damaged : PartCondition.Broken;
            Debug.Log($"[Tank] {tankName} 的 {hit.data.partId} 被技能破坏 → {hit.condition}");
            OnChanged?.Invoke();
            return hit;
        }

        /// <summary>所有部件是否全部大破（战车失去战斗能力）</summary>
        public bool IsDestroyed
        {
            get
            {
                foreach (var p in AllParts()) if (p.IsFunctional) return false;
                return true;
            }
        }

        #endregion

        #region 修理与改造

        /// <summary>修理单价（数值平衡阶段再调）</summary>
        public const int RepairCostDamaged = 100;
        public const int RepairCostBroken = 500;

        /// <summary>全面修理的费用</summary>
        public int RepairCost()
        {
            int cost = 0;
            foreach (var p in AllParts())
            {
                if (p.condition == PartCondition.Damaged) cost += RepairCostDamaged;
                else if (p.condition == PartCondition.Broken) cost += RepairCostBroken;
            }
            return cost;
        }

        /// <summary>修理全部部件并补满 SP、弹药，返回费用（扣费由调用方负责）</summary>
        public int RepairAll()
        {
            int cost = RepairCost();
            foreach (var p in AllParts())
            {
                p.Repair();
                p.Refill();
            }
            currentSp = MaxSp;
            Debug.Log($"[Tank] {tankName} 全面修理，费用 {cost}G");
            OnChanged?.Invoke();
            return cost;
        }

        /// <summary>改造该部件后部件总重是否仍不超过载重（引擎改造提升载重，总是允许）</summary>
        public bool CanUpgradeWithoutOverweight(PartInstance part) =>
            part.data is EngineData || PartsWeight + part.data.weightPerUpgrade <= LoadCapacity;

        /// <summary>部件属性在外部被修改（如改造）后调用：修正装甲并通知外观刷新</summary>
        public void NotifyChanged()
        {
            ClampArmor();
            OnChanged?.Invoke();
        }

        #endregion

        public IEnumerable<PartInstance> AllParts()
        {
            if (chassis != null) yield return chassis;
            if (engine != null) yield return engine;
            if (cUnit != null) yield return cUnit;
            foreach (var w in weapons) if (w != null) yield return w;
        }
    }
}
