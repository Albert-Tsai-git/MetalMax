using Game.Core;
using UnityEngine;

namespace Game.Town
{
    /// <summary>城镇配置：由 Data/towns.csv 生成</summary>
    public class TownData : ScriptableObject
    {
        public string townId;
        [Tooltip("旅馆住宿费，住宿后全员 HP 回满")]
        [Min(0)] public int innPrice = 10;
        [Tooltip("镇上的商店 ID，没有商店为空")]
        public string shopId;
        [Tooltip("是否有车库（修理、改造、装卸部件）")]
        public bool hasGarage = true;
        [Tooltip("是否有赏金办事处（领取赏金）")]
        public bool hasBountyOffice;

        public string DisplayName => TextDB.Name(townId);
    }
}
