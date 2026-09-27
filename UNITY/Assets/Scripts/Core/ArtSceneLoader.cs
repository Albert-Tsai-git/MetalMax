using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// <summary>
    /// 放在每个逻辑场景中：若存在同名美术场景 {逻辑场景名}_Art，则叠加加载并设为活动场景，
    /// 同时隐藏本场景中所有灰盒占位物体（GreyboxMarker）。没有美术场景时保持灰盒显示。
    /// </summary>
    public class ArtSceneLoader : MonoBehaviour
    {
        public const string ArtSuffix = "_Art";

        /// <summary>当前逻辑场景名；美术场景成为活动场景后，用它来记录返回位置</summary>
        public static string CurrentLogicScene { get; private set; }

        private void Awake()
        {
            CurrentLogicScene = gameObject.scene.name;
        }

        private void Start()
        {
            string art = CurrentLogicScene + ArtSuffix;
            if (!Application.CanStreamedLevelBeLoaded(art))
            {
                Debug.Log($"[Scene] {CurrentLogicScene} 无美术场景，使用灰盒");
                return;
            }
            var op = SceneManager.LoadSceneAsync(art, LoadSceneMode.Additive);
            op.completed += _ =>
            {
                SceneManager.SetActiveScene(SceneManager.GetSceneByName(art));
                foreach (var g in FindObjectsByType<GreyboxMarker>(FindObjectsInactive.Include))
                    if (g.gameObject.scene == gameObject.scene) g.gameObject.SetActive(false);
                Debug.Log($"[Scene] 已叠加美术场景 {art}，灰盒已隐藏");
            };
        }
    }
}
