using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// <summary>
    /// 逻辑场景之间的移动（野外 ↔ 迷宫 等）：记录目标出生点，新场景加载后由玩家控制器取用。
    /// </summary>
    public static class SceneTravel
    {
        private static string _pendingSpawn;

        /// <summary>前往目标逻辑场景的指定出生点</summary>
        public static void Travel(string scene, string spawnId)
        {
            _pendingSpawn = spawnId;
            Debug.Log($"[Travel] 前往 {scene}（出生点 {spawnId}）");
            SceneManager.LoadScene(scene);
        }

        /// <summary>只记录出生点（不加载场景），用于测试与读档</summary>
        public static void SetPendingSpawn(string spawnId) => _pendingSpawn = spawnId;

        /// <summary>取出待使用的出生点（只取一次）</summary>
        public static bool TryConsumeSpawn(out string spawnId)
        {
            spawnId = _pendingSpawn;
            _pendingSpawn = null;
            return !string.IsNullOrEmpty(spawnId);
        }
    }
}
