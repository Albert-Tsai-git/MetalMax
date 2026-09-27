using System;
using System.Collections.Generic;
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

        /// <summary>
        /// 装配部件。武器需要指定武器孔下标，其他部件忽略 holeIndex。
        /// 返回被替换下来的旧部件（可放回背包），失败返回 false。
        /// </summary>
        public bool TryEquip(PartInstance part, int holeIndex, out PartInstance replaced, out string error)
        {
            replaced = null;
            error = null;

            switch (part.data)
            {
                case ChassisData cd:
                    replaced = chassis;
                    chassis = part;
                    // 底盘变了，武器孔要重建；不兼容的武器被卸下
                    var old = weapons;
                    weapons = new List<PartInstance>(new PartInstance[cd.weaponHoles.Length]);
                    for (int i = 0; i < old.Count && i < weapons.Count; i++)
                        if (old[i] != null && ((WeaponData)old[i].data).weaponType == cd.weaponHoles[i])
                            weapons[i] = old[i];
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
                    if (chassis == null) { error = "没有底盘"; return false; }
                    var holes = ((ChassisData)chassis.data).weaponHoles;
                    if (holeIndex < 0 || holeIndex >= holes.Length) { error = "武器孔不存在"; return false; }
                    if (holes[holeIndex] != wd.weaponType) { error = $"该孔只能装 {holes[holeIndex]}"; return false; }
                    replaced = weapons[holeIndex];
                    weapons[holeIndex] = part;
                    break;

                default:
                    error = "未知部件类型";
                    return false;
            }

            ClampArmor();
            Debug.Log($"[Tank] {tankName} 装配 {part.data.DisplayName}，总重 {TotalWeight:F1}/{LoadCapacity:F1}t");
            OnChanged?.Invoke();
            return true;
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
                    Debug.Log($"[Tank] {tankName} 的 {hit.data.DisplayName} → {hit.condition}");
                }
            }

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

        /// <summary>修理全部部件并补满 SP、弹药，返回费用</summary>
        public int RepairAll(int costPerDamaged = 100, int costPerBroken = 500)
        {
            int cost = 0;
            foreach (var p in AllParts())
            {
                if (p.condition == PartCondition.Damaged) cost += costPerDamaged;
                else if (p.condition == PartCondition.Broken) cost += costPerBroken;
                p.Repair();
                p.Refill();
            }
            currentSp = MaxSp;
            Debug.Log($"[Tank] {tankName} 全面修理，费用 {cost}G");
            OnChanged?.Invoke();
            return cost;
        }

        /// <summary>改造部件。改造后会超重则拒绝。</summary>
        public bool TryUpgrade(PartInstance part, ref int money, out string error)
        {
            error = null;
            int cost = part.NextUpgradeCost;
            if (cost < 0) { error = "已达最大改造等级"; return false; }
            if (money < cost) { error = "金钱不足"; return false; }

            // 引擎改造会提升载重，不需要检查超重；其他部件改造会增重
            if (part.data is not EngineData && PartsWeight + part.data.weightPerUpgrade > LoadCapacity)
            {
                error = "改造后超重";
                return false;
            }

            money -= cost;
            part.upgradeLevel++;
            ClampArmor();
            Debug.Log($"[Tank] {part.data.DisplayName} 改造至 Lv{part.upgradeLevel}，花费 {cost}G");
            OnChanged?.Invoke();
            return true;
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
