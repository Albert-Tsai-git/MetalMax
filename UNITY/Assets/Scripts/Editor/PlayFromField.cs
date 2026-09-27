using System.Linq;
using Game.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 编辑器中点 Play 时，若当前打开的不是逻辑场景（Field / Battle / 迷宫等），先切换到 Field 再进入 Play。
    /// 不依赖 playModeStartScene：工程开启了“进入 Play 不重载场景”，该设置会被忽略。
    /// 可通过菜单 Game/Play 从 Field 开始 关闭，设置按本机保存。
    /// 同时保证进入 Play 时重载脚本与场景。
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromField
    {
        private const string MenuPath = "Game/Play 从 Field 开始";
        private const string PrefKey = "Game.PlayFromField";

        static PlayFromField()
        {
            EditorSceneManager.playModeStartScene = null; // 清除旧版本设置
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EnsureFullReload();
        }

        /// <summary>
        /// 进入 Play 时总是重载脚本域与场景（关闭“Enter Play Mode Options”），
        /// 避免静态状态（对话、传送、城镇等）残留到下一次试玩。
        /// </summary>
        private static void EnsureFullReload()
        {
            if (!EditorSettings.enterPlayModeOptionsEnabled) return;
            EditorSettings.enterPlayModeOptionsEnabled = false;
            AssetDatabase.SaveAssets();
            Debug.Log("[PlayFromField] 已开启进入 Play 时重载脚本与场景（关闭 Enter Play Mode Options）");
        }

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        [MenuItem(MenuPath)]
        private static void Toggle() => Enabled = !Enabled;

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode || !Enabled) return;
            string active = EditorSceneManager.GetActiveScene().name;
            if (PrototypeSceneBuilder.LogicScenes.Contains(active)) return;

            string path = PrototypeSceneBuilder.ScenePath(GameSession.FieldSceneName);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                Debug.LogWarning($"[PlayFromField] 找不到 {path}，请先执行 Game/生成逻辑场景");
                return;
            }
            // 当前场景有未保存修改时询问；用户取消则不进入 Play
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorApplication.isPlaying = false;
                return;
            }
            EditorSceneManager.OpenScene(path);
            Debug.Log($"[PlayFromField] 当前场景 {active} 不是逻辑场景，已切换到 Field");
        }
    }
}
