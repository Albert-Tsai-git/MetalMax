using UnityEngine;

namespace Game.Field
{
    /// <summary>相机挂在玩家下面时，保持世界朝向不跟着玩家转（固定俯视角）</summary>
    public class KeepWorldRotation : MonoBehaviour
    {
        private Quaternion _rot;
        private Vector3 _offset;

        private void Start()
        {
            _rot = transform.rotation;
            _offset = transform.position - transform.parent.position;
        }

        private void LateUpdate()
        {
            transform.SetPositionAndRotation(transform.parent.position + _offset, _rot);
        }
    }
}
