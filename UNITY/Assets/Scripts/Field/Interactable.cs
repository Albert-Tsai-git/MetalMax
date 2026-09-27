using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Field
{
    /// <summary>
    /// 可按 E 交互的物体（宝箱、NPC、停放的战车等）。FieldInteractor 选取玩家附近最近的可交互对象。
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        [Tooltip("玩家与其中心距离不超过该值时可交互（米）")]
        public float interactRadius = 2.5f;

        private static readonly List<Interactable> _all = new();
        public static IReadOnlyList<Interactable> All => _all;

        /// <summary>交互提示的文本键（如 UI.Interact.Open）</summary>
        public abstract string PromptKey { get; }

        /// <summary>当前是否可以交互（如宝箱已打开则不可）</summary>
        public virtual bool CanInteract(PlayerState s) => true;

        public abstract void Interact(PlayerState s);

        protected virtual void OnEnable() => _all.Add(this);
        protected virtual void OnDisable() => _all.Remove(this);
    }
}
