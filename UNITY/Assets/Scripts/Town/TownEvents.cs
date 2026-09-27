using System;

namespace Game.Town
{
    /// <summary>
    /// 城镇与赏金事件（docs/INTERFACE.md I-14）。表现层只订阅；OnEnable 订阅、OnDisable 取消。
    /// </summary>
    public static class TownEvents
    {
        /// <summary>进入城镇：城镇 ID</summary>
        public static event Action<string> Entered;
        /// <summary>离开城镇：城镇 ID</summary>
        public static event Action<string> Left;
        /// <summary>住宿完成：城镇 ID、花费</summary>
        public static event Action<string, int> Rested;
        /// <summary>赏金首被击败：敌人 ID</summary>
        public static event Action<string> BountyDefeated;
        /// <summary>领取赏金：敌人 ID、金额</summary>
        public static event Action<string, int> BountyClaimed;

        internal static void RaiseEntered(string id) => Entered?.Invoke(id);
        internal static void RaiseLeft(string id) => Left?.Invoke(id);
        internal static void RaiseRested(string id, int cost) => Rested?.Invoke(id, cost);
        internal static void RaiseBountyDefeated(string id) => BountyDefeated?.Invoke(id);
        internal static void RaiseBountyClaimed(string id, int gold) => BountyClaimed?.Invoke(id, gold);
    }
}
