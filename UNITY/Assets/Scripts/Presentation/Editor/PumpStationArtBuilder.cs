#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

namespace Game.Presentation
{
    /// <summary>生成只含可见网格、灯光的泵站美术场景；逻辑组件与碰撞仍在逻辑场景。</summary>
    public static class PumpStationArtBuilder
    {
        private const string ScenePath = "Assets/Scenes/Art/Dungeon_PumpStation_Art.unity";
        private const string MaterialDir = "Assets/Materials";
        private static readonly Dictionary<string, Material> Materials = new();

        public static void Build()
        {
            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/Scenes/Art");
            EnsureFolder(MaterialDir);
            var concrete = GetMaterial("M_Art_Pump_Concrete", new Color(.48f, .49f, .46f));
            var paleConcrete = GetMaterial("M_Art_Pump_PaleConcrete", new Color(.65f, .63f, .56f));
            var iron = GetMaterial("M_Art_Pump_Iron", new Color(.19f, .23f, .23f));
            var salt = GetMaterial("M_Art_Pump_Salt", new Color(.76f, .72f, .61f));
            var orange = GetMaterial("M_Art_Pump_Lamp", new Color(1f, .28f, .055f), true);
            var water = GetMaterial("M_Art_Pump_Waterline", new Color(.31f, .43f, .43f));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.42f, .43f, .41f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = false;
            RenderSettings.fogColor = new Color(.42f, .43f, .40f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 52;
            RenderSettings.fogEndDistance = 120;

            Box("Floor_SaltConcrete", new Vector3(0, -.38f, 41), new Vector3(12, .75f, 84), salt);
            Box("Floor_ServiceTrack", new Vector3(0, .015f, 41), new Vector3(3.4f, .04f, 82), concrete);
            Box("Ceiling_MainPipe", new Vector3(0, 3.05f, 41), new Vector3(.46f, .46f, 82), iron);

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 5.78f;
                Box($"Wall_Continuous_{side}", new Vector3(x, 1.9f, 41), new Vector3(.42f, 3.8f, 82), concrete);
                Box($"Waterline_{side}", new Vector3(side * 5.55f, .83f, 41), new Vector3(.035f, .13f, 81), water);
                Cylinder($"Pipe_Lateral_{side}", new Vector3(side * 5.1f, 2.95f, 41), .15f, 82, iron, new Vector3(90, 0, 0));
                Cylinder($"Pipe_Lateral_Lower_{side}", new Vector3(side * 4.82f, 2.35f, 41), .095f, 82, paleConcrete, new Vector3(90, 0, 0));
                for (int z = 8; z <= 72; z += 16)
                {
                    Box($"WallButtress_{side}_{z}", new Vector3(side * 5.48f, 1.9f, z), new Vector3(.32f, 3.6f, .75f), paleConcrete);
                    Box($"LampHousing_{side}_{z}", new Vector3(side * 5.2f, 2.72f, z), new Vector3(.22f, .30f, .55f), iron);
                    var glow = Box($"LampLens_{side}_{z}", new Vector3(side * 5.06f, 2.72f, z), new Vector3(.05f, .16f, .34f), orange);
                    AddLamp(glow.transform.position, 3.2f);
                }
            }

            foreach (int z in new[] { 18, 42, 64 })
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * 4.0f;
                    Box($"PumpPedestal_{side}_{z}", new Vector3(x, .42f, z), new Vector3(1.4f, .85f, 1.4f), iron);
                    Cylinder($"PumpHousing_{side}_{z}", new Vector3(x, 1.25f, z), .56f, 1.0f, concrete);
                    Cylinder($"ValveWheel_{side}_{z}", new Vector3(x, 1.65f, z + side * .42f), .42f, .10f, orange, new Vector3(90, 0, 0));
                    Box($"GaugePlate_{side}_{z}", new Vector3(x, 1.48f, z - side * .44f), new Vector3(.48f, .36f, .07f), paleConcrete);
                }
            }

            // Rusted service hoops and readable maintenance markings above chest and encounter landmarks.
            foreach (int z in new[] { 12, 28, 44, 60, 76 })
            {
                Box($"FloorMark_Left_{z}", new Vector3(-2.6f, .045f, z), new Vector3(.85f, .025f, 1.3f), paleConcrete);
                Box($"FloorMark_Right_{z}", new Vector3(2.6f, .045f, z), new Vector3(.85f, .025f, 1.3f), paleConcrete);
                Box($"Marking_Dash_{z}", new Vector3(0, .045f, z), new Vector3(1.0f, .03f, .20f), orange);
            }

            var sun = new GameObject("Art_DirectionalLight").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, .91f, .76f);
            sun.intensity = 1.8f;
            sun.transform.rotation = Quaternion.Euler(48, -28, 0);

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[Art] 已保存 {ScenePath}（美术对象，不含碰撞体或逻辑组件）");
        }

        public static void CapturePreview()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var obj in scene.GetRootGameObjects())
                foreach (var collider in obj.GetComponentsInChildren<Collider>(true))
                    throw new System.InvalidOperationException($"[Art] 美术场景包含碰撞体：{collider.name}");

            var cameraObject = new GameObject("PreviewCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 12, -6);
            camera.transform.LookAt(new Vector3(0, 1, 28));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.25f, .27f, .26f);
            camera.fieldOfView = 58;
            camera.farClipPlane = 120;
            var target = new RenderTexture(960, 720, 24);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(960, 720, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 960, 720), 0, 0);
                image.Apply();
                var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../ArtSource/UnityPreviews/Dungeon_PumpStation_Art.png"));
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.WriteAllBytes(output, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                Debug.Log($"[Art] 已渲染泵站预览 {output}");
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
            }
        }

        private static Material GetMaterial(string name, Color color, bool emissive = false)
        {
            if (Materials.TryGetValue(name, out var found)) return found;
            var path = $"{MaterialDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = name, color = color };
                if (emissive)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color * 2.5f);
                }
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            Materials[name] = material;
            return material;
        }

        private static GameObject Box(string name, Vector3 position, Vector3 scale, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.position = position;
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            var physics = obj.GetComponent<Collider>();
            if (physics != null) Object.DestroyImmediate(physics);
            return obj;
        }

        private static GameObject Cylinder(string name, Vector3 position, float radius, float length, Material material, Vector3 rotation = default)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            obj.name = name;
            obj.transform.position = position;
            obj.transform.rotation = Quaternion.Euler(rotation);
            obj.transform.localScale = new Vector3(radius * 2, length / 2, radius * 2);
            obj.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(obj.GetComponent<Collider>());
            return obj;
        }

        private static void AddLamp(Vector3 position, float range)
        {
            var obj = new GameObject("Art_AmberMaintenanceLight");
            obj.transform.position = position;
            var light = obj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, .32f, .08f);
            light.intensity = 1.8f;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
