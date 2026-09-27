using System;

namespace Game.Tank
{
    /// <summary>部件损坏状态</summary>
    public enum PartCondition
    {
        Normal,     // 正常
        Damaged,    // 损坏：性能减半
        Broken,     // 大破：完全失效
    }

    /// <summary>
    /// 部件实例：静态数据 + 玩家这件部件自己的改造等级、损坏状态、弹药等。
    /// 可序列化，方便存档。
    /// </summary>
    [Serializable]
    public class PartInstance
    {
        public TankPartData data;
        public int upgradeLevel;
        public PartCondition condition = PartCondition.Normal;
        public int currentAmmo;

        public PartInstance(TankPartData data)
        {
            this.data = data;
            if (data is WeaponData w) currentAmmo = w.maxAmmo;
        }

        /// <summary>实际重量 = 基础重量 + 改造增重</summary>
        public float Weight => data.weight + data.weightPerUpgrade * upgradeLevel;

        public bool IsFunctional => condition != PartCondition.Broken;

        /// <summary>损坏时性能倍率</summary>
        public float PerformanceRate => condition switch
        {
            PartCondition.Normal => 1f,
            PartCondition.Damaged => 0.5f,
            _ => 0f,
        };

        /// <summary>武器当前攻击力（含改造与损坏修正）</summary>
        public int Attack => data is WeaponData w
            ? (int)((w.attack + w.attackPerUpgrade * upgradeLevel) * PerformanceRate)
            : 0;

        /// <summary>引擎当前载重（含改造与损坏修正）</summary>
        public float LoadCapacity => data is EngineData e
            ? (e.loadCapacity + e.loadPerUpgrade * upgradeLevel) * PerformanceRate
            : 0f;

        public bool HasAmmo => data is WeaponData w && (w.maxAmmo < 0 || currentAmmo > 0);

        /// <summary>下一级改造费用；已满级返回 -1</summary>
        public int NextUpgradeCost =>
            upgradeLevel >= data.maxUpgradeLevel ? -1 : data.upgradeCostBase * (upgradeLevel + 1);

        public void Refill()
        {
            if (data is WeaponData w) currentAmmo = w.maxAmmo;
        }

        public void Repair() => condition = PartCondition.Normal;
    }
}
