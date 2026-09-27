using UnityEngine;

namespace Game.Tank
{
    /// <summary>C 装置：影响命中与回避</summary>
    [CreateAssetMenu(menuName = "Game/Tank/CUnit", fileName = "TNK_CUnit_")]
    public class CUnitData : TankPartData
    {
        [Header("C 装置")]
        [Range(0, 100)] public int hitBonus = 0;
        [Range(0, 100)] public int evadeBonus = 0;
        [Tooltip("每回合可执行的行动数")]
        [Min(1)] public int actionsPerTurn = 1;

        public override PartSlot Slot => PartSlot.CUnit;
    }
}
