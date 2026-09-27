using System.Collections.Generic;
using Game.Battle;
using Game.Core;
using Game.Town;
using UnityEngine;

namespace Game.Field
{
    /// <summary>
    /// 赏金首出没点：玩家进入且该赏金首仍在通缉中时，立即开战（可带随从）。击败后不再出现。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BountyZone : MonoBehaviour
    {
        public EnemyData bounty;
        [Tooltip("随从")]
        public EnemyData[] escorts = new EnemyData[0];

        private bool _triggered;

        private void Reset() => GetComponent<Collider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (_triggered || bounty == null || !other.TryGetComponent(out FieldPlayerController _)) return;
            if (BountyService.StateOf(GameSession.Instance.State, bounty.enemyId) != BountyState.Wanted) return;

            _triggered = true;
            var enemies = new List<Combatant> { bounty.CreateCombatant() };
            for (int i = 0; i < escorts.Length; i++)
                if (escorts[i] != null) enemies.Add(escorts[i].CreateCombatant($" {(char)('A' + i)}"));
            Debug.Log($"[Bounty] 遭遇赏金首 {bounty.enemyId}");
            GameSession.Instance.StartBattle(enemies, other.transform.position - transform.forward * 3f);
        }
    }
}
