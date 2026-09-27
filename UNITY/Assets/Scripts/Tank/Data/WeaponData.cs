using UnityEngine;

namespace Game.Tank
{
    /// <summary>武器：主炮 / 副炮 / SE</summary>
    [CreateAssetMenu(menuName = "Game/Tank/Weapon", fileName = "WPN_")]
    public class WeaponData : TankPartData
    {
        [Header("武器")]
        public WeaponType weaponType = WeaponType.MainCannon;
        public AttackRange range = AttackRange.Single;
        [Min(0)] public int attack = 50;
        public Element element = Element.Normal;
        [Range(0, 100)] public int accuracy = 80;
        [Tooltip("弹药上限，-1 表示无限（副炮常用）")]
        public int maxAmmo = 20;
        [Tooltip("补充一发弹药的价格（无限弹药武器忽略）")]
        [Min(0)] public int ammoPrice = 10;
        [Tooltip("每级改造增加的攻击力")]
        [Min(0)] public int attackPerUpgrade = 10;

        public override PartSlot Slot => PartSlot.Weapon;
    }
}
