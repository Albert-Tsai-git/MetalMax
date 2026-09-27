using Game.Core;
using Game.Story;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Field
{
    /// <summary>挂在玩家上：每帧选取最近的可交互对象，按 E 交互。当前目标变化时广播 FieldEvents.TargetChanged。</summary>
    public class FieldInteractor : MonoBehaviour
    {
        /// <summary>当前可交互的对象，没有为 null</summary>
        public Interactable Current { get; private set; }

        private void Update()
        {
            var s = GameSession.Instance.State;
            Interactable best = null;
            float bestDist = float.MaxValue;
            if (StoryService.ActiveDialogue == null)
            {
                foreach (var it in Interactable.All)
                {
                    if (it == null || !it.isActiveAndEnabled || !it.CanInteract(s)) continue;
                    var d = it.transform.position - transform.position;
                    d.y = 0;
                    float dist = d.magnitude;
                    if (dist <= it.interactRadius && dist < bestDist) { best = it; bestDist = dist; }
                }
            }
            if (best != Current)
            {
                Current = best;
                FieldEvents.RaiseTargetChanged(best);
            }

            if (Current != null && Keyboard.current?.eKey.wasPressedThisFrame == true)
            {
                var target = Current;
                Debug.Log($"[Interact] {target.name}");
                target.Interact(s);
                FieldEvents.RaiseInteracted(target);
            }
        }
    }
}
