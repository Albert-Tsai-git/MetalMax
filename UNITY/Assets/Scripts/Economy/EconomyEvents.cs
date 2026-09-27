using System;
using Game.Tank;

namespace Game.Economy
{
    /// <summary>
    /// 经济与车库事件（docs/INTERFACE.md I-11）。表现层只订阅；OnEnable 订阅、OnDisable 取消。
    /// </summary>
    public static class EconomyEvents
    {
        /// <summary>金钱变化：旧值、新值</summary>
        public static event Action<int, int> GoldChanged;
        /// <summary>背包内容变化</summary>
        public static event Action InventoryChanged;
        /// <summary>买入：商店 ID、部件实例</summary>
        public static event Action<string, PartInstance> Bought;
        /// <summary>卖出：部件实例、得到的金钱</summary>
        public static event Action<PartInstance, int> Sold;
        /// <summary>改造完成：部件实例（upgradeLevel 为新等级）</summary>
        public static event Action<PartInstance> Upgraded;

        internal static void RaiseGoldChanged(int oldGold, int newGold) { if (oldGold != newGold) GoldChanged?.Invoke(oldGold, newGold); }
        internal static void RaiseInventoryChanged() => InventoryChanged?.Invoke();
        internal static void RaiseBought(string shopId, PartInstance p) => Bought?.Invoke(shopId, p);
        internal static void RaiseSold(PartInstance p, int gold) => Sold?.Invoke(p, gold);
        internal static void RaiseUpgraded(PartInstance p) => Upgraded?.Invoke(p);
    }
}
