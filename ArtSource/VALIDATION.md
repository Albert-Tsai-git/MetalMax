# 美术与启动验收记录

> 2026-09-27；Unity 6000.6.3f1、Blender 4.5.10。此记录只描述已实际执行的测试。

## T01：Blender 源文件

- 操作：在 Blender 后台逐一重新打开五个 `.blend`，运行 `validate_assets.py`。
- 预期：根节点单位变换；轻型挂点 Main/Sub，重型另有 SE；武器沿 +Z 延伸。
- 实际：五项均 PASS；轻型 23 个网格、重型 23 个网格，挂点名称准确。
- 结论：通过源文件结构检查。

## T02：Unity FBX 导入与预制体

- 操作：执行 `Game.Presentation.VisualPrefabBuilder.Build`，导入五个 FBX 并生成同 ID 预制体；读取 `Resources/Visuals/{ID}`。
- 预期：1 Unity 单位为 1 米，底盘长轴为 +Z，挂点朝 +Z，五个资源可加载。
- 实际：轻型边界尺寸 `2.66 × 1.50 × 4.55 m`，重型 `3.37 × 1.56 × 5.45 m`；Main/Sub/SE 位置与朝向通过检查；五个 `Resources.Load` 成功。
- 边界过程：第一次导出缩小至约 1%，轴向错误；调整 FBX 轴转换和 `ModelImporter.globalScale` 后重新导入通过。生成器已加入尺寸门槛，错误导入会抛异常。
- 结论：通过导入和资源加载检查。

## T03：Unity URP 预览

- 操作：执行 `Game.Presentation.VisualPreviewCapture.Run`，在 Unity 中实例化底盘、主炮、副炮、重型 SE 后渲染。
- 预期：装备分立、方向可读、材质可显示。
- 实际：`ArtSource/UnityPreviews/` 两张预览中可辨认轮廓与各武器；没有粉色材质。
- 结论：通过首轮表现检查；正式场景镜头尚未验收。

## T04：Windows 玩家启动

- 操作：执行 `Game.Presentation.PlayerLaunchCheck.Build`，启动 `ArtSource/TestBuild/MetalMax.exe`，观察约 10 秒运行日志后停止进程。
- 预期：构建成功，进入 `Field` 场景，初始化会话，无异常。
- 实际：`BuildPipeline` 为 Succeeded、0 错误、2 警告；玩家进入 `Field` 灰盒并初始化演示队伍，无运行时异常。
- 限制：玩家窗口以隐藏方式启动，未模拟键鼠操作或判断屏幕可交互性；F5/F9 与战斗循环仍待实景复验。
- 结论：启动路径通过；交互路径未验收。

## 待复验

- 开发版运行日志仅加载 18 个中文文本键，尚未包含最新 `text_zh.csv` 的商店与操作提示。已发消息请 Claude 重跑数据导入。
- 完整场景试玩、F5/F9 存读档、战斗、商店/车库与美术场景融合等待下一轮运行测试。


## T05：新武器与挂装预览

- 操作：Blender 4.5.10 后台生成 `WPN_Flamethrower`、`WPN_ShockCannon`，运行源层级校验并导出 FBX；Unity `VisualPrefabBuilder.Build` 导入并生成同 ID 预制体；Unity 预览场景将两种新武器挂到重型车并渲染。
- 预期：两个源文件根节点单位变换、炮口沿 +Z；预制体可由 Resources 加载，米制尺寸合理；预览中武器与车体可辨。
- 实际：两模型各 8 个网格，源结构检查通过；Unity 边界分别为 `0.92 × 0.60 × 1.56 m` 与 `1.04 × 0.49 × 1.91 m`，预制体保存/加载成功；重型战车挂装截图中可辨认电击炮与火焰喷射器。
- 结论：通过模型、导入及渲染检查。

## T06：泵站美术场景

- 操作：Unity 打开 `Assets/Scenes/Art/Dungeon_PumpStation_Art.unity`，遍历全场景 Collider 并渲染预览。
- 预期：场景可打开，包含走廊、管线、泵体与维护灯；不含 Collider。
- 实际：检查无 Collider 后生成 960×720 预览；墙、管线、泵体、地面标识及琥珀色维护灯可见。
- 结论：通过美术场景结构与预览检查；逻辑场景叠加运行待 Claude RebuildAll 后复验。

## T07：新增内容后的 Windows 启动

- 操作：Unity `PlayerLaunchCheck.Build` 构建开发版，运行 `ArtSource/TestBuild/MetalMax.exe` 10 秒后关闭。
- 预期：构建成功，玩家进程保持运行。
- 实际：构建 `Succeeded`，0 错误、2 警告；玩家进程运行超过 10 秒。
- 限制：本轮实际构建早于 Claude 对新 `text_zh.csv`、`dialogue.csv` 的 RebuildAll 重导入；没有键鼠驱动工具，未验证移动/交互、存读档、战斗或真实屏幕 UI。
- 结论：启动路径通过；完整垂直切片交互 `BLOCKED-无法真实验证`，需待 Claude 重建并提供可交互窗口测试。
