using System.Collections.Generic;
using System.Linq;
using Game.Battle;
using Game.Field;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 一键生成原型场景 Field / Battle，并加入 Build Settings。
    /// 已存在的场景会被覆盖，请勿在生成的场景里做正式内容。
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        private const string SceneDir = "Assets/Scenes/Prototype";

        [MenuItem("Game/生成原型场景 (Field + Battle)")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            System.IO.Directory.CreateDirectory(SceneDir);

            string field = BuildField();
            string battle = BuildBattle();

            // 加入 Build Settings，Field 放在第一个
            var list = EditorBuildSettings.scenes.Where(s => s.path != field && s.path != battle).ToList();
            list.InsertRange(0, new[] { new EditorBuildSettingsScene(field, true), new EditorBuildSettingsScene(battle, true) });
            EditorBuildSettings.scenes = list.ToArray();

            EditorSceneManager.OpenScene(field);
            Debug.Log("[SceneBuilder] 原型场景已生成，打开 Field 后点 Play 即可");
        }

        private static string BuildField()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(6, 1, 6);
            SetColor(ground, new Color(0.55f, 0.48f, 0.35f));

            // 遇敌区域：半透明红色方块，只作为触发器
            var zone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zone.name = "EncounterZone_Wasteland";
            zone.transform.position = new Vector3(0, 0.05f, 12);
            zone.transform.localScale = new Vector3(30, 0.1f, 20);
            zone.GetComponent<Collider>().isTrigger = true;
            zone.AddComponent<EncounterZone>();
            SetColor(zone, new Color(0.8f, 0.2f, 0.2f));

            // 玩家：胶囊体代替战车；Trigger 检测需要一侧有 Rigidbody
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = new Vector3(0, 1, -10);
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            player.AddComponent<CharacterController>();
            var rb = player.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            player.AddComponent<FieldPlayerController>();
            player.AddComponent<RandomEncounter>();
            SetColor(player, new Color(0.2f, 0.5f, 0.3f));

            // 固定俯视相机，跟随玩家
            var cam = Camera.main!.gameObject;
            cam.transform.SetParent(player.transform, false);
            cam.transform.localPosition = new Vector3(0, 14, -10);
            cam.transform.localRotation = Quaternion.Euler(50, 0, 0);
            cam.AddComponent<KeepWorldRotation>();

            new GameObject("FieldHUD").AddComponent<FieldHUD>();

            string path = $"{SceneDir}/Field.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        private static string BuildBattle()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Camera.main!.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = new Color(0.12f, 0.1f, 0.08f);
            new GameObject("BattleController").AddComponent<BattleController>();

            string path = $"{SceneDir}/Battle.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        private static void SetColor(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            // 使用当前渲染管线的默认材质，URP 和内置管线都适用
            var mat = new Material(r.sharedMaterial) { color = c };
            string dir = $"{SceneDir}/Materials";
            System.IO.Directory.CreateDirectory(dir);
            AssetDatabase.CreateAsset(mat, $"{dir}/M_{go.name}.mat");
            r.sharedMaterial = mat;
        }
    }
}
