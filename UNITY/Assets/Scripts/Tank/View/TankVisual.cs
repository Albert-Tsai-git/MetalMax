using System.Collections.Generic;
using UnityEngine;

namespace Game.Tank
{
    /// <summary>
    /// 战车外观：根据 TankLoadout 实例化底盘，并把武器挂到底盘的挂点上。
    /// 底盘模型需要在 Blender 里用 Empty 做挂点，命名与 ChassisData.weaponMountNames 一致。
    /// </summary>
    public class TankVisual : MonoBehaviour
    {
        private TankLoadout _loadout;
        private GameObject _chassisObj;
        private readonly List<GameObject> _weaponObjs = new();

        public void Bind(TankLoadout loadout)
        {
            if (_loadout != null) _loadout.OnChanged -= Rebuild;
            _loadout = loadout;
            _loadout.OnChanged += Rebuild;
            Rebuild();
        }

        private void OnDestroy()
        {
            if (_loadout != null) _loadout.OnChanged -= Rebuild;
        }

        /// <summary>重建外观。换装频率低，直接销毁重建即可。</summary>
        public void Rebuild()
        {
            if (_chassisObj != null) Destroy(_chassisObj);
            _weaponObjs.Clear();

            if (_loadout?.chassis?.data.modelPrefab == null) return;
            var cd = (ChassisData)_loadout.chassis.data;
            _chassisObj = Instantiate(cd.modelPrefab, transform);

            for (int i = 0; i < _loadout.weapons.Count; i++)
            {
                var wp = _loadout.weapons[i];
                if (wp?.data.modelPrefab == null || i >= cd.weaponMountNames.Length) continue;

                var mount = FindDeep(_chassisObj.transform, cd.weaponMountNames[i]);
                if (mount == null)
                {
                    Debug.LogWarning($"[TankVisual] 底盘 {cd.displayName} 缺少挂点 {cd.weaponMountNames[i]}");
                    continue;
                }
                _weaponObjs.Add(Instantiate(wp.data.modelPrefab, mount, false));
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
