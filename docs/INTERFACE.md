# 对接接口（Claude × Codex）

只记录双方必须一致的约定。每条须双方认可才生效；修改条款时把双方状态重置为“待认可”。
状态：✅ 认可　⏳ 待认可　✏️ 提出修改（附在条款后）

| 编号 | 条款 | Claude | Codex |
|---|---|---|---|
| I-01 | 数据 ID 前缀：`TNK_` 车体部件、`WPN_` 武器、`ENM_` 敌人（赏金首 `ENM_Bounty_`）、`CHR_` 角色、`ENV_` 环境。ID 由 Claude 定义，定义后不改名。 | ✅ | ✅ |
| I-02 | 文本表 `UNITY/Data/Text/text_{语言}.csv`，列 `key,text`，UTF-8。键：`{ID}.name`、`{ID}.desc`；UI 文字键 `UI.{界面}.{项}`。键名由需求方新增，text 由 Codex 填写。 | ✅ | ✅ |
| I-03 | 模型预制体 `UNITY/Assets/Resources/Visuals/{ID}.prefab`，图标 `UNITY/Assets/Resources/Icons/{ID}.png`（Sprite）。文件名与 ID 完全一致；缺失时逻辑正常运行、不显示外观。 | ✅ | ✅ |
| I-04 | 模型规格：1 单位 = 1 米，Y 轴向上，前方 +Z；预制体根节点为单位变换；武器枢轴在安装点、炮口朝 +Z。 | ✅ | ✅ |
| I-05 | 底盘挂点空节点名：`Mount_Main`、`Mount_Sub`、`Mount_SE`。各底盘拥有哪些挂点以 `UNITY/Data/parts.csv` 的 `mounts` 列为准：`TNK_Chassis_Light` = Main、Sub；`TNK_Chassis_Heavy` = Main、Sub、SE。 | ✅ | ✅ |
| I-06 | 场景：逻辑场景 `Assets/Scenes/Logic/{名称}.unity`（当前 `Field`、`Battle`）；美术场景 `Assets/Scenes/Art/{名称}_Art.unity`，运行时叠加加载并设为活动场景，灰盒自动隐藏。美术场景只放可见物体、灯光、后处理，不放碰撞体与逻辑组件。 | ✅ | ✅ |
| I-07 | 表现层脚本放在 `Assets/Scripts/Presentation/`（程序集 `Game.Presentation`，引用 `Game.Runtime`）；只读取逻辑层数据、订阅事件，不修改游戏状态。逻辑层不引用表现层。 | ✅ | ✅ |
| I-08 | 逻辑层对外可读数据：`GameSession.Instance`（`party`、`gold`、`exp`）；`TankLoadout`（部件、`OnChanged` 事件）；`TextDB.Name/Desc/Get`；`VisualCatalog.Model/Icon`。新增接口先登记到本表。 | ✅ | ✅ |
| I-09 | 战斗事件 `Game.Battle.BattleEvents`（静态，OnEnable 订阅 / OnDisable 取消）：`Started(BattleSystem)`、`TurnStarted(int 回合)`、`ActionStarted(行动者, ActionType, 武器或 null)`、`Missed(攻击者, 目标)`、`Hit(攻击者, 目标, 伤害, 是否打在战车)`、`PartDamaged(所属者, PartInstance)`、`TankDisabled(所属者)`、`BoardChanged(行动者, 是否在车上)`、`Defeated(单位)`、`EscapeAttempted(行动者, 是否成功)`、`Ended(BattleState, 经验, 金钱)`。 | ✅ | ✅ |
| I-10 | 战斗单位 `Combatant.id` 为数据 ID（`ENM_` / `CHR_`），表现层据此取模型（I-03）与文本（I-02）；演示单位可能为空。 | ✅ | ✅ |
| I-11 | 商店与车库（`Game.Economy`）：`ShopService.Buy/Sell/SellPrice`、`GarageService.Equip/Unequip/Upgrade/Repair/FillArmor`，均返回 `Game.Core.OpResult`；玩家状态 `GameSession.Instance.State`（`PlayerState`：`Gold`、`inventory`）；商店 `GameDB.Shop(ID).goods`。事件 `EconomyEvents`：`GoldChanged(旧, 新)`、`InventoryChanged()`、`Bought(商店ID, 部件)`、`Sold(部件, 金钱)`、`Upgraded(部件)`。 | ✅ | ✅ |
| I-12 | 操作结果提示文本键 `UI.Result.{OpResult 枚举名}`（如 `UI.Result.NotEnoughGold`）；商店 ID 前缀 `SHP_`，名称键 `{ID}.name`。 | ✅ | ✅ |
| I-13 | 存档（`Game.Core.Save`）：3 个槽位（0～2）；`GameSession.Instance.SaveGame(槽位, 玩家位置)`、`LoadGame(槽位)`；存档列表用 `SaveSystem.Peek(槽位)`（`savedAt`、`gold`、`scene`），`SaveSystem.Exists/Delete`；结果为 `OpResult`（新增 `SlotNotFound`、`IoError`、`Corrupted`，文本键同 I-12）。事件 `SaveSystem.Saved(槽位, 结果)`、`Loaded(槽位, 结果)`。 | ✅ | ✅ |
| I-14 | 城镇与赏金首（`Game.Town`）：城镇 ID 前缀 `TWN_`（名称键 `{ID}.name`），`GameDB.Town(ID)`（`innPrice`、`shopId`、`hasGarage`、`hasBountyOffice`）；`TownService.Enter/Leave/Rest`、`BountyService.All/StateOf/ClaimAll` 返回 `OpResult`（新增 `NotFound`、`NoBountyOffice`）；`PlayerState.lastTown`、`bounties`。野外 `TownGate.Current` 为当前所在城镇入口（null 表示不在城镇）。事件 `TownEvents`：`Entered(城镇ID)`、`Left(城镇ID)`、`Rested(城镇ID, 花费)`、`BountyDefeated(敌人ID)`、`BountyClaimed(敌人ID, 金额)`。 | ✅ | ⏳ |
| I-15 | 角色成长（`Game.Progression`）：`Combatant.level`、`Combatant.exp`（当前等级内经验）；`LevelService.ExpRemaining(角色)`（满级为 -1）；角色数据 `GameDB.Character(CHR_ID)`（`ExpToNext(等级)`）。事件 `ProgressionEvents`：`ExpGained(角色, 经验)`、`LeveledUp(角色, 新等级)`（连升多级时逐级触发）。 | ✅ | ⏳ |

## 待定条款

| 编号 | 事项 | 提出方 |
|---|---|---|
| P-02 | Animator 参数名、音效与特效触发键 | Codex |
