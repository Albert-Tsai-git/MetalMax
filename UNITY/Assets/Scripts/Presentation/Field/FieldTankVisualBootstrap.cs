using System.Linq;
using Game.Core;
using Game.Field;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Presentation
{
    /// <summary>
    /// 【临时接入，用户授权 Claude 代做，待 Codex 接手或替换】
    /// 野外/迷宫场景加载后，把队伍中第一辆战车的模型（TankVisual，按 ID 从 Resources/Visuals 加载）挂到玩家身上；
    /// 成功生成模型时隐藏玩家的灰盒胶囊，没有对应模型时保留胶囊。
    /// 只读取逻辑层数据，不修改游戏状态。
    /// </summary>
    public static class FieldTankVisualBootstrap
    {
        /// <summary>玩家碰撞体底部相对玩家中心的高度（CharacterController 默认高 2、中心 0）</summary>
        private const float GroundOffset = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Attach();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) Attach();
        }

        private static void Attach()
        {
            var player = Object.FindAnyObjectByType<FieldPlayerController>();
            if (player == null) return; // 战斗等没有野外玩家的场景

            var owner = GameSession.Instance.party.FirstOrDefault(p => p.tank != null);
            if (owner == null) return;

            var holder = new GameObject("TankVisual");
            holder.transform.SetParent(player.transform, false);
            holder.transform.localPosition = new Vector3(0, GroundOffset, 0);
            var visual = holder.AddComponent<TankVisual>();
            visual.Bind(owner.tank);

            bool hasModel = holder.transform.childCount > 0;
            var greybox = player.transform.Find("Player_Greybox");
            if (greybox != null) greybox.gameObject.SetActive(!hasModel);
            Debug.Log(hasModel
                ? $"[Visual] 玩家使用战车模型 {owner.tank.chassis?.data.partId}"
                : "[Visual] 当前底盘没有模型，保留灰盒外观");
        }
    }
}
