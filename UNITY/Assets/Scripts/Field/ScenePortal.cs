using System;
using Game.Core;
using Game.Story;
using UnityEngine;

namespace Game.Field
{
    /// <summary>
    /// 场景传送口：玩家进入触发区且条件成立时，前往目标场景的出生点；条件不成立时广播 Locked。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ScenePortal : MonoBehaviour
    {
        public string targetScene;
        public string targetSpawnId;
        [Tooltip("通行条件（Conditions 语法），空为总是可通行")]
        public string condition;
        [Tooltip("条件不成立时的提示文本键")]
        public string lockedTextKey;

        /// <summary>通行被拒：传送口（表现层据 lockedTextKey 显示提示）</summary>
        public static event Action<ScenePortal> Locked;

        private void Reset() => GetComponent<Collider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent(out FieldPlayerController _)) return;
            if (!Conditions.Evaluate(condition, GameSession.Instance.State))
            {
                Debug.Log($"[Travel] {name} 未满足条件 {condition}");
                Locked?.Invoke(this);
                return;
            }
            SceneTravel.Travel(targetScene, targetSpawnId);
        }
    }
}
