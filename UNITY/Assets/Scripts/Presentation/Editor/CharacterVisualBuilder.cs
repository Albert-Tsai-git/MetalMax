#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>Import the player character FBX files, create locomotion controllers and ID-addressable prefabs.</summary>
    public static class CharacterVisualBuilder
    {
        private static readonly string[] Ids = { "CHR_Hunter", "CHR_Mechanic" };
        private const string ModelDir = "Assets/Models";
        private const string PrefabDir = "Assets/Resources/Visuals";
        private const string ControllerDir = "Assets/Animations";

        [MenuItem("Tools/Characters/Build Player Character Visuals")]
        public static void Build()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EnsureFolder("Assets/Resources");
            EnsureFolder(PrefabDir);
            EnsureFolder(ControllerDir);
            foreach (var id in Ids) BuildOne(id);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[Characters] Hunter and Mechanic rigs, animations, controllers, and prefabs built.");
        }

        private static void BuildOne(string id)
        {
            var fbxPath = $"{ModelDir}/{id}.fbx";
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException($"[Characters] Missing FBX: {fbxPath}");
            importer.globalScale = 1f;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            var clipSettings = importer.defaultClipAnimations;
            if (clipSettings.Length == 0) throw new InvalidOperationException($"[Characters] No clips found in {fbxPath}");
            foreach (var clip in clipSettings)
            {
                var stableName = clip.name.Split('|').Last();
                if (stableName == "Idle" || stableName == "Walk" || stableName == "Run") clip.name = stableName;
                clip.loopTime = stableName == "Idle" || stableName == "Walk" || stableName == "Run";
                clip.loopPose = clip.loopTime;
            }
            importer.clipAnimations = clipSettings;
            importer.SaveAndReimport();

            var clips = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
            foreach (var clip in AssetDatabase.LoadAllAssetRepresentationsAtPath(fbxPath).OfType<AnimationClip>())
            {
                var stableName = clip.name.Split('|').Last();
                if (stableName == "__preview__") continue;
                if (clips.ContainsKey(stableName))
                    throw new InvalidOperationException($"[Characters] {id} has duplicate imported clip name '{stableName}'");
                clips.Add(stableName, clip);
            }
            foreach (var name in new[] { "Idle", "Walk", "Run", "Attack", "Hit", "Defeat" })
                if (!clips.ContainsKey(name))
                    throw new InvalidOperationException($"[Characters] {id} FBX is missing the {name} clip. Imported: {string.Join(", ", clips.Keys)}");

            var avatar = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid)
                throw new InvalidOperationException($"[Characters] {id} avatar is invalid");
            var controller = BuildController(id, clips);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null) throw new InvalidOperationException($"[Characters] Model did not import: {fbxPath}");

            var root = new GameObject(id);
            try
            {
                var visual = PrefabUtility.InstantiatePrefab(model) as GameObject;
                if (visual == null) throw new InvalidOperationException($"[Characters] Could not instantiate {id}");
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = Vector3.zero;
                // Unity adds a -90° X axis-conversion node for the Blender FBX;
                // cancel it so the authored Y-up / +Z-forward pose is upright in the game.
                visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                visual.transform.localScale = Vector3.one;
                var animator = visual.GetComponent<Animator>() ?? visual.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var renderers = visual.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException($"[Characters] {id} has no renderers");
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                if (bounds.size.y < 1.5f || bounds.size.y > 2.1f)
                    throw new InvalidOperationException($"[Characters] {id} height is outside 1.5–2.1m: {bounds.size}");
                var path = $"{PrefabDir}/{id}.prefab";
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                    throw new InvalidOperationException($"[Characters] Could not save {path}");
                if (Resources.Load<GameObject>($"Visuals/{id}") == null)
                    throw new InvalidOperationException($"[Characters] Resources could not load {id}");
                Debug.Log($"[Characters] {id}: bounds={bounds.size}, clips=Idle/Walk/Run, prefab={path}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static AnimatorController BuildController(string id, IReadOnlyDictionary<string, AnimationClip> clips)
        {
            var path = $"{ControllerDir}/{id}.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
                AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Running", AnimatorControllerParameterType.Bool);
            controller.AddParameter("OnFoot", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Defeat", AnimatorControllerParameterType.Trigger);

            var machine = controller.layers[0].stateMachine;
            machine.states = Array.Empty<ChildAnimatorState>();
            var idle = machine.AddState("Idle");
            idle.motion = clips["Idle"];
            var walk = machine.AddState("Walk");
            walk.motion = clips["Walk"];
            walk.speed = 1f;
            var run = machine.AddState("Run");
            run.motion = clips["Run"];
            run.speed = 1f;
            var attack = machine.AddState("Attack");
            attack.motion = clips["Attack"];
            var hit = machine.AddState("Hit");
            hit.motion = clips["Hit"];
            var defeated = machine.AddState("Defeat");
            defeated.motion = clips["Defeat"];
            machine.defaultState = idle;

            AddTransition(idle, walk, (AnimatorConditionMode.If, "Moving"), (AnimatorConditionMode.IfNot, "Running"));
            AddTransition(idle, run, (AnimatorConditionMode.If, "Moving"), (AnimatorConditionMode.If, "Running"));
            AddTransition(walk, idle, (AnimatorConditionMode.IfNot, "Moving"));
            AddTransition(walk, run, (AnimatorConditionMode.If, "Moving"), (AnimatorConditionMode.If, "Running"));
            AddTransition(run, idle, (AnimatorConditionMode.IfNot, "Moving"));
            AddTransition(run, walk, (AnimatorConditionMode.If, "Moving"), (AnimatorConditionMode.IfNot, "Running"));
            AddTriggerTransition(machine, attack, "Attack");
            AddTriggerTransition(machine, hit, "Hit");
            AddTriggerTransition(machine, defeated, "Defeat");
            AddReturnTransition(attack, idle);
            AddReturnTransition(hit, idle);
            EditorUtility.SetDirty(controller);
            return controller;
        }


        private static void AddTransition(AnimatorState source, AnimatorState destination, params (AnimatorConditionMode mode, string parameter)[] conditions)
        {
            var transition = source.AddTransition(destination);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = .08f;
            transition.offset = 0;
            transition.interruptionSource = TransitionInterruptionSource.None;
            foreach (var (mode, parameter) in conditions) transition.AddCondition(mode, 0, parameter);
        }

        private static void AddTriggerTransition(AnimatorStateMachine machine, AnimatorState destination, string trigger)
        {
            var transition = machine.AddAnyStateTransition(destination);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = .04f;
            transition.canTransitionToSelf = false;
            transition.interruptionSource = TransitionInterruptionSource.None;
            transition.AddCondition(AnimatorConditionMode.If, 0, trigger);
        }

        private static void AddReturnTransition(AnimatorState source, AnimatorState destination)
        {
            var transition = source.AddTransition(destination);
            transition.hasExitTime = true;
            transition.exitTime = 1f;
            transition.hasFixedDuration = true;
            transition.duration = .08f;
            transition.interruptionSource = TransitionInterruptionSource.None;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
