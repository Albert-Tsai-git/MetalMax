using System.Collections.Generic;
using UnityEngine;

namespace Game.Field
{
    /// <summary>出生点：场景切换后玩家出现的位置，由 ScenePortal 按 ID 指定</summary>
    public class SpawnPoint : MonoBehaviour
    {
        public string spawnId;

        private static readonly List<SpawnPoint> _all = new();

        public static SpawnPoint Find(string id) => _all.Find(s => s.spawnId == id);

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.DrawRay(transform.position, transform.forward);
        }
    }
}
