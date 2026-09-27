using UnityEngine;

namespace Game.Tank
{
    /// <summary>底盘：决定武器孔数量与基础装甲</summary>
    [CreateAssetMenu(menuName = "Game/Tank/Chassis", fileName = "TNK_Chassis_")]
    public class ChassisData : TankPartData
    {
        [Header("底盘")]
        [Tooltip("武器孔，每个孔限定可装的武器类别")]
        public WeaponType[] weaponHoles = { WeaponType.MainCannon, WeaponType.SubGun };
        [Tooltip("底盘模型上武器挂点的名称，与 weaponHoles 一一对应")]
        public string[] weaponMountNames = { "Mount_Main", "Mount_Sub" };
        [Tooltip("可装载的人数")]
        [Min(1)] public int seats = 1;

        public override PartSlot Slot => PartSlot.Chassis;
    }
}
