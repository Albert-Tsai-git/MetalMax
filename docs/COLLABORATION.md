# 协作约定（Claude × Codex）

> 版本：v0.2（草案）　|　更新日期：2026-09-27　|　适用项目：`D:\code\GAME`（Unity 工程位于 `UNITY/`，Unity 6 + URP）
> 本文件是两个 AI 共同遵守的唯一协作约定。任何一方修改“接口约定”一节时，须在文末变更记录中写明，并由用户转达另一方。

---

## 1. 职责分工

| 领域 | 负责方 | 范围 |
|---|---|---|
| 逻辑与机制 | **Claude** | 战斗、战车/部件、遭遇、任务、经济、存档、队伍成长等玩法规则与运行时脚本；数据结构与 CSV 导入；逻辑测试 |
| 模型、美术、环境 | **Codex** | Blender 建模与导出、材质/贴图/Shader、概念图、Unity 工程与包配置、URP/渲染设置、场景搭建与布景、光照、资产导入设置 |
| 设计文档 | 共同维护 | `docs/GDD.md`：机制章节以 Claude 为主，美术/环境章节以 Codex 为主；设计决策以用户确认为准 |

不在上表中的新工作，由用户指定负责方后再开始。

## 2. 目录所有权

### 2.1 仓库目录结构（2026-09-27 确定）

```text
GAME/                    仓库根目录（非 Unity 工程）
├─ UNITY/                Unity 工程根目录：用 Unity Hub 打开此目录
│  ├─ Assets/
│  ├─ Packages/
│  ├─ ProjectSettings/
│  ├─ UserSettings/      本机设置，不入版本管理
│  ├─ Library/ Logs/     Unity 生成缓存，不入版本管理
│  └─ Data/              CSV 数值表（导入器按工程根相对路径 `Data` 读取）
└─ docs/                 GDD、协作约定等文档
```

规则：

- Unity 工程内的一切文件都放在 `UNITY/` 下；仓库根只放 `UNITY/`、`docs/` 以及今后的版本管理配置（如 `.gitignore`、`.gitattributes`）。
- 本文其余路径均相对于 `UNITY/`（`docs/` 除外）。
- `Data/` 放在工程内而非仓库根：导入器以 Unity 工作目录（工程根）解析相对路径 `Data`，这样工程自包含、无需改代码；若要移出须同步修改 `CsvDataImporter.DataDir`。
- `*.csproj`、`*.slnx` 由 Unity 自动生成，不手工编辑、不入版本管理。
- 新增顶层目录须先在本节登记。建议美术源文件（`.blend` 等）放在仓库根 `ArtSource/`，只把导出的 FBX 放入 `UNITY/Assets/`，避免 Unity 直接导入 `.blend`。

### 2.2 各路径所有者

| 路径 | 所有者 | 另一方可做的事 |
|---|---|---|
| `Assets/Scripts/`（除下述 View 类） | Claude | 只读；发现问题提给用户 |
| `Assets/Scripts/Tank/View/`、UI 表现层脚本 | Claude 写逻辑接入，Codex 可提需求 | 需要改动时先说明需求 |
| `Assets/Scripts/Editor/CsvDataImporter.cs` | Claude | 只读 |
| `Assets/Scripts/Editor/PrototypeSceneBuilder.cs` | Claude（灰盒原型用） | 正式场景由 Codex 手工/工具搭建，不依赖此脚本 |
| `Data/*.csv` | Claude | Codex 只可提出新增 ID 需求 |
| `Assets/GameData/`（CSV 生成的 ScriptableObject） | Claude（数值字段）/ Codex（`modelPrefab` 等美术引用字段） | 见 §3.3 |
| `Assets/Art/`、`Assets/Models/`、`Assets/Materials/`、`Assets/Prefabs/`（美术预制体） | Codex | 只读 |
| `Assets/Scenes/`、`Assets/Settings/`、`Assets/InputSystem_Actions.inputactions` | Codex | 需要新增输入动作或场景组件时提需求 |
| `ProjectSettings/`、`Packages/` | Codex | 需要新增包时提需求，不自行安装 |
| `docs/` | 共同 | 编辑自己负责的章节 |

**规则**：不修改对方所有的文件。确需跨界时，在“待对接事项”（§5）登记，由所有者处理。

## 3. 接口约定

### 3.1 数据 ID 与命名前缀

| 前缀 | 含义 | 示例 |
|---|---|---|
| `TNK_` | 底盘、引擎、C 装置等车体部件 | `TNK_Chassis_Light` |
| `WPN_` | 武器 | `WPN_Cannon_75` |
| `ENM_` | 敌人（悬赏目标用 `ENM_Bounty_`） | `ENM_Bounty_IronCrab` |
| `CHR_` | 角色 | — |
| `ENV_` | 环境资产 | — |

- 数据 ID 由 Claude 在 CSV 中定义，是 ScriptableObject 文件名，也是存档引用键，**定义后不改名**。
- 美术资产文件名沿用对应 ID，例如 `TNK_Chassis_Light` 的模型预制体命名为 `TNK_Chassis_Light.prefab`；纯美术资产（无对应数据）可加后缀如 `_Concept_v01`。

### 3.2 战车模型与挂点

- 底盘预制体内必须包含**名称精确一致**的空节点作为武器挂点（运行时按名称深度查找，`TankVisual.FindDeep`）：
  - `Mount_Main`：主炮
  - `Mount_Sub`：副炮
  - `Mount_SE`：SE 特殊武器（仅有该槽位的底盘需要）
- 每个底盘有哪些挂点，以 `Data/parts.csv` 的 `holes` / `mounts` 两列为准；新增挂点类型由 Claude 先改数据，再由 Codex 加节点。
- 武器预制体的枢轴放在安装点，本地坐标原点对齐挂点，朝向 +Z 为炮口方向。
- 单位 1 = 1 米，Y 轴向上；导出前应用变换，预制体根节点 Transform 为单位变换。
- 引擎、C 装置外观暂无挂点；需要时另行约定（GDD 5.3）。

### 3.3 ScriptableObject 中的美术引用

- `TankPartData.modelPrefab`、`EnemyData.modelPrefab` 由 **Codex 在 Unity 中赋值**。
- CSV 重新导入会复用已有资产并只更新数值字段，`modelPrefab` 保留；**仅当某 ID 的部件类型改变时资产会被删除重建，引用会丢失**——Claude 改类型前须登记到 §5。
- Codex 不修改这些资产上的数值字段；Claude 不修改 `modelPrefab` 字段。

### 3.4 场景与流程

- 场景名是代码常量：野外 `Field`、战斗 `Battle`（`GameSession`）。改名或新增据点/迷宫场景时，Codex 在 Build Settings 登记，Claude 同步代码常量。
- 当前场景位于 `Assets/Scenes/Prototype/`。正式场景目录与原型场景的替换时机另行约定。
- 场景中逻辑组件（如 `EncounterZone`、`RandomEncounter`、`FieldPlayerController`、`BattleController`）由 Codex 摆放，其参数与所需引用由 Claude 在“接入说明”中列出。

### 3.5 其他

- 命名空间：`Game.Core`、`Game.Battle`、`Game.Tank`、`Game.Field`、`Game.EditorTools`；新增模块沿用 `Game.*`。
- 代码注释使用中文；重要数据流日志使用 `[模块]` 前缀（如 `[Session]`、`[Import]`）。
- 输入统一走 Unity Input System；新增输入动作由 Claude 提需求，Codex 在 `.inputactions` 中添加。

## 4. 交付与交接

每次交付需附一段交接说明（写在对话中，或需要长期保留时写入 `docs/`）：

- **Claude → Codex**：新增/改动的脚本与组件、需要在场景或预制体上挂接的组件与参数、新增的数据 ID、需要的挂点或美术资源清单。
- **Codex → Claude**：新增的预制体与路径、挂点与层级结构、场景对象与标签/Layer、已赋值的 `modelPrefab`、渲染或包配置变化。

验收：

- 逻辑改动：Claude 提供可复现的验证方式（逻辑测试或 `BattleTestRunner` 等运行结果）。
- 美术/环境改动：Codex 以 Unity 中的实际导入结果为准，不以 Blender 视窗为准（GDD 7）。
- 双方都不在对方未确认前宣称“联调通过”。

## 5. 待对接事项

| # | 提出方 | 事项 | 处理方 | 状态 |
|---|---|---|---|---|
| 1 | Claude | GDD 第 10 节“工作区缺少 ProjectSettings/Packages/Scenes”已过时（现已存在），需更新原型状态 | Claude（机制部分）/ Codex（工程部分） | 已更新（2026-09-27，静态检查）；Codex 可补充工程侧细节 |
| 2 | Claude | 确认 `Assets/Scenes/Prototype/` 下场景是否已加入 Build Settings | Codex | 已确认：两场景均已登记 |
| 4 | Claude | Unity 工程已迁移至 `UNITY/`：Unity Hub 需移除旧路径 `GAME` 并添加 `GAME/UNITY`；首次打开请核对场景、URP 设置与 `modelPrefab` 引用无丢失 | Codex / 用户 | 待确认 |
| 3 | Claude | 轻型/重型底盘预制体需含挂点：轻型 `Mount_Main`、`Mount_Sub`；重型另加 `Mount_SE` | Codex | 待做 |

## 6. 冲突处理

- 设计分歧：列出方案交用户决定，不自行覆盖对方成果。
- 文件被对方改动：先通知用户，不直接回滚。
- 本约定未覆盖的情况，以用户的最新指示为准，并补入本文件。

## 7. 变更记录

- **v0.2**（2026-09-27，Claude）：新增 §2.1 仓库目录结构；Unity 工程迁移至 `UNITY/`，`Data/` 随工程迁移。
- **v0.1**（2026-09-27，Claude 起草）：初版分工、目录所有权、接口约定与交接流程，待 Codex 与用户确认。
