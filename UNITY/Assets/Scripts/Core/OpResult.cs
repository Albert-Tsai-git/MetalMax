namespace Game.Core
{
    /// <summary>
    /// 玩家操作（商店、车库、改造等）的结果。表现层用文本键 UI.Result.{枚举名} 显示提示。
    /// </summary>
    public enum OpResult
    {
        Ok,
        NotEnoughGold,      // 金钱不足
        NotInShop,          // 该商店不卖此物
        NotInInventory,     // 背包中没有该部件
        NoChassis,          // 没有底盘，无法安装武器
        HoleNotFound,       // 武器孔不存在
        HoleTypeMismatch,   // 武器类型与武器孔不符
        Overweight,         // 超重
        MaxUpgrade,         // 已达最大改造等级
        CannotRemove,       // 底盘和引擎只能替换，不能卸下
        SlotEmpty,          // 该位置没有部件
        UnknownPart,        // 未知部件类型
        NothingToDo,        // 无需操作（如没有需要修理的部件）
        SlotNotFound,       // 存档槽位不存在
        IoError,            // 文件读写失败
        Corrupted,          // 存档损坏，无法读取
        NotFound,           // 找不到对应数据（如城镇 ID）
        NoBountyOffice,     // 该城镇没有赏金办事处
        ChoiceRequired,     // 对话节点有选项，需要选择
        StackFull,          // 道具已达持有上限
        InvalidTarget,      // 目标无效
    }
}
