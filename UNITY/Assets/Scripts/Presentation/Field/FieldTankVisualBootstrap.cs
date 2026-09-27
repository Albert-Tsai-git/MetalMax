using System.Linq;
using Game.Core;
using Game.Field;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Presentation
{
    /// <summary>
    /// 【临时接入，用户授权 Claude 代做，待 Codex 接手或替换】
    /// 野外/迷宫中显示战车模型（TankVisual，按 ID 从 Resources/Visuals 加载）：
    /// 乘车时模型挂在玩家身上并隐藏玩家灰盒胶囊；下车时模型挂到停放的战车上（隐藏其灰盒箱体），玩家显示为灰盒胶囊。
    /// 没有对应模型时保留灰盒外观。只读取逻辑层数据与事件，不修改游戏状态。
    /// </summary>
    public static class FieldTankVisualBootstrap
    {
        /// <summary>模型底部相对玩家/停放战车中心的高度（CharacterController 默认高 2、中心 0）</summary>
        private const float GroundOffset = -1f;

        private static TankVisual _visual;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            FieldEvents.ModeChanged -= Apply;
            FieldEvents.ModeChanged += Apply;
            Create();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) Create();
        }

        /// <summary>场景加载时创建模型；挂到哪里由 ModeChanged（玩家 Start 时广播）决定</summary>
        private static void Create()
        {
            var player = Object.FindAnyObjectByType<FieldPlayerController>();
            if (player == null) return; // 战斗等没有野外玩家的场景
            var owner = GameSession.Instance.party.FirstOrDefault(p => p.tank != null);
            if (owner == null) return;

            var holder = new GameObject("TankVisual");
            holder.transform.SetParent(player.transform, false);
            _visual = holder.AddComponent<TankVisual>();
            _visual.Bind(owner.tank);
            Apply(player.OnFoot);
        }

        private static void Apply(bool onFoot)
        {
            var player = Object.FindAnyObjectByType<FieldPlayerController>();
            if (player == null || _visual == null) return;
            bool hasModel = _visual.transform.childCount > 0;
            var parked = ParkedTank.Current;

            Transform anchor = onFoot ? parked != null ? parked.transform : null : player.transform;
            _visual.gameObject.SetActive(anchor != null);
            if (anchor != null)
            {
                _visual.transform.SetParent(anchor, false);
                _visual.transform.localPosition = new Vector3(0, GroundOffset, 0);
                _visual.transform.localRotation = Quaternion.identity;
            }
            // 步行模型优先显示人物；没有人物模型时保留胶囊灰盒。乘车时由战车模型取代胶囊。
            var capsule = player.transform.Find("Player_Greybox");
            bool hasCharacter = player.GetComponent<FieldCharacterVisualDriver>()?.HasVisual == true;
            if (capsule != null) capsule.gameObject.SetActive(onFoot ? !hasCharacter : !hasModel);
            if (parked != null && parked.Greybox != null) parked.Greybox.SetActive(!hasModel);
            Debug.Log($"[Visual] {(onFoot ? "步行" : "乘车")}，战车模型{(hasModel ? "已显示" : "缺失，使用灰盒")}");
        }
    }
}
