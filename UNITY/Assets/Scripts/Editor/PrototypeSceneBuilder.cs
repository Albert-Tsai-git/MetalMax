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
    /// 生成逻辑场景 Field / Battle / Dungeon_PumpStation（Assets/Scenes/Logic），并同步 Build Settings。
    /// 逻辑场景只放碰撞体、触发区、控制器与相机；可见物体一律标 GreyboxMarker，
    /// 美术场景 {名称}_Art（Assets/Scenes/Art）存在时由 ArtSceneLoader 叠加并隐藏灰盒。
    /// 已存在的逻辑场景会被覆盖。
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        private const string LogicDir = "Assets/Scenes/Logic";
        private const string ArtDir = "Assets/Scenes/Art";
        private const string GreyboxMatDir = LogicDir + "/Greybox";
        private const string EnemyDir = "Assets/Resources/GameData/Enemies";

        /// <summary>逻辑场景列表，Build Settings 中按此顺序排列（Field 为启动场景）</summary>
        public static readonly string[] LogicScenes =
            { GameSession.FieldSceneName, GameSession.BattleSceneName, GameSession.PumpStationSceneName };

        [MenuItem("Game/生成逻辑场景")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(LogicDir);
            AssetDatabase.DeleteAsset(GreyboxMatDir); // 清除旧灰盒材质，本次重新生成

            BuildField();
            BuildBattle();
            BuildPumpStation();
            SyncBuildSettings();

            if (!Application.isBatchMode) EditorSceneManager.OpenScene(ScenePath(GameSession.FieldSceneName));
            Debug.Log("[SceneBuilder] 逻辑场景已生成，打开 Field 后点 Play 即可");
        }

        public static string ScenePath(string name) => $"{LogicDir}/{name}.unity";

        /// <summary>Build Settings = 逻辑场景 + Art 目录下所有 *_Art 场景</summary>
        [MenuItem("Game/同步 Build Settings")]
        public static void SyncBuildSettings()
        {
            var paths = LogicScenes.Select(ScenePath).ToList();
            if (Directory.Exists(ArtDir))
                paths.AddRange(Directory.GetFiles(ArtDir, "*" + ArtSceneLoader.ArtSuffix + ".unity")
                    .Select(p => p.Replace(Path.DirectorySeparatorChar, '/')).OrderBy(p => p));

            EditorBuildSettings.scenes = paths.Where(File.Exists)
                .Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
            Debug.Log($"[SceneBuilder] Build Settings：{string.Join(", ", paths.Where(File.Exists))}");
        }

        #region 野外

        private static void BuildField()
        {
            var scene = NewLogicScene();
            Ground(new Vector3(0, 0, 0), new Vector2(60, 60), new Color(0.55f, 0.48f, 0.35f));

            // 野外遇敌区：较弱的组合，用于升级与攒钱（与 BattleSimulator.FieldTable 一致）
            var zone = TriggerBox("EncounterZone_Wasteland", new Vector3(0, 0.05f, 12), new Vector3(30, 2, 20),
                new Color(0.8f, 0.2f, 0.2f), 0.1f);
            zone.AddComponent<EncounterZone>().groups = new List<EncounterZone.EnemyGroup>
            {
                Group(3, "ENM_Ant", "ENM_Ant"),
                Group(2, "ENM_Dog", "ENM_Dog"),
                Group(2, "ENM_Ant", "ENM_Ant", "ENM_Ant"),
                Group(1, "ENM_Dog", "ENM_Ant"),
            };

            var player = Player(new Vector3(0, 1, -10));
            Spawn("start", player.transform.position);

            // 栈桥镇入口
            var town = TriggerBox("TownGate_TWN_Zhanqiao", new Vector3(-15, 0, -14), new Vector3(8, 3, 8),
                new Color(0.3f, 0.45f, 0.8f), 0.2f);
            town.AddComponent<TownGate>().townId = PlayerState.DefaultTown;

            // 第一幕：首次进镇播放开场并开始任务；带回维护日志后进镇回报。对话缺失时效果照常执行。
            Story("act1_intro", town.transform.position, "", "DLG_Act1_Intro",
                "quest:start:QST_Act1_Signal; set:act1_heard_signal");
            Story("act1_report", town.transform.position, "flag:act1_got_log & !flag:act1_reported", "DLG_Act1_Report",
                "set:act1_reported");

            // 废弃泵站入口（听到信号后开放）
            var portal = TriggerBox("Portal_PumpStation", new Vector3(22, 0, 24), new Vector3(4, 3, 4),
                new Color(0.2f, 0.8f, 0.8f), 0.3f);
            var p = portal.AddComponent<ScenePortal>();
            p.targetScene = GameSession.PumpStationSceneName;
            p.targetSpawnId = "entrance";
            p.condition = "quest:QST_Act1_Signal>=1";
            p.lockedTextKey = "UI.Portal.PumpLocked";
            Spawn("from_pump", new Vector3(17, 1, 19));

            // 栈桥镇车库老板曲婆（STORY 关键 NPC），按 E 对话；对话内容由 Codex 编写
            var npc = new GameObject("NPC_Qupo");
            npc.transform.position = new Vector3(-9, 1, -14);
            npc.AddComponent<CapsuleCollider>();
            var talk = npc.AddComponent<NpcTalk>();
            talk.npcId = "NPC_Qupo";
            talk.dialogueId = "DLG_Qupo";
            Visual(PrimitiveType.Capsule, "NPC_Qupo_Greybox", npc.transform, new Color(0.8f, 0.5f, 0.3f));

            new GameObject("TownDebugMenu").AddComponent<TownDebugMenu>();
            Save(scene, GameSession.FieldSceneName);
        }

        #endregion

        #region 战斗

        private static void BuildBattle()
        {
            var scene = NewLogicScene();
            Camera.main!.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = new Color(0.12f, 0.1f, 0.08f);
            new GameObject("BattleController").AddComponent<BattleController>();
            Save(scene, GameSession.BattleSceneName);
        }

        #endregion

        #region 废弃泵站

        /// <summary>
        /// 废弃泵站短迷宫：沿 +Z 的走廊。入口（z≈0）→ 遇敌段（z 10~50，两个宝箱）→ 深处宝箱 → 铁钳巨蟹（z≈72）。
        /// </summary>
        private static void BuildPumpStation()
        {
            var scene = NewLogicScene();
            var floor = new Color(0.75f, 0.74f, 0.7f);
            Ground(new Vector3(0, 0, 40), new Vector2(12, 84), floor);
            Wall("Wall_West", new Vector3(-6, 1.5f, 40), new Vector3(0.5f, 3, 84));
            Wall("Wall_East", new Vector3(6, 1.5f, 40), new Vector3(0.5f, 3, 84));
            Wall("Wall_South", new Vector3(0, 1.5f, -2), new Vector3(12, 3, 0.5f));
            Wall("Wall_North", new Vector3(0, 1.5f, 82), new Vector3(12, 3, 0.5f));

            Player(new Vector3(0, 1, 6));
            Spawn("entrance", new Vector3(0, 1, 6));

            var exit = TriggerBox("Portal_Exit", new Vector3(0, 0, 1), new Vector3(8, 3, 2),
                new Color(0.2f, 0.8f, 0.8f), 0.3f);
            var p = exit.AddComponent<ScenePortal>();
            p.targetScene = GameSession.FieldSceneName;
            p.targetSpawnId = "from_pump";

            // 迷宫专属遇敌
            var zone = TriggerBox("EncounterZone_PumpStation", new Vector3(0, 0.05f, 32), new Vector3(12, 2, 44),
                new Color(0.6f, 0.2f, 0.2f), 0.05f);
            var ez = zone.AddComponent<EncounterZone>();
            ez.encounterRatePerMeter = 0.05f;
            ez.groups = new List<EncounterZone.EnemyGroup>
            {
                // 各组合考验不同武器：蚁群（一组、怕火）→ 副炮；炮台虫（硬、怕电）→ 主炮；混编两组 → SE
                Group(3, "ENM_Ant", "ENM_Ant", "ENM_Ant", "ENM_Ant"),
                Group(2, "ENM_TurretBug"),
                Group(2, "ENM_Dog", "ENM_Dog", "ENM_Ant", "ENM_Ant"),
                Group(1, "ENM_TurretBug", "ENM_Ant", "ENM_Ant", "ENM_Ant"),
            };

            Chest("pump_01", new Vector3(4, 0, 20), "gold:+300");
            Chest("pump_02", new Vector3(-4, 0, 42), "item:ITM_RepairPack:2");
            Chest("pump_03", new Vector3(4, 0, 62), "item:ITM_ReviveKit:1; item:ITM_Tonic:3");

            // 深处：铁钳巨蟹盘踞的滤站
            var boss = TriggerBox("BountyZone_ENM_Bounty_IronCrab", new Vector3(0, 0, 72), new Vector3(10, 3, 6),
                new Color(0.9f, 0.6f, 0.1f), 0.1f);
            boss.AddComponent<BountyZone>().bounty = Enemy("ENM_Bounty_IronCrab");

            Save(scene, GameSession.PumpStationSceneName);
        }

        #endregion

        #region 构建工具

        private static UnityEngine.SceneManagement.Scene NewLogicScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            new GameObject("[ArtSceneLoader]").AddComponent<ArtSceneLoader>();
            Greybox(Object.FindAnyObjectByType<Light>().gameObject);
            return scene;
        }

        private static void Save(UnityEngine.SceneManagement.Scene scene, string name) =>
            EditorSceneManager.SaveScene(scene, ScenePath(name));

        /// <summary>地面：逻辑碰撞体与灰盒外观分开</summary>
        private static void Ground(Vector3 center, Vector2 size, Color color)
        {
            var ground = new GameObject("Ground");
            ground.transform.position = center;
            var box = ground.AddComponent<BoxCollider>();
            box.size = new Vector3(size.x, 0.2f, size.y);
            box.center = new Vector3(0, -0.1f, 0);
            var view = Visual(PrimitiveType.Plane, "Ground_Greybox", ground.transform, color);
            view.transform.localScale = new Vector3(size.x / 10f, 1, size.y / 10f);
        }

        private static void Wall(string name, Vector3 pos, Vector3 size)
        {
            var wall = new GameObject(name);
            wall.transform.position = pos;
            wall.AddComponent<BoxCollider>().size = size;
            Visual(PrimitiveType.Cube, name + "_Greybox", wall.transform, new Color(0.45f, 0.45f, 0.45f)).transform.localScale = size;
        }

        /// <summary>触发区 + 扁平灰盒外观</summary>
        private static GameObject TriggerBox(string name, Vector3 pos, Vector3 size, Color color, float viewHeight)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            Visual(PrimitiveType.Cube, name + "_Greybox", go.transform, color).transform.localScale = new Vector3(size.x, viewHeight, size.z);
            return go;
        }

        /// <summary>玩家：Trigger 检测需要一侧有 Rigidbody；外观为灰盒胶囊，正式外观由 Codex 的表现层挂载</summary>
        private static GameObject Player(Vector3 pos)
        {
            var player = new GameObject("Player");
            player.transform.position = pos;
            player.AddComponent<CharacterController>();
            player.AddComponent<Rigidbody>().isKinematic = true;
            player.AddComponent<FieldPlayerController>();
            player.AddComponent<RandomEncounter>();
            player.AddComponent<FieldInteractor>();
            Visual(PrimitiveType.Capsule, "Player_Greybox", player.transform, new Color(0.2f, 0.5f, 0.3f));

            // 固定俯视相机，跟随玩家
            var cam = Camera.main!.gameObject;
            cam.transform.SetParent(player.transform, false);
            cam.transform.localPosition = new Vector3(0, 14, -10);
            cam.transform.localRotation = Quaternion.Euler(50, 0, 0);
            cam.AddComponent<KeepWorldRotation>();

            new GameObject("FieldHUD").AddComponent<FieldHUD>();
            new GameObject("DialogueDebugUI").AddComponent<DialogueDebugUI>();
            return player;
        }

        private static void Spawn(string id, Vector3 pos)
        {
            var go = new GameObject($"Spawn_{id}");
            go.transform.position = pos;
            go.AddComponent<SpawnPoint>().spawnId = id;
        }

        private static void Story(string id, Vector3 pos, string condition, string dialogueId, string effects)
        {
            var go = new GameObject($"StoryTrigger_{id}");
            go.transform.position = pos;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(8, 3, 8);
            var st = go.AddComponent<StoryTrigger>();
            st.triggerId = id;
            st.condition = condition;
            st.dialogueId = dialogueId;
            st.effects = effects;
        }

        private static void Chest(string id, Vector3 pos, string contents)
        {
            var go = new GameObject($"Chest_{id}");
            go.transform.position = pos;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1.5f, 2, 1.5f);
            var chest = go.AddComponent<TreasureChest>();
            chest.chestId = id;
            chest.contents = contents;
            chest.visual = Visual(PrimitiveType.Cube, $"Chest_{id}_Greybox", go.transform, new Color(0.85f, 0.7f, 0.2f));
            chest.visual.transform.localScale = new Vector3(1, 0.7f, 0.7f);
            chest.visual.transform.localPosition = new Vector3(0, 0.35f, 0);
        }

        private static EnemyData Enemy(string id)
        {
            var e = AssetDatabase.LoadAssetAtPath<EnemyData>($"{EnemyDir}/{id}.asset");
            if (e == null) Debug.LogError($"[SceneBuilder] 敌人数据 {id} 不存在，请先导入数值表");
            return e;
        }

        private static EncounterZone.EnemyGroup Group(int weight, params string[] ids) =>
            new() { weight = weight, members = ids.Select(Enemy).ToArray() };

        /// <summary>无碰撞的灰盒可见物体</summary>
        private static GameObject Visual(PrimitiveType type, string name, Transform parent, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            Greybox(go, color);
            return go;
        }

        /// <summary>标记为灰盒；给定颜色时同时生成灰盒材质（同名材质复用）</summary>
        private static void Greybox(GameObject go, Color? color = null)
        {
            go.AddComponent<GreyboxMarker>();
            if (color == null) return;
            var r = go.GetComponent<Renderer>();
            string path = $"{GreyboxMatDir}/M_{go.name}.mat";
            Directory.CreateDirectory(GreyboxMatDir);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                // 使用当前渲染管线的默认材质，URP 和内置管线都适用
                mat = new Material(r.sharedMaterial);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color.Value;
            r.sharedMaterial = mat;
        }

        #endregion
    }
}
