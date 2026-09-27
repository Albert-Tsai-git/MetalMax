using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Battle;
using Game.Core;
using Game.Field;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 生成逻辑场景 Field / Battle（Assets/Scenes/Logic），并同步 Build Settings。
    /// 逻辑场景只放碰撞体、触发区、控制器与相机；可见物体一律标 GreyboxMarker，
    /// 美术场景 {名称}_Art（Assets/Scenes/Art）存在时由 ArtSceneLoader 叠加并隐藏灰盒。
    /// 已存在的逻辑场景会被覆盖。
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        private const string LogicDir = "Assets/Scenes/Logic";
        private const string ArtDir = "Assets/Scenes/Art";
        private const string GreyboxMatDir = LogicDir + "/Greybox";

        [MenuItem("Game/生成逻辑场景 (Field + Battle)")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(LogicDir);

            BuildField();
            BuildBattle();
            SyncBuildSettings();

            if (!Application.isBatchMode) EditorSceneManager.OpenScene($"{LogicDir}/{GameSession.FieldSceneName}.unity");
            Debug.Log("[SceneBuilder] 逻辑场景已生成，打开 Field 后点 Play 即可");
        }

        /// <summary>Build Settings = 逻辑场景（Field 在首位）+ Art 目录下所有 *_Art 场景</summary>
        [MenuItem("Game/同步 Build Settings")]
        public static void SyncBuildSettings()
        {
            var paths = new List<string>
            {
                $"{LogicDir}/{GameSession.FieldSceneName}.unity",
                $"{LogicDir}/{GameSession.BattleSceneName}.unity",
            };
            if (Directory.Exists(ArtDir))
                paths.AddRange(Directory.GetFiles(ArtDir, "*" + ArtSceneLoader.ArtSuffix + ".unity")
                    .Select(p => p.Replace(Path.DirectorySeparatorChar, '/')).OrderBy(p => p));

            EditorBuildSettings.scenes = paths.Where(File.Exists)
                .Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
            Debug.Log($"[SceneBuilder] Build Settings：{string.Join(", ", paths.Where(File.Exists))}");
        }

        private static void BuildField()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            new GameObject("[ArtSceneLoader]").AddComponent<ArtSceneLoader>();
            Greybox(Object.FindAnyObjectByType<Light>().gameObject);

            // 地面：逻辑碰撞体 + 灰盒外观分开
            var ground = new GameObject("Ground");
            var box = ground.AddComponent<BoxCollider>();
            box.size = new Vector3(60, 0.2f, 60);
            box.center = new Vector3(0, -0.1f, 0);
            var groundView = GameObject.CreatePrimitive(PrimitiveType.Plane);
            groundView.name = "Ground_Greybox";
            Object.DestroyImmediate(groundView.GetComponent<Collider>());
            groundView.transform.localScale = new Vector3(6, 1, 6);
            Greybox(groundView, new Color(0.55f, 0.48f, 0.35f));

            // 遇敌区域：触发器本身不可见，另放半透明灰盒
            var zone = new GameObject("EncounterZone_Wasteland");
            zone.transform.position = new Vector3(0, 0.05f, 12);
            var trigger = zone.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(30, 2f, 20);
            zone.AddComponent<EncounterZone>();
            var zoneView = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zoneView.name = "EncounterZone_Greybox";
            Object.DestroyImmediate(zoneView.GetComponent<Collider>());
            zoneView.transform.SetParent(zone.transform, false);
            zoneView.transform.localScale = new Vector3(30, 0.1f, 20);
            Greybox(zoneView, new Color(0.8f, 0.2f, 0.2f));

            // 玩家：Trigger 检测需要一侧有 Rigidbody；外观为灰盒胶囊，正式外观由 Codex 的表现层挂载
            var player = new GameObject("Player");
            player.transform.position = new Vector3(0, 1, -10);
            player.AddComponent<CharacterController>();
            var rb = player.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            player.AddComponent<FieldPlayerController>();
            player.AddComponent<RandomEncounter>();
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Player_Greybox";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(player.transform, false);
            Greybox(body, new Color(0.2f, 0.5f, 0.3f));

            // 固定俯视相机，跟随玩家
            var cam = Camera.main!.gameObject;
            cam.transform.SetParent(player.transform, false);
            cam.transform.localPosition = new Vector3(0, 14, -10);
            cam.transform.localRotation = Quaternion.Euler(50, 0, 0);
            cam.AddComponent<KeepWorldRotation>();

            // 城镇入口：栈桥镇（南侧）
            var town = new GameObject("TownGate_TWN_Zhanqiao");
            town.transform.position = new Vector3(-15, 0, -14);
            var townTrigger = town.AddComponent<BoxCollider>();
            townTrigger.isTrigger = true;
            townTrigger.size = new Vector3(8, 3, 8);
            town.AddComponent<TownGate>().townId = PlayerState.DefaultTown;
            var townView = GameObject.CreatePrimitive(PrimitiveType.Cube);
            townView.name = "TownGate_Greybox";
            Object.DestroyImmediate(townView.GetComponent<Collider>());
            townView.transform.SetParent(town.transform, false);
            townView.transform.localScale = new Vector3(8, 0.2f, 8);
            Greybox(townView, new Color(0.3f, 0.45f, 0.8f));

            // 赏金首出没点：铁钳巨蟹（遇敌区北端）
            var bountyZone = new GameObject("BountyZone_ENM_Bounty_IronCrab");
            bountyZone.transform.position = new Vector3(0, 0, 28);
            var bountyTrigger = bountyZone.AddComponent<BoxCollider>();
            bountyTrigger.isTrigger = true;
            bountyTrigger.size = new Vector3(6, 3, 6);
            bountyZone.AddComponent<BountyZone>().bounty =
                AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Resources/GameData/Enemies/ENM_Bounty_IronCrab.asset");
            var bountyView = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bountyView.name = "BountyZone_Greybox";
            Object.DestroyImmediate(bountyView.GetComponent<Collider>());
            bountyView.transform.SetParent(bountyZone.transform, false);
            bountyView.transform.localScale = new Vector3(6, 0.1f, 6);
            Greybox(bountyView, new Color(0.9f, 0.6f, 0.1f));

            new GameObject("FieldHUD").AddComponent<FieldHUD>();
            new GameObject("TownDebugMenu").AddComponent<TownDebugMenu>();
            new GameObject("DialogueDebugUI").AddComponent<DialogueDebugUI>();

            // 第一幕开场：首次进入栈桥镇时播放开场对话（对话内容由 Codex 在 dialogue.csv 中编写）
            var intro = new GameObject("StoryTrigger_act1_intro");
            intro.transform.position = town.transform.position;
            var introTrigger = intro.AddComponent<BoxCollider>();
            introTrigger.isTrigger = true;
            introTrigger.size = new Vector3(8, 3, 8);
            var st = intro.AddComponent<StoryTrigger>();
            st.triggerId = "act1_intro";
            st.dialogueId = "DLG_Act1_Intro";
            EditorSceneManager.SaveScene(scene, $"{LogicDir}/{GameSession.FieldSceneName}.unity");
        }

        private static void BuildBattle()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            new GameObject("[ArtSceneLoader]").AddComponent<ArtSceneLoader>();
            Greybox(Object.FindAnyObjectByType<Light>().gameObject);
            Camera.main!.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = new Color(0.12f, 0.1f, 0.08f);
            new GameObject("BattleController").AddComponent<BattleController>();
            EditorSceneManager.SaveScene(scene, $"{LogicDir}/{GameSession.BattleSceneName}.unity");
        }

        /// <summary>标记为灰盒；给定颜色时同时生成灰盒材质</summary>
        private static void Greybox(GameObject go, Color? color = null)
        {
            go.AddComponent<GreyboxMarker>();
            if (color == null) return;
            var r = go.GetComponent<Renderer>();
            // 使用当前渲染管线的默认材质，URP 和内置管线都适用
            var mat = new Material(r.sharedMaterial) { color = color.Value };
            Directory.CreateDirectory(GreyboxMatDir);
            AssetDatabase.CreateAsset(mat, $"{GreyboxMatDir}/M_{go.name}.mat");
            r.sharedMaterial = mat;
        }
    }
}
