using System.Collections.Generic;
using Game.Core;
using Game.Items;
using Game.Tank;
using UnityEngine;

namespace Game.Economy
{
    /// <summary>商店配置：由 Data/shops.csv 生成，一行一件商品</summary>
    public class ShopData : ScriptableObject
    {
        public string shopId;
        public List<TankPartData> goods = new();
        public List<ItemData> items = new();
        public List<Game.Equipment.GearData> gear = new();

        public string DisplayName => TextDB.Name(shopId);
    }
}
