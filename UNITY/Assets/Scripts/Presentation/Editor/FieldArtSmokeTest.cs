#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Presentation.Editor
{
    public static class FieldArtSmokeTest
    {
        public static void RunForBatch()
        {
            if (!EditorBuildSettings.scenes.Any(s => s.enabled && s.path == "Assets/Scenes/Art/Field_Art.unity"))
                throw new InvalidOperationException("Field_Art is not enabled in Build Settings; runtime ArtSceneLoader will skip the terrain and UI");

            var icons = Resources.LoadAll<Sprite>("Icons");
            if (icons.Length != 15) throw new InvalidOperationException($"Expected 15 Resources/Icons sprites; got {icons.Length}");
            if (Resources.Load<Sprite>("Icons/UI_Icon_Map") == null) throw new InvalidOperationException("Map icon Resources.Load failed");
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansSC-VF.ttf");
            if (font == null) throw new InvalidOperationException("Noto Sans SC did not import as a Font");
            foreach (var id in new[] { "MAP_Wasteland", "MAP_PumpStation", "LOC_PumpStation", "LOC_PumpCrabNest" })
                if (!File.ReadAllText("Data/Text/text_zh.csv").Contains(id + ".name,"))
                    throw new InvalidOperationException($"Missing required map text key {id}.name");
            foreach (var key in new[] { "UI.Screen.Menu", "UI.Screen.WorldMap", "UI.Menu.Party", "UI.Menu.Items", "UI.Menu.Tank", "UI.Menu.Quests", "UI.Menu.System" })
                if (!File.ReadAllText("Data/Text/text_zh.csv").Contains(key + ","))
                    throw new InvalidOperationException($"Missing required I-22 text key {key}");

            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Art/Field_Art.unity", OpenSceneMode.Single);
            var terrain = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Terrain>(true)).FirstOrDefault();
            if (terrain == null || terrain.terrainData == null) throw new InvalidOperationException("Field_Art has no Terrain/TerrainData");
            if (terrain.GetComponent<TerrainCollider>() == null) throw new InvalidOperationException("Field_Art TerrainCollider missing");
            var data = terrain.terrainData;
            if (AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Art/World/TD_Field_Wasteland.asset") == null)
                throw new InvalidOperationException("Unity cannot reload the persisted native TerrainData asset");
            if (data.terrainLayers.Length != 1 || data.terrainLayers[0].diffuseTexture == null ||
                AssetDatabase.GetAssetPath(data.terrainLayers[0].diffuseTexture) != "Assets/Art/World/TEX_Wasteland_SaltGround.png")
                throw new InvalidOperationException("Terrain must use the dedicated salt-ground diffuse texture, not the map artwork");
            if (data.terrainLayers[0].diffuseTexture.wrapMode != TextureWrapMode.Repeat)
                throw new InvalidOperationException("Terrain salt-ground texture must tile with Repeat wrap mode");
            if (data.heightmapResolution != 513 || data.size != new Vector3(88, 12, 88))
                throw new InvalidOperationException($"Terrain shape mismatch: {data.heightmapResolution}, {data.size}");

            var pads = new[] { new Vector2(-15, -14), new Vector2(22, 24), new Vector2(-9, -14), new Vector2(0, -10), new Vector2(17, 19) };
            foreach (var center in pads)
            for (int ring = 0; ring <= 6; ring++)
            for (int spoke = 0; spoke < 16; spoke++)
            {
                float angle = spoke * Mathf.PI * 2 / 16;
                var p = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring;
                var uv = new Vector2((p.x + 44) / 88f, (p.y + 44) / 88f);
                float height = data.GetInterpolatedHeight(uv.x, uv.y);
                float slope = Vector3.Angle(data.GetInterpolatedNormal(uv.x, uv.y), Vector3.up);
                if (height < -1f || height > 1f || slope > 30f)
                    throw new InvalidOperationException($"I-23 pad {center} fails at {p}: height={height:F2}m slope={slope:F1}deg");
            }

            float encounterMaxSlope = 0;
            for (float z = 2; z <= 22; z += 1f)
            for (float x = -15; x <= 15; x += 1f)
            {
                var uv = new Vector2((x + 44) / 88f, (z + 44) / 88f);
                encounterMaxSlope = Mathf.Max(encounterMaxSlope, Vector3.Angle(data.GetInterpolatedNormal(uv.x, uv.y), Vector3.up));
            }
            if (encounterMaxSlope > 30f) throw new InvalidOperationException($"Encounter walkable area max slope {encounterMaxSlope:F1}deg");

            float edgeMin = 12f;
            for (int i = 0; i <= 64; i++)
            {
                float t = i / 64f;
                edgeMin = Mathf.Min(edgeMin, data.GetInterpolatedHeight(0, t), data.GetInterpolatedHeight(1, t),
                    data.GetInterpolatedHeight(t, 0), data.GetInterpolatedHeight(t, 1));
            }
            if (edgeMin <= 5f) throw new InvalidOperationException($"Terrain perimeter is not a raised natural barrier: min edge={edgeMin:F2}m");

            var ui = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<FieldUiPresentation>(true)).FirstOrDefault();
            if (ui == null) throw new InvalidOperationException("Field_Art UI presentation component missing");
            var serializedUi = new SerializedObject(ui);
            if (serializedUi.FindProperty("font").objectReferenceValue == null || serializedUi.FindProperty("mapBase").objectReferenceValue == null)
                throw new InvalidOperationException("Field_Art UI font/map references are not assigned");

            var markerMethod = typeof(FieldUiPresentation).GetMethod("Marker", BindingFlags.Instance | BindingFlags.NonPublic);
            if (markerMethod == null) throw new InvalidOperationException("Field UI marker builder was not found");
            var markerRoot = new GameObject("Field UI marker smoke root", typeof(RectTransform));
            try
            {
                var marker = markerMethod.Invoke(ui, new object[] { markerRoot.transform, "MapMark_Player", new Vector2(.5f, .5f), Color.red, 22f }) as UnityEngine.UI.Image;
                if (marker == null || marker.GetComponent<UnityEngine.UI.Text>() != null ||
                    marker.transform.Find("Glyph")?.GetComponent<UnityEngine.UI.Text>() == null)
                    throw new InvalidOperationException("Map marker must keep its glyph Text on a separate child object");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(markerRoot);
            }

            Debug.Log($"[FieldArtSmoke] PASS icons={icons.Length}, font={font.name}, nativeTerrainData reload, marker glyph child, heightmap={data.heightmapResolution}, encounterMaxSlope={encounterMaxSlope:F1}deg, padRadius=6m, minEdge={edgeMin:F1}m, TerrainCollider+UI bound");
        }
    }
}
#endif
