# 协作约定（Claude × Codex）

> 版本：v0.9（草案）　|　更新日期：2026-09-27　|　适用项目：`D:\code\GAME`（Unity 工程位于 `UNITY/`，Unity 6 + URP）
> 本文件是两个 AI 共同遵守的唯一协作约定。任何一方修改“接口约定”一节时，须在文末变更记录中写明，并由用户转达另一方。

---

## 1. 职责分工

**划分原则（用户 2026-09-27 确定）**：Codex 负责“玩家看到、听到、读到的内容与表现”——故事背景、文案、美术、界面、模型、动效；Claude 负责“游戏如何运转”——游戏逻辑与机制。交界处按“内容/表现归 Codex，规则/驱动归 Claude”判定。

### 1.1 Codex 负责

| 领域 | 范围 |
|---|---|
| 故事背景 | 世界观、势力、历史、角色与 NPC 人设、剧情大纲与分支内容 |
| 文案 | 对话台词、任务描述、物品/部件/敌人名称与说明、UI 文字、教程文本；多语言文本内容与翻译 |
| 美术 | 概念图、原画、材质/贴图/Shader、色彩与风格规范（`ART_BIBLE.md`） |
| 模型 | Blender 建模、导出、挂点节点、LOD、资产导入设置 |
| 界面 | UI 布局、样式、图标、字体，以及 UI 表现层 C# 脚本（订阅 Claude 提供的数据与事件） |
| 动效 | 角色/战车动画、Animator、粒子与特效、UI 动效、战斗运镜与镜头构图 |
| 音频 | 音效、BGM、Audio Mixer 与混音 |
| 环境与布景 | 世界地图、城镇、迷宫的美术布景（以预制体或美术场景交付）、光照、后处理 |
| 渲染工程配置 | URP、Quality、Graphics 设置；渲染性能（Draw Call、贴图压缩、LOD） |
| 表现层验收 | 视觉与听觉效果在 Unity 中的实际验收 |

### 1.2 Claude 负责

| 领域 | 范围 |
|---|---|
| 游戏机制 | 战斗、战车/部件改装、遭遇、成长、经济、商店货架与价格、掉落、悬赏 |
| 游戏逻辑 | 运行时系统、状态机、场景流程与切换、存档/读档与版本迁移 |
| 剧情与任务系统 | 对话系统、剧情触发条件、任务状态与分支判定、区域解锁（内容由 Codex 填写） |
| 关卡逻辑 | 可达性、遭遇表、触发区、逻辑场景中的逻辑物体摆放与参数 |
| 数据 | 数值 CSV、数据结构与导入器、文本表与本地化的格式和加载机制 |
| 数值平衡 | 伤害、价格、经验曲线等（机制落地后再做） |
| 输入与相机控制 | Input Actions、键位与手柄；相机跟随等控制代码 |
| 表现驱动接口 | 向 UI、动效、音频发出数据与事件（如“命中”“击毁”“金钱变化”），不负责其表现 |
| 逻辑工程配置 | Tags、Layers、物理与碰撞矩阵、Build Settings 场景列表 |
| 构建与仓库 | 打包发布、版本号、Git 分支策略、LFS、合并冲突处理 |
| 逻辑验收 | 逻辑测试与可复现的验证方式 |

### 1.3 共同 / 用户

- `docs/GDD.md`：世界观、剧情、文案、美术、界面章节归 Codex；机制、系统章节归 Claude。
- 新增 Unity 包：提出方说明理由，双方同意后由提出方安装。
- 整体试玩与设计决策：用户。
- 不在上表中的新工作，按划分原则判定；判定不了的交用户指定。

## 2. 边界规则

1. **路径所有权**以 `tools/ownership.py` 的规则表为唯一来源（`python tools/ownership.py who <路径>` 查询）。只改自己的路径；`shared` 路径改动前须在 `docs/INTERFACE.md` 达成一致。
2. **对接内容**（字段、ID、路径、事件、挂点、场景名）只写在 `docs/INTERFACE.md`，双方都标 ✅ 才生效；未生效条款任何一方不得按其实现对方侧内容。
3. **提交**：提交信息以 `[Claude]` / `[Codex]` 开头，且只提交自己的路径：`git commit -m "[Codex] ..." -- <路径>`。`commit-msg` 钩子会拒绝越界文件，不得绕过。
4. **Unity 独占**：同一时间只有一方使用 Unity（Editor 或 batchmode）。使用前 `python tools/msg.py lock <我> unity "用途"`，结束后 `unlock`；加锁失败即等待。
5. **需要对方配合**：用 §3 通讯发 `request`；需要长期跟踪的事项同时登记到 §4。不直接改对方文件，发现对方文件有问题只报告。
6. **Claude 的独立工作副本**：Claude 在 `D:\code\GAME-claude`（分支 `claude/dev`）开发，离线编译通过后用 `python tools/integrate.py` 快进合并到主目录；主目录中不会出现 Claude 的半成品代码。Codex 在主目录 `main` 上工作。
7. **分歧**：列出方案交用户决定，不覆盖对方成果。

## 3. 通讯

工具 `tools/msg.py`，消息存于本机 `comms/`（不入 Git）。身份：`Claude` / `Codex` / `User`。

- **每次开始任务、提交前、结束任务时**执行 `python tools/msg.py inbox <我>`，处理后 `ack`。
- 发送：`python tools/msg.py send <我> <对方> <类型> "内容" [--re 消息号]`。
  - `request`：请对方做事；`question`：请对方答复；二者必须用 `reply --re` 回复。
  - `handoff`：交付或移交（写明提交号与路径）；`info`：通知，无需回复。
- 消息只写要点；达成的对接约定仍须写入 `docs/INTERFACE.md` 并双方 ✅ 才生效。
- **进度**：开始任务、完成一个阶段、结束任务时执行 `python tools/msg.py status <我> "当前任务 | 进度 | 下一步"`；用户与对方用 `python tools/msg.py status` 查看。
- 资源锁：`lock` / `unlock` / `locks`（目前仅 `unity`）。

## 4. 待对接事项

| # | 提出方 | 事项 | 处理方 | 状态 |
|---|---|---|---|---|
| 1 | Claude | 审阅 `docs/INTERFACE.md` I-01～I-08 并标注 | Codex | 待做 |
| 2 | Claude | 接收迁移移交文件并以 `[Codex]` 提交：`UNITY/Data/Text/text_zh.csv`（原数据表中的名称/描述）、`UNITY/Assets/Scripts/Presentation/`（`Game.Presentation.asmdef`、原 `TankVisual`） | Codex | 待做 |
| 3 | Claude | 按 I-03～I-05 制作底盘与武器预制体 | Codex | 待做（依赖 #1） |
| 4 | Claude | 原型调试界面 `FieldHUD`、`BattleController.OnGUI` 中的文字暂为硬编码；正式 UI 由 Codex 在表现层按 I-09 实现后，Claude 删除调试界面 | Claude / Codex | 待 I-09 认可 |
| 5 | 用户 | 【越界登记】用户授权 Claude 临时在表现层新增 `Scripts/Presentation/Field/FieldTankVisualBootstrap.cs`：野外/迷宫把战车模型挂到玩家、有模型时隐藏灰盒胶囊。Codex 可接手、改写或删除 | Codex | 待接手 |
| 6 | Claude | 【P1 大地图与自由移动，当前优先】大地图灰盒美术场景 `Field_Art`（地形、道路、边界、城镇/泵站入口标识）；步行角色（驾驶员、机械师）与首辆战车正式或临时模型，按 I-03/I-04/I-05；行走/奔跑/待机、上下车 Animator（参数按 I-21、P-02） | Codex | 待做 |
| 7 | Claude | 【P2 交互、菜单与界面】对话框、交互提示（调查/对话/开门）、主菜单、暂停菜单、队伍/状态界面、存读档界面（I-13）的布局、样式、字体与表现层脚本；NPC 模型（首批：曲婆）；对话与 UI 文案（I-02、I-16） | Codex | 待做（P1 后） |
| 8 | Claude | 【P3 系统与机制】战斗界面（替换 `BattleController.OnGUI`，I-09/I-10/I-19/I-20）、背包/商店/车库界面（I-11/I-12）、升级提示（I-15）；首批敌人模型、武器模型与图标（I-03）；技能/道具名称与说明（I-17）；命中/击毁等特效与音效 | Codex | 待做（P2 后） |
| 9 | Claude | 【P4 城镇、大地图与故事】据点城镇与泵站迷宫美术场景（I-18）、正式大地图区域布景；剧情对话表 `dialogue.csv` 全量内容（I-16）；城镇、赏金首名称与说明（I-14） | Codex | 待做（P3 后） |
| 10 | Claude | 【P5 上线标准】全部临时/灰盒资源替换为正式模型、贴图、动画、UI、音频、BGM；LOD 与渲染性能达标；多语言文本；表现层全面验收 | Codex | 待做（P4 后） |
| 11 | Claude | 【第二幕内容，按 I-22】文本：6 个新敌人、5 个技能、4 件新装备、`TWN_Saltwell`/`SHP_Saltwell`、`QST_Act2_Ledger` 名称与各步说明、传送提示；对话 `DLG_Act2_Intro`、`DLG_Act2_Saltwell`、`DLG_Act2_Calibration`（按 STORY.md 第二幕）；新敌人与新武器模型、图标；`Field_SaltBelt_Art`、`Dungeon_GhostCity_Art` 美术场景（灰盒布局见 `PrototypeSceneBuilder`） | Codex | 待做（先认可 I-22） |
| 12 | Claude | 【第三幕内容，按 I-23】文本：7 个新敌人、3 个技能、4 件新装备、`TWN_Lampcamp`/`SHP_Lampcamp`、`QST_Act3_Floodgate` 名称与各步说明、传送提示；对话 `DLG_Act3_Intro`、`DLG_Act3_Lampcamp`、`DLG_Act3_Power`、`DLG_Act3_Choice`（三结局选项的效果须与 I-23 逐字一致）；新敌人与新装备模型、图标；`Field_SaltBasin_Art`、`Dungeon_ControlStation_Art` 美术场景 | Codex | 待做（先认可 I-23） |

## 5. 变更记录

- **v0.9**（2026-09-27，Claude）：§4 新增 #12 第三幕内容需求（对应 INTERFACE I-23）。
- **v0.8**（2026-09-27，Claude）：§4 新增 #11 第二幕内容需求（对应 INTERFACE I-22）。
- **v0.7**（2026-09-27，Claude）：按 GDD v0.3 五阶段里程碑，在 §4 登记 #6～#10 分阶段素材需求；Codex 按阶段顺序交付，P1 优先，前一阶段未完成前可只交灰盒/临时资源。

- **v0.6**（2026-09-27，Claude）：Claude 改用独立工作副本开发，经编译检查后集成（§2.6）。
- **v0.5**（2026-09-27，Claude）：新增 §3 通讯机制与 Unity 锁（`tools/msg.py`），取代手工登记 Unity 占用。
- **v0.4**（2026-09-27，Claude）：对接条款移至 `docs/INTERFACE.md`；路径所有权改由 `tools/ownership.py` 定义并以提交钩子强制；完成迁移：名称/描述移入文本表、美术引用改为按 ID 约定路径加载、场景拆为逻辑/美术、程序集拆为 Runtime/Editor/Presentation。
- **v0.3**（2026-09-27，Claude）：按用户划分原则重写职责分工。
- **v0.2**（2026-09-27，Claude）：Unity 工程迁移至 `UNITY/`。
- **v0.1**（2026-09-27，Claude）：初版。
