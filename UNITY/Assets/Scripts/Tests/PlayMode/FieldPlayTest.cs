using System.Collections;
using System.Linq;
using Game.Core;
using Game.Field;
using Game.UI;
using Game.WorldMap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.PlayTests
{
    /// <summary>
    /// 野外实机测试：真正进入 Play，加载 Field 场景，用模拟键盘驱动玩家，
    /// 验证移动、下车/奔跑/上车、地图与菜单界面、界面阻断操作、地图边界与地点发现。
    /// 运行：Unity -batchmode -runTests -testPlatform PlayMode（见 tools/playtest.sh）
    /// </summary>
    public class FieldPlayTest
    {
        private Keyboard _kb;
        private InputSettings _oldSettings;
        private FieldPlayerController _player;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // 批处理没有窗口焦点：临时改用独立设置，让输入不受焦点影响
            _oldSettings = InputSystem.settings;
            var settings = ScriptableObject.CreateInstance<InputSettings>();
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = settings;
            _kb = InputSystem.AddDevice<Keyboard>("TestKeyboard");
            _kb.MakeCurrent();
            // 每个用例从“在车上、无界面”开始
            var s = GameSession.Instance.State;
            s.vehicle = new VehicleState();
            var owner = s.party.FirstOrDefault(p => p.tank != null);
            if (owner != null) owner.inTank = true;
            SceneManager.LoadScene(GameSession.FieldSceneName);
            yield return null;
            yield return null;
            _player = Object.FindAnyObjectByType<FieldPlayerController>();
            Assert.NotNull(_player, "Field 场景中有玩家");
            UIRouter.CloseAll();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Release();
            InputSystem.RemoveDevice(_kb);
            if (_oldSettings != null) InputSystem.settings = _oldSettings;
            yield return null;
        }

        private void Hold(params Key[] keys)
        {
            // 只排队事件，由下一帧的输入更新处理，保证 wasPressedThisFrame 落在游戏脚本读取的那一帧
            InputSystem.QueueStateEvent(_kb, new KeyboardState(keys));
        }

        private void Release() => Hold();

        /// <summary>按下并松开（跨两帧，保证 wasPressedThisFrame 被读到）</summary>
        private IEnumerator Tap(Key key)
        {
            Hold(key);
            yield return null;
            Release();
            yield return null;
        }

        private IEnumerator HoldFor(float seconds, params Key[] keys)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                Hold(keys);
                yield return null;
            }
            Release();
            yield return null;
        }

        private Vector3 Pos => _player.transform.position;

        [UnityTest]
        public IEnumerator 乘车移动_下车奔跑_上车()
        {
            Assert.False(_player.OnFoot, "开局在车上");
            var start = Pos;
            yield return HoldFor(1f, Key.W);
            Assert.Greater(Pos.z - start.z, 2f, "按 W 战车向前移动");
            Debug.Log($"[PlayTest] 乘车 1 秒移动 {Pos.z - start.z:F2} 米");

            yield return Tap(Key.F);
            Assert.True(_player.OnFoot, "F 下车");
            var parked = ParkedTank.Current;
            Assert.NotNull(parked, "下车后有停放的战车");

            float maxSpeed = 0;
            bool running = false;
            float end = Time.time + 1f;
            while (Time.time < end)
            {
                Hold(Key.W, Key.LeftShift);
                yield return null;
                maxSpeed = Mathf.Max(maxSpeed, _player.Speed);
                running |= _player.IsRunning;
            }
            Release();
            Assert.True(running && maxSpeed > _player.walkSpeed + 0.5f, $"Shift 奔跑（最高速度 {maxSpeed:F2}）");
            Debug.Log($"[PlayTest] 奔跑最高速度 {maxSpeed:F2} 米/秒");

            // 远离战车时不能上车
            yield return Tap(Key.F);
            Assert.True(_player.OnFoot, "离战车太远不能上车");

            // 走回战车旁上车
            end = Time.time + 6f;
            while (Time.time < end && Vector3.Distance(Pos, parked.transform.position) > 2.5f)
            {
                var d = parked.transform.position - Pos;
                var keys = new System.Collections.Generic.List<Key>();
                if (d.z > 0.5f) keys.Add(Key.W);
                if (d.z < -0.5f) keys.Add(Key.S);
                if (d.x > 0.5f) keys.Add(Key.D);
                if (d.x < -0.5f) keys.Add(Key.A);
                Hold(keys.ToArray());
                yield return null;
            }
            Release();
            yield return null;
            yield return Tap(Key.F);
            Assert.False(_player.OnFoot, "走到战车旁 F 上车");
            yield return null;
            Assert.True(ParkedTank.Current == null, "上车后停放战车移除");
        }

        [UnityTest]
        public IEnumerator 地图与菜单界面_阻断操作()
        {
            yield return Tap(Key.M);
            Assert.AreEqual(UIScreen.WorldMap, UIRouter.Current, "M 打开地图");
            var start = Pos;
            yield return HoldFor(0.5f, Key.W);
            Assert.Less(Vector3.Distance(Pos, start), 0.1f, "地图打开时不能移动");
            yield return Tap(Key.M);
            Assert.AreEqual(UIScreen.None, UIRouter.Current, "再按 M 关闭地图");

            yield return Tap(Key.Escape);
            Assert.AreEqual(UIScreen.Menu, UIRouter.Current, "Esc 打开菜单");
            var tab = UIRouter.Tab;
            yield return Tap(Key.E);
            Assert.AreNotEqual(tab, UIRouter.Tab, "菜单中 E 切换分页");
            yield return Tap(Key.F);
            Assert.False(_player.OnFoot, "菜单打开时 F 不下车");
            yield return Tap(Key.Escape);
            Assert.AreEqual(UIScreen.None, UIRouter.Current, "Esc 关闭菜单");

            start = Pos;
            yield return HoldFor(1f, Key.W);
            Debug.Log($"[PlayTest] 关闭界面后 1 秒移动 {Vector3.Distance(Pos, start):F2} 米");
            Assert.Greater(Vector3.Distance(Pos, start), 2f, "关闭界面后恢复移动（与开局乘车 1 秒相当）");
        }

        [UnityTest]
        public IEnumerator 地图边界与地点发现()
        {
            var map = _player.Map;
            Assert.NotNull(map, "野外有地图配置");

            // 向东一直开，停在边界
            yield return HoldFor(7f, Key.D);
            Assert.LessOrEqual(Pos.x, map.Bounds.xMax + 0.01f, "不越过东边界");
            Assert.Greater(Pos.x, map.Bounds.xMax - 1f, "开到东边界附近");
            Assert.Greater(Pos.y, 0f, "仍站在地面上");
            Debug.Log($"[PlayTest] 东边界停在 x={Pos.x:F2}（边界 {map.Bounds.xMax}）");

            // 向西开回去，接近栈桥镇时发现（在镇口触发区之前停下）
            var s = GameSession.Instance.State;
            s.flags.Remove(WorldMapService.FoundPrefix + "TWN_Zhanqiao");
            float end = Time.time + 12f;
            while (Time.time < end && !WorldMapService.IsFound(s, "TWN_Zhanqiao"))
            {
                Hold(Pos.z > -12f ? new[] { Key.A, Key.S } : new[] { Key.A });
                yield return null;
            }
            Release();
            Assert.True(WorldMapService.IsFound(s, "TWN_Zhanqiao"), "走近栈桥镇后发现");
            Assert.True(WorldMapService.KnownLocations(s, map.entryId).Any(l => l.entryId == "TWN_Zhanqiao"), "地图上显示栈桥镇");
            Debug.Log($"[PlayTest] 在 {Pos} 发现栈桥镇");
        }
    }
}
