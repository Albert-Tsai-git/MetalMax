using System;

namespace Game.Field
{
    /// <summary>
    /// 野外事件（docs/INTERFACE.md I-21）。表现层只订阅；OnEnable 订阅、OnDisable 取消。
    /// </summary>
    public static class FieldEvents
    {
        /// <summary>移动方式变化：是否步行（场景开始时也会广播一次当前状态）</summary>
        public static event Action<bool> ModeChanged;
        /// <summary>下车：停放的战车</summary>
        public static event Action<ParkedTank> Dismounted;
        /// <summary>上车</summary>
        public static event Action Boarded;
        /// <summary>当前可交互对象变化（null 表示附近没有）</summary>
        public static event Action<Interactable> TargetChanged;
        /// <summary>完成一次交互</summary>
        public static event Action<Interactable> Interacted;

        internal static void RaiseModeChanged(bool onFoot) => ModeChanged?.Invoke(onFoot);
        internal static void RaiseDismounted(ParkedTank t) => Dismounted?.Invoke(t);
        internal static void RaiseBoarded() => Boarded?.Invoke();
        internal static void RaiseTargetChanged(Interactable i) => TargetChanged?.Invoke(i);
        internal static void RaiseInteracted(Interactable i) => Interacted?.Invoke(i);
    }
}
