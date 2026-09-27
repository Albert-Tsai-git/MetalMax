using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Field
{
    /// <summary>
    /// 随机遇敌：挂在玩家身上。在 EncounterZone 内按移动距离累积，按概率触发战斗。
    /// </summary>
    [RequireComponent(typeof(FieldPlayerController))]
    public class RandomEncounter : MonoBehaviour
    {
        [Tooltip("关闭后不会遇敌（调试用）")]
        public bool encountersEnabled = true;

        private FieldPlayerController _mover;
        private readonly List<EncounterZone> _zones = new();
        private readonly System.Random _rng = new();
        private float _safeLeft;
        private bool _triggered;

        private void Awake()
        {
            _mover = GetComponent<FieldPlayerController>();
        }

        private void Start()
        {
            // 每次进入野外场景（包括从战斗返回）都给一段安全距离
            _safeLeft = 5f;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out EncounterZone z) && !_zones.Contains(z)) _zones.Add(z);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out EncounterZone z)) _zones.Remove(z);
        }

        private void Update()
        {
            if (!encountersEnabled || _triggered || _zones.Count == 0) return;

            float dist = _mover.LastMoveDistance;
            if (dist <= 0f) return;

            // 取最后进入的区域为当前区域
            var zone = _zones[^1];
            if (_safeLeft > 0f)
            {
                _safeLeft -= dist;
                return;
            }

            // 1 - (1-p)^d 保证不同帧率下遇敌率一致
            float chance = 1f - Mathf.Pow(1f - zone.encounterRatePerMeter, dist);
            if (_rng.NextDouble() < chance)
            {
                _triggered = true;
                GameSession.Instance.StartBattle(zone.RollEnemies(_rng), transform.position);
            }
        }
    }
}
