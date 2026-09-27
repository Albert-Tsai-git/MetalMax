using UnityEngine;

namespace Game.Tank
{
    /// <summary>引擎：提供载重上限</summary>
    [CreateAssetMenu(menuName = "Game/Tank/Engine", fileName = "TNK_Engine_")]
    public class EngineData : TankPartData
    {
        [Header("引擎")]
        [Tooltip("载重上限（吨）")]
        [Min(0f)] public float loadCapacity = 10f;
        [Tooltip("每级改造增加的载重（吨）")]
        [Min(0f)] public float loadPerUpgrade = 1f;

        public override PartSlot Slot => PartSlot.Engine;
    }
}
