#if UNITY_EDITOR
using System;
using System.IO;
using Game.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Presentation.Editor
{
    public static class FieldArtBuilder
    {
        private const string WorldDir = "Assets/Art/World";
        private const string SceneDir = "Assets/Scenes/Art";
        private const string FontPath = "Assets/Fonts/NotoSansSC-VF.ttf";
        private const string MapPath = "Assets/Art/World/MAP_Wasteland_Base.png";
        private const string GroundPath = "Assets/Art/World/TEX_Wasteland_SaltGround.png";
        private const string RawPath = "Assets/Art/World/Field_Wasteland_513.raw";
        private const string ScenePath = "Assets/Scenes/Art/Field_Art.unity";

        [MenuItem("Game/生成荒原美术场景与正式界面")]
        public static void BuildFromMenu() => Build();

        public static void BuildForBatch() => Build();

        private static void Build()
        {
            Directory.SetCurrentDirectory(Directory.GetParent(Application.dataPath).FullName);
            ConfigureTexture("Assets/Resources/Icons", TextureImporterType.Sprite, 256);
            ConfigureTexture(MapPath, TextureImporterType.Sprite, 1024);
            ConfigureGroundTexture();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            var map = AssetDatabase.LoadAssetAtPath<Sprite>(MapPath);
            if (font == null) throw new InvalidOperationException($"Noto Sans SC font missing or failed to import: {FontPath}");
            if (map == null) throw new InvalidOperationException($"Wasteland map sprite missing: {MapPath}");

            var data = BuildTerrainData();
            BuildFieldArt(data, font, map);
            AddUiToScene("Assets/Scenes/Art/Dungeon_PumpStation_Art.unity", font, map);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[FieldArt] Terrain + Field UI built with dedicated salt-ground texture.");
        }

        private static void ConfigureGroundTexture()
        {
            var importer = AssetImporter.GetAtPath(GroundPath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException($"Wasteland ground texture missing: {GroundPath}");
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static void ConfigureTexture(string pathOrDirectory, TextureImporterType type, int maxSize)
        {
            var paths = Directory.Exists(pathOrDirectory)
                ? Directory.GetFiles(pathOrDirectory, "*.png")
                : new[] { pathOrDirectory };
            foreach (var fullPath in paths)
            {
                var path = fullPath.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = type;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = maxSize;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }

        private static TerrainData BuildTerrainData()
        {
            var dataPath = $"{WorldDir}/TD_Field_Wasteland.asset";
            // Always rebuild from the authoritative RAW using Unity's serializer. This
            // also replaces any stale/corrupt TerrainData byte stream from an old import.
            if (File.Exists(dataPath) && !AssetDatabase.DeleteAsset(dataPath) && File.Exists(dataPath))
                throw new IOException($"Could not remove stale TerrainData asset: {dataPath}");
            var terrainData = new TerrainData();

            var raw = File.ReadAllBytes(RawPath);
            const int resolution = 513;
            if (raw.Length != resolution * resolution * 2)
                throw new InvalidDataException($"Expected {resolution}x{resolution} 16-bit RAW ({resolution * resolution * 2} bytes), got {raw.Length}");

            var heights = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                int i = (z * resolution + x) * 2;
                ushort value = (ushort)(raw[i] | (raw[i + 1] << 8));
                heights[z, x] = value / 65535f;
            }

            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{WorldDir}/TL_Wasteland.terrainlayer");
            if (layer == null) layer = new TerrainLayer();
            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(GroundPath);
            if (layer.diffuseTexture == null) throw new InvalidOperationException($"Wasteland ground texture failed to import: {GroundPath}");
            layer.tileSize = new Vector2(12, 12);
            layer.tileOffset = Vector2.zero;
            layer.diffuseRemapMin = new Vector4(.55f, .5f, .4f, 1);
            layer.diffuseRemapMax = Vector4.one;
            var layerPath = $"{WorldDir}/TL_Wasteland.terrainlayer";
            if (AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath) == null) AssetDatabase.CreateAsset(layer, layerPath);

            terrainData.heightmapResolution = resolution;
            terrainData.size = new Vector3(88, 12, 88);
            terrainData.SetHeights(0, 0, heights);
            terrainData.terrainLayers = new[] { layer };
            terrainData.alphamapResolution = 512;
            AssetDatabase.CreateAsset(terrainData, dataPath);
            EditorUtility.SetDirty(terrainData);
            AssetDatabase.SaveAssets();
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath) == null)
                throw new InvalidOperationException($"Unity failed to serialize/reload native TerrainData: {dataPath}");
            ValidatePads(terrainData);
            return terrainData;
        }

        private static void ValidatePads(TerrainData data)
        {
            var points = new[] { new Vector2(-15, -14), new Vector2(22, 24), new Vector2(-9, -14), new Vector2(0, -10), new Vector2(17, 19) };
            foreach (var point in points)
            for (int step = 0; step < 16; step++)
            {
                float angle = step * Mathf.PI * 2 / 16;
                var p = point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 6f;
                float u = (p.x + 44) / 88f;
                float v = (p.y + 44) / 88f;
                if (data.GetInterpolatedHeight(u, v) > 1f)
                    throw new InvalidOperationException($"I-23 interaction pad too steep/high: {point} sample {p} height={data.GetInterpolatedHeight(u, v):F2}m");
            }
        }

        private static void BuildFieldArt(TerrainData data, Font font, Sprite map)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.43f, .40f, .32f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 44;
            RenderSettings.fogEndDistance = 175;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.58f, .64f, .63f);
            RenderSettings.ambientEquatorColor = new Color(.46f, .43f, .35f);
            RenderSettings.ambientGroundColor = new Color(.20f, .18f, .14f);

            var terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = "Wasteland Terrain";
            terrainObject.transform.position = new Vector3(-44, 0, -44);
            var terrain = terrainObject.GetComponent<Terrain>();
            terrain.heightmapPixelError = 4;
            terrain.basemapDistance = 1200;
            terrain.drawInstanced = true;
            var collider = terrainObject.GetComponent<TerrainCollider>();
            if (collider == null) throw new InvalidOperationException("TerrainCollider was not created");

            var lightObject = new GameObject("Wasteland Sun", typeof(Light));
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, .83f, .62f);
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(47, -32, 0);

            AttachUi(scene, font, map);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void AddUiToScene(string path, Font font, Sprite map)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            AttachUi(scene, font, map);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
        }

        private static void AttachUi(Scene scene, Font font, Sprite map)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<FieldUiPresentation>(true) != null) return;
            var host = new GameObject("Field UI Presentation");
            SceneManager.MoveGameObjectToScene(host, scene);
            var presentation = host.AddComponent<FieldUiPresentation>();
            var serialized = new SerializedObject(presentation);
            serialized.FindProperty("font").objectReferenceValue = font;
            serialized.FindProperty("mapBase").objectReferenceValue = map;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
#endif
