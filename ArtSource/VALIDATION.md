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
