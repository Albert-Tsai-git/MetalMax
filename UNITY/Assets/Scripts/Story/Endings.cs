namespace Game.Story
{
    /// <summary>
    /// 第三幕结局（INTERFACE I-23）：主控室抉择对话 DLG_Act3_Choice 的三个选项分别使用下列效果。
    /// 每个结局都设置 act3_choice_made（推进任务最后一步）与各自的结局标记，并给出不同的回报。
    /// </summary>
    public static class Endings
    {
        /// <summary>联合会接管并接受监督：联合会酬谢现金</summary>
        public const string Union = "set:act3_end_union; set:act3_choice_made; gold:+15000";

        /// <summary>公开账本、聚落共同调度：各聚落凑出的补给</summary>
        public const string Open = "set:act3_end_open; set:act3_choice_made; gold:+5000; item:ITM_ReviveKit:5; item:ITM_RepairPack:5";

        /// <summary>保留临时配给、拆分主控权：封闸守备交出的双核 C 装置</summary>
        public const string Split = "set:act3_end_split; set:act3_choice_made; gold:+5000; give:TNK_CUnit_Twin";

        public static readonly string[] All = { Union, Open, Split };
    }
}
