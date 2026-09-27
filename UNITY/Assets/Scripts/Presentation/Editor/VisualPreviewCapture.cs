#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>在 Unity URP 中渲染预制体预览，检查实际材质与挂装轮廓。</summary>
    public static class VisualPreviewCapture
    {
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../ArtSource/UnityPreviews"));
            Directory.CreateDirectory(output);
            Capture("TNK_Chassis_Light", output);
            Capture("TNK_Chassis_Heavy", output);
            Debug.Log("[Visuals] Unity 预览渲染完成");
        }

        private static void Capture(string id, string output)
        {
            var chassisAsset = Resources.Load<GameObject>($"Visuals/{id}");
            if (chassisAsset == null) throw new InvalidOperationException($"[Visuals] 找不到 {id}");
            var chassis = UnityEngine.Object.Instantiate(chassisAsset);
            chassis.name = id;
            Attach(chassis.transform, "Mount_Main", id == "TNK_Chassis_Heavy" ? "WPN_ShockCannon" : "WPN_Cannon_75");
            Attach(chassis.transform, "Mount_Sub", id == "TNK_Chassis_Heavy" ? "WPN_Flamethrower" : "WPN_MG_77");
            if (id == "TNK_Chassis_Heavy") Attach(chassis.transform, "Mount_SE", "WPN_SE_Missile");

            var cameraObject = new GameObject("PreviewCamera");
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(7, 7, 9);
            cameraObject.transform.LookAt(new Vector3(0, .9f, 0));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.78f, .76f, .69f, 1);
            camera.fieldOfView = 35;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 100;

            var lightObject = new GameObject("PreviewSun");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            lightObject.transform.rotation = Quaternion.Euler(45, -30, 0);
            RenderTexture target = new RenderTexture(960, 720, 24);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var pixels = new Texture2D(960, 720, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 960, 720), 0, 0);
                pixels.Apply();
                var file = Path.Combine(output, id + ".png");
                File.WriteAllBytes(file, pixels.EncodeToPNG());
                Debug.Log($"[Visuals] 已渲染 {file}");
                UnityEngine.Object.DestroyImmediate(pixels);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(lightObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(chassis);
            }
        }

        private static void Attach(Transform root, string mountName, string weaponId)
        {
            var mount = Find(root, mountName);
            var weapon = Resources.Load<GameObject>($"Visuals/{weaponId}");
            if (mount == null || weapon == null) throw new InvalidOperationException($"[Visuals] 挂装失败：{mountName}/{weaponId}");
            var child = UnityEngine.Object.Instantiate(weapon, mount, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
        }

        private static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var match = Find(child, name);
                if (match != null) return match;
            }
            return null;
        }
    }
}
#endif
