using Game.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 编辑器中点 Play 时总是从 Field 场景开始（不影响当前打开的场景，退出 Play 后回到原场景）。
    /// 可通过菜单 Game/Play 从 Field 开始 关闭，设置按本机保存。
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromField
    {
        private const string MenuPath = "Game/Play 从 Field 开始";
        private const string PrefKey = "Game.PlayFromField";

        static PlayFromField() => EditorApplication.delayCall += Apply;

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            Enabled = !Enabled;
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        private static void Apply()
        {
            if (!Enabled)
            {
                EditorSceneManager.playModeStartScene = null;
                return;
            }
            string path = PrototypeSceneBuilder.ScenePath(GameSession.FieldSceneName);
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            if (scene == null) Debug.LogWarning($"[PlayFromField] 找不到 {path}，请先执行 Game/生成逻辑场景");
            EditorSceneManager.playModeStartScene = scene;
        }
    }
}
