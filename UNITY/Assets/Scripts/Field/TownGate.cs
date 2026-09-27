using System.Collections.Generic;
using Game.Core;
using Game.Town;
using UnityEngine;

namespace Game.Field
{
    /// <summary>
    /// 城镇入口：玩家进入触发区即进入城镇（登记为最后到访城镇），离开触发区即离开。
    /// 全灭后玩家在 RespawnPoint 复活。城镇内不会遇敌。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TownGate : MonoBehaviour
    {
        public string townId = PlayerState.DefaultTown;
        [Tooltip("复活点相对入口的偏移")]
        public Vector3 respawnOffset = new(0, 1, -6);

        private static readonly List<TownGate> _all = new();

        /// <summary>玩家当前所在城镇入口，不在城镇时为 null</summary>
        public static TownGate Current { get; private set; }

        public Vector3 RespawnPoint => transform.position + respawnOffset;

        public static TownGate Find(string id) => _all.Find(g => g.townId == id);

        private void OnEnable() => _all.Add(this);

        private void OnDisable()
        {
            _all.Remove(this);
            if (Current == this) Current = null;
        }

        private void Reset() => GetComponent<Collider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent(out FieldPlayerController _)) return;
            if (TownService.Enter(GameSession.Instance.State, townId) != OpResult.Ok) return;
            Current = this;
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.TryGetComponent(out FieldPlayerController _) || Current != this) return;
            Current = null;
            TownService.Leave(townId);
        }
    }
}
