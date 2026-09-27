using UnityEngine;

namespace Game.Tank
{
    /// <summary>部件槽位类型</summary>
    public enum PartSlot
    {
        Chassis,    // 底盘
        Engine,     // 引擎
        CUnit,      // C 装置
        Weapon,     // 武器（主炮/副炮/SE 共用武器孔）
    }

    /// <summary>武器类别</summary>
    public enum WeaponType
    {
        MainCannon, // 主炮：高伤害单体
        SubGun,     // 副炮：连射/群攻
        SpecialEq,  // SE：导弹、火焰等
    }

    /// <summary>攻击范围</summary>
    public enum AttackRange
    {
        Single,     // 单体
        Group,      // 一组
        All,        // 全体
    }

    /// <summary>
    /// 所有战车部件的基类：静态配置数据，运行时不修改。
    /// </summary>
    public abstract class TankPartData : ScriptableObject
    {
        [Header("基础信息")]
        public string partId;
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;

        [Header("外观")]
        [Tooltip("挂到底盘挂点上的模型预制体")]
        public GameObject modelPrefab;

        [Header("数值")]
        [Tooltip("重量（吨）")]
        [Min(0f)] public float weight = 1f;
        [Min(0)] public int price = 100;
        [Tooltip("该部件自身的防御力")]
        [Min(0)] public int defense = 0;

        [Header("改造")]
        [Tooltip("最大改造等级")]
        [Min(0)] public int maxUpgradeLevel = 5;
        [Tooltip("每级改造增加的重量（吨）")]
        [Min(0f)] public float weightPerUpgrade = 0.2f;
        [Tooltip("每级改造的基础费用，实际费用 = 基础费用 × 目标等级")]
        [Min(0)] public int upgradeCostBase = 200;

        public abstract PartSlot Slot { get; }
    }
}
