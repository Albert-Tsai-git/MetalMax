using Game.Field;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Game.UI
{
    /// <summary>自动创建的常驻输入组件：把界面按键交给 UIRouter，切场景时清空界面</summary>
    public class UIInput : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var go = new GameObject("[UIInput]");
            DontDestroyOnLoad(go);
            go.AddComponent<UIInput>();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) UIRouter.CloseAll();
        }

        private void Update() =>
            UIRouter.HandleInput(Keyboard.current, FindAnyObjectByType<FieldPlayerController>() != null);
    }
}
