using Game.Core;
using UnityEngine;

namespace Game.Field
{
    /// <summary>
    /// 停放的战车（下车后留在原地）：有实体碰撞，靠近后按 E 或 F 上车。
    /// 外观为灰盒箱体（Greybox 子物体）；表现层可在其下挂战车模型并隐藏灰盒。
    /// </summary>
    public class ParkedTank : Interactable
    {
        /// <summary>当前场景中停放的战车，没有为 null</summary>
        public static ParkedTank Current { get; private set; }

        /// <summary>灰盒外观，表现层挂上模型后可隐藏</summary>
        public GameObject Greybox { get; private set; }

        public override string PromptKey => "UI.Interact.Board";

        public static ParkedTank Spawn(Vector3 position, float yaw)
        {
            if (Current != null) Current.Remove();
            var go = new GameObject("ParkedTank");
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(2.4f, 1.6f, 4.2f);
            box.center = new Vector3(0, -0.2f, 0);
            var pt = go.AddComponent<ParkedTank>();
            pt.interactRadius = 3.5f;

            var view = GameObject.CreatePrimitive(PrimitiveType.Cube);
            view.name = "ParkedTank_Greybox";
            Object.Destroy(view.GetComponent<Collider>());
            view.transform.SetParent(go.transform, false);
            view.transform.localScale = box.size;
            view.transform.localPosition = box.center;
            view.AddComponent<GreyboxMarker>();
            view.GetComponent<Renderer>().material.color = new Color(0.25f, 0.4f, 0.3f);
            pt.Greybox = view;

            Current = pt;
            return pt;
        }

        public void Remove()
        {
            if (Current == this) Current = null;
            Destroy(gameObject);
        }

        public override void Interact(PlayerState s)
        {
            var player = FindAnyObjectByType<FieldPlayerController>();
            player?.TryBoard();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (Current == this) Current = null;
        }
    }
}
