using System.Linq;
using Game.Core;
using Game.UI;
using Game.WorldMap;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Field
{
    /// <summary>
    /// 野外移动：WASD / 方向键，俯视角平面移动；F 下车 / 上车，步行时 Shift 奔跑。
    /// 乘车与步行的速度、加减速、转向不同；下车时战车停在原地（ParkedTank），可走回去上车。
    /// 对表现层公开 Speed / NormalizedSpeed / IsMoving / IsRunning / OnFoot，供动画使用（docs/INTERFACE.md I-21）。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FieldPlayerController : MonoBehaviour
    {
        [Header("步行")]
        public float walkSpeed = 4f;
        public float runSpeed = 6.5f;
        public float footAcceleration = 30f;
        public float footTurnSpeed = 720f;

        [Header("乘车")]
        public float tankSpeed = 7f;
        public float tankAcceleration = 10f;
        public float tankDeceleration = 16f;
        public float tankTurnSpeed = 240f;

        [Header("上车")]
        [Tooltip("离停放的战车多近可以上车（米）")]
        public float boardDistance = 3.5f;

        private CharacterController _cc;
        private Vector3 _velocity;
        private float _verticalSpeed;

        /// <summary>本帧实际移动的距离，供遇敌系统累计步数</summary>
        public float LastMoveDistance { get; private set; }
        /// <summary>当前水平速度（米/秒）</summary>
        public float Speed => new Vector3(_velocity.x, 0, _velocity.z).magnitude;
        /// <summary>水平速度相对奔跑速度（步行）或战车最高速度的比例，0~1</summary>
        public float NormalizedSpeed => Mathf.Clamp01(Speed / (OnFoot ? runSpeed : tankSpeed));
        public bool IsMoving => Speed > 0.1f;
        public bool IsRunning { get; private set; }
        /// <summary>是否下车步行</summary>
        public bool OnFoot => GameSession.Instance.State.vehicle.parked;

        /// <summary>当前所在地图（worldmap.csv 中与本场景对应的 Map），没有配置为 null</summary>
        public WorldMapData Map { get; private set; }

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            Map = WorldMapService.MapOfScene(CurrentScene);
        }

        private void Start()
        {
            // 从战斗返回时回到进入战斗前的位置；场景传送到出生点；全灭时回到最后到访城镇的入口
            var session = GameSession.Instance;
            if (session.TryConsumeReturnPosition(out var pos)) Teleport(pos);
            else if (SceneTravel.TryConsumeSpawn(out var spawnId))
            {
                var sp = SpawnPoint.Find(spawnId);
                if (sp != null) { Teleport(sp.transform.position); transform.rotation = sp.transform.rotation; }
                else Debug.LogWarning($"[Field] 场景中没有出生点 {spawnId}，保持原位");
            }
            else if (session.TryConsumeRespawnTown(out var townId))
            {
                var gate = TownGate.Find(townId);
                if (gate != null) Teleport(gate.RespawnPoint);
                else Debug.LogWarning($"[Field] 场景中没有城镇入口 {townId}，保持原位");
            }

            // 战车停在本场景时恢复停放位置
            var v = session.State.vehicle;
            if (v.parked && v.scene == CurrentScene) ParkedTank.Spawn(v.position, v.yaw);
            FieldEvents.RaiseModeChanged(OnFoot);
        }

        private static string CurrentScene => ArtSceneLoader.CurrentLogicScene ?? gameObjectSceneFallback;
        private static string gameObjectSceneFallback => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        private void OnEnable() => ArtSceneLoader.TerrainReady += SnapToGround;
        private void OnDisable() => ArtSceneLoader.TerrainReady -= SnapToGround;

        /// <summary>落到脚下地面（换成起伏的美术地形后避免卡在地下或悬空）</summary>
        private void SnapToGround() => Teleport(transform.position);

        private void Teleport(Vector3 pos)
        {
            // 从高处向下找地面（只认地形与灰盒地面，避免站到战车等物体顶上）
            _cc.enabled = false;
            foreach (var hit in Physics.RaycastAll(pos + Vector3.up * 50f, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore)
                         .OrderBy(h => h.distance))
            {
                if (!(hit.collider is TerrainCollider) && hit.collider.GetComponent<GreyboxGround>() == null) continue;
                pos.y = hit.point.y + _cc.height / 2f - _cc.center.y + _cc.skinWidth;
                break;
            }
            transform.position = pos;
            _cc.enabled = true;
            _velocity = Vector3.zero;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            LastMoveDistance = 0f;
            IsRunning = false;
            bool blocked = kb == null || UIRouter.BlocksFieldInput;

            if (!blocked && kb.fKey.wasPressedThisFrame)
            {
                if (OnFoot) TryBoard();
                else Dismount();
            }

            var input = Vector2.zero;
            if (!blocked)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1;
            }
            var dir = new Vector3(input.x, 0, input.y);
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            // 目标速度与加减速：步行灵活，战车起步慢、刹车稍快
            float maxSpeed, accel, turn;
            if (OnFoot)
            {
                IsRunning = dir.sqrMagnitude > 0.01f && kb != null && kb.shiftKey.isPressed;
                maxSpeed = IsRunning ? runSpeed : walkSpeed;
                accel = footAcceleration;
                turn = footTurnSpeed;
            }
            else
            {
                maxSpeed = tankSpeed;
                accel = dir.sqrMagnitude > 0.01f ? tankAcceleration : tankDeceleration;
                turn = tankTurnSpeed;
            }
            var horizontal = new Vector3(_velocity.x, 0, _velocity.z);
            horizontal = Vector3.MoveTowards(horizontal, dir * maxSpeed, accel * Time.deltaTime);

            // 重力：着地时保持轻微下压，离地时累积
            _verticalSpeed = _cc.isGrounded ? -2f : _verticalSpeed + Physics.gravity.y * Time.deltaTime;
            _velocity = new Vector3(horizontal.x, _verticalSpeed, horizontal.z);

            var before = transform.position;
            _cc.Move(_velocity * Time.deltaTime);
            // 地图边界：走到边缘停下
            var clamped = WorldMapService.Clamp(Map, transform.position);
            if (clamped != transform.position)
            {
                _cc.enabled = false;
                transform.position = clamped;
                _cc.enabled = true;
            }
            var delta = transform.position - before;
            delta.y = 0;
            LastMoveDistance = delta.magnitude;
            if (Map != null && LastMoveDistance > 0f)
                WorldMapService.Tick(GameSession.Instance.State, Map.entryId, transform.position);

            if (dir.sqrMagnitude > 0.01f)
            {
                var target = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turn * Time.deltaTime);
            }
        }

        /// <summary>下车：战车停在当前位置，全员步行</summary>
        public void Dismount()
        {
            var s = GameSession.Instance.State;
            if (OnFoot || !s.party.Any(p => p.tank != null)) return;
            s.vehicle = new VehicleState
            {
                parked = true, scene = CurrentScene, position = transform.position, yaw = transform.eulerAngles.y,
            };
            foreach (var p in s.party) p.inTank = false;
            var parked = ParkedTank.Spawn(transform.position, transform.eulerAngles.y);
            // 人从战车旁边下来，避免卡在战车碰撞体里
            Teleport(transform.position - transform.right * 2.2f);
            Debug.Log($"[Field] 下车，战车停在 {s.vehicle.position}");
            FieldEvents.RaiseDismounted(parked);
            FieldEvents.RaiseModeChanged(true);
        }

        /// <summary>上车：必须走到停放的战车旁边</summary>
        public bool TryBoard()
        {
            var parked = ParkedTank.Current;
            if (!OnFoot || parked == null) return false;
            if (Vector3.Distance(parked.transform.position, transform.position) > boardDistance)
            {
                Debug.Log("[Field] 离战车太远，无法上车");
                return false;
            }
            var s = GameSession.Instance.State;
            s.vehicle = new VehicleState();
            var owner = s.party.FirstOrDefault(p => p.tank != null && p.IsAlive);
            if (owner != null) owner.inTank = true;
            Teleport(parked.transform.position);
            transform.rotation = parked.transform.rotation;
            parked.Remove();
            Debug.Log("[Field] 上车");
            FieldEvents.RaiseBoarded();
            FieldEvents.RaiseModeChanged(false);
            return true;
        }
    }
}
