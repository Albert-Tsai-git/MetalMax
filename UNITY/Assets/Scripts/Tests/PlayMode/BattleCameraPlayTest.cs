using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Battle;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.PlayTests
{
    /// <summary>
    /// 战斗镜头实机测试（I-27）：从野外进入战斗，验证默认敌左我右、Q/E 两向旋转 45°、R 复位。
    /// 表现由 Codex 实现，这里只按接口约定验收。
    /// </summary>
    public class BattleCameraPlayTest
    {
        private Keyboard _kb;
        private InputSettings _oldSettings;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _oldSettings = InputSystem.settings;
            var settings = ScriptableObject.CreateInstance<InputSettings>();
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = settings;
            _kb = InputSystem.AddDevice<Keyboard>("TestKeyboard");
            _kb.MakeCurrent();
            SceneManager.LoadScene(GameSession.FieldSceneName);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            InputSystem.QueueStateEvent(_kb, new KeyboardState());
            InputSystem.RemoveDevice(_kb);
            if (_oldSettings != null) InputSystem.settings = _oldSettings;
            yield return null;
        }

        private IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(_kb, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(_kb, new KeyboardState());
            yield return null;
        }

        private static float Yaw => Camera.main.transform.eulerAngles.y;

        private static float DeltaYaw(float a, float b) => Mathf.DeltaAngle(a, b);

        [UnityTest]
        public IEnumerator 默认敌左我右_QE旋转_R复位()
        {
            // 记录野外相机的默认姿态（相对玩家的偏移与朝向），用于战斗结束返回后比对
            var fieldPlayer = Object.FindAnyObjectByType<Game.Field.FieldPlayerController>().transform;
            var fieldCamOffset = Camera.main.transform.position - fieldPlayer.position;
            var fieldCamRot = Camera.main.transform.rotation;

            var enemies = new List<Combatant> { GameDB.Enemy("ENM_Ant").CreateCombatant(" 0"), GameDB.Enemy("ENM_Ant").CreateCombatant(" 1") };
            foreach (var e in enemies) { e.attack = 0; e.maxHp = e.hp = 99999; }
            GameSession.Instance.StartBattle(enemies, Vector3.zero);

            // 等战斗场景与表现舞台就绪
            float end = Time.realtimeSinceStartup + 10f;
            Transform stage = null;
            while (Time.realtimeSinceStartup < end && stage == null)
            {
                yield return null;
                if (SceneManager.GetActiveScene().name == GameSession.BattleSceneName)
                    stage = GameObject.Find("BattleVisualStage")?.transform;
            }
            Assert.NotNull(stage, "战斗表现舞台已生成");
            yield return null;

            var views = stage.GetComponentsInChildren<Transform>().Where(t => t.parent == stage && t.name.StartsWith("View_")).ToList();
            var foes = views.Where(v => v.name.StartsWith("View_ENM_")).ToList();
            var mine = views.Where(v => v.name.StartsWith("View_CHR_")).ToList();
            Assert.IsNotEmpty(foes, "有敌方单位");
            Assert.IsNotEmpty(mine, "有我方单位");
            Assert.True(foes.All(v => v.localPosition.x < 0), "敌方在左（X<0）");
            Assert.True(mine.All(v => v.localPosition.x > 0), "我方在右（X>0）");

            float yaw0 = Yaw;
            var pos0 = Camera.main.transform.position;
            yield return Tap(Key.E);
            Assert.AreEqual(45f, Mathf.Abs(DeltaYaw(yaw0, Yaw)), 0.5f, "E 旋转 45°");
            float afterE = DeltaYaw(yaw0, Yaw);
            yield return Tap(Key.Q);
            yield return Tap(Key.Q);
            Assert.AreEqual(-afterE, DeltaYaw(yaw0, Yaw), 0.5f, "Q 反向旋转（两次 Q 到另一侧 45°）");
            yield return Tap(Key.R);
            Assert.AreEqual(0f, DeltaYaw(yaw0, Yaw), 0.5f, "R 复位朝向");
            Assert.Less(Vector3.Distance(pos0, Camera.main.transform.position), 0.01f, "R 复位位置");
            Debug.Log($"[PlayTest] 战斗镜头：默认 yaw {yaw0:F1}，E 后变化 {afterE:F1}°，R 复位");

            // 转动后结束战斗（逃跑）回到野外：野外相机应保持默认姿态，不受战斗镜头旋转影响
            yield return Tap(Key.E);
            GameSession.Instance.EndBattle(BattleState.Escaped, 0, 0);
            end = Time.realtimeSinceStartup + 10f;
            Game.Field.FieldPlayerController back = null;
            while (Time.realtimeSinceStartup < end && back == null)
            {
                yield return null;
                if (SceneManager.GetActiveScene().name != GameSession.BattleSceneName)
                    back = Object.FindAnyObjectByType<Game.Field.FieldPlayerController>();
            }
            Assert.NotNull(back, "战斗结束后回到野外");
            yield return null;
            var offset = Camera.main.transform.position - back.transform.position;
            Assert.Less(Vector3.Distance(offset, fieldCamOffset), 0.05f, "野外相机相对玩家的位置保持默认");
            Assert.Less(Quaternion.Angle(Camera.main.transform.rotation, fieldCamRot), 0.5f, "野外相机朝向保持默认");
            Debug.Log("[PlayTest] 战斗结束返回野外：相机姿态保持默认");
        }
    }
}
