#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>把 Blender 导出的 FBX 包装成按数据 ID 加载的表现层预制体。</summary>
    public static class VisualPrefabBuilder
    {
        private static readonly string[] Ids =
        {
            "TNK_Chassis_Light", "TNK_Chassis_Heavy", "WPN_Cannon_75", "WPN_MG_77", "WPN_SE_Missile"
        };

        public static void Build()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EnsureFolder("Assets/Resources/Visuals");
            foreach (var id in Ids)
            {
                var modelPath = $"Assets/Models/{id}.fbx";
                var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
                if (importer == null) throw new InvalidOperationException($"[Visuals] 缺少 FBX 导入器：{modelPath}");
                if (!Mathf.Approximately(importer.globalScale, 100f))
                {
                    importer.globalScale = 100f;
                    importer.SaveAndReimport();
                }
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (model == null) throw new InvalidOperationException($"[Visuals] 未导入 {modelPath}");

                var root = new GameObject(id);
                try
                {
                    var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                    visual.transform.SetParent(root.transform, false);
                    visual.transform.localPosition = Vector3.zero;
                    visual.transform.localRotation = Quaternion.identity;
                    visual.transform.localScale = Vector3.one;
                    if (id.StartsWith("TNK_", StringComparison.Ordinal))
                    {
                        Require(root.transform, "Mount_Main");
                        Require(root.transform, "Mount_Sub");
                        if (id == "TNK_Chassis_Heavy") Require(root.transform, "Mount_SE");
                    }

                    var renderers = root.GetComponentsInChildren<Renderer>();
                    if (renderers.Length == 0) throw new InvalidOperationException($"[Visuals] {id} 没有网格");
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                    Debug.Log($"[Visuals] {id} bounds={bounds.size}, center={bounds.center}");
                    if (id.StartsWith("TNK_", StringComparison.Ordinal))
                    {
                        if (bounds.size.x < 1.8f || bounds.size.x > 4.0f ||
                            bounds.size.y < 1.0f || bounds.size.y > 3.0f ||
                            bounds.size.z < 4.0f || bounds.size.z > 6.5f)
                            throw new InvalidOperationException($"[Visuals] {id} 底盘轴向或米制尺寸错误：{bounds.size}");
                    }
                    else if (bounds.size.z < .6f || bounds.size.z > 2.5f || bounds.size.y > 1.5f)
                    {
                        throw new InvalidOperationException($"[Visuals] {id} 武器轴向或米制尺寸错误：{bounds.size}");
                    }

                    var prefabPath = $"Assets/Resources/Visuals/{id}.prefab";
                    if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
                        throw new InvalidOperationException($"[Visuals] 保存失败：{prefabPath}");
                    if (Resources.Load<GameObject>($"Visuals/{id}") == null)
                        throw new InvalidOperationException($"[Visuals] Resources 加载失败：{id}");
                    Debug.Log($"[Visuals] 已生成 {prefabPath}");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
            AssetDatabase.SaveAssets();
        }

        private static void EnsureFolder(string path)
        {
            var parent = "Assets/Resources";
            if (!AssetDatabase.IsValidFolder(parent)) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, "Visuals");
        }

        private static void Require(Transform root, string name)
        {
            var mount = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
            if (mount == null)
                throw new InvalidOperationException($"[Visuals] {root.name} 缺少挂点 {name}");
            if (mount.position.y < 1f || Vector3.Dot(mount.forward, Vector3.forward) < .99f)
                throw new InvalidOperationException($"[Visuals] {root.name}/{name} 位置或朝向错误：{mount.position}, {mount.forward}");
            Debug.Log($"[Visuals] {root.name}/{name} pos={mount.position}, forward={mount.forward}");
        }
    }
}
#endif
