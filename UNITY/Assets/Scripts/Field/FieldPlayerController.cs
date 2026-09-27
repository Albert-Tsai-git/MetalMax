using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Field
{
    /// <summary>
    /// 野外移动：WASD / 方向键，俯视角平面移动。
    /// 使用 Input System（Unity 6 新工程默认启用）。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FieldPlayerController : MonoBehaviour
    {
        public float moveSpeed = 5f;
        public float turnSpeed = 720f;

        private CharacterController _cc;

        /// <summary>本帧实际移动的距离，供遇敌系统累计步数</summary>
        public float LastMoveDistance { get; private set; }

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
        }

        private void Start()
        {
            // 从战斗返回时，回到进入战斗前的位置
            if (GameSession.Instance.TryConsumeReturnPosition(out var pos))
            {
                _cc.enabled = false;
                transform.position = pos;
                _cc.enabled = true;
            }
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            var input = Vector2.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1;

            var dir = new Vector3(input.x, 0, input.y);
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            var before = transform.position;
            _cc.Move((dir * moveSpeed + Physics.gravity) * Time.deltaTime);
            var delta = transform.position - before;
            delta.y = 0;
            LastMoveDistance = delta.magnitude;

            if (dir.sqrMagnitude > 0.01f)
            {
                var target = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
            }
        }
    }
}
