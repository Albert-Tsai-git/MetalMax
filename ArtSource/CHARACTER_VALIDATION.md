# 人物模型与移动动画验收记录

> 2026-09-27；Blender 4.5.10 LTS。Unity 当前由用户持锁，本轮仅记录已执行的 Blender 与 FBX 验证。

## 资产范围

- `CHR_Hunter`（阿砾）：低多边形猎人，防尘披肩、护目镜、橘色信号围巾与回收腰包。
- `CHR_Mechanic`（棠榆）：低多边形机械师，工装、工具包、扳手与工具腰带。
- 两人都包含 20 骨骼通用人形 Rig 和 `Idle`、`Walk`、`Run` 原地动作循环。
- 源坐标 1 单位=1 米，Y 向上，角色正面 +Z；Blender 预览包含正面行走/奔跑及棠榆背面装备检查。

## T01：Blender 源模型

- 操作：后台打开每个 `.blend`，检查 Rig、要求骨骼、蒙皮网格、动作名与循环曲线，并测量网格边界。
- 预期：Hunter/Mechanic 各有皮肤网格、可用双腿/双臂 Rig 和三种循环动作；身高处于 1.5–2.0 米。
- 实际：Hunter 25 个网格、Mechanic 27 个网格；各有 20 根骨骼；`Idle/Walk/Run` 齐全，Walk/Run 有循环曲线；边界约 `0.99 × 1.78 m`。
- 结论：通过 Blender 源结构检查；预览已检查两类步态可辨。

## T02：FBX 往返

- 操作：导出 `UNITY/Assets/Models/CHR_Hunter.fbx` 与 `CHR_Mechanic.fbx`，再由 Blender 导入并检查骨骼、网格、动作和轴向尺寸。
- 预期：FBX 保留所有骨骼、网格和三段动作；单位高度合理，前后轴有明显深度。
- 实际：两 FBX 均保留 20 骨骼、25/27 网格与 Idle/Walk/Run；高度 1.78 米，深度 0.48/0.60 米。动作在 FBX 中带 Rig 前缀，Unity 构建脚本会按末段映射回稳定 clip 名。
- 结论：通过 Blender FBX 往返；`Idle/Walk/Run` 的每条导入曲线首尾值差小于 `1e-3`。Unity 导入仍待实测。

## T03：Unity 导入、Animator 与步行试玩

- 操作计划：释放锁后运行 `Game.Presentation.CharacterVisualBuilder.Build`，确认两个资源预制体可 `Resources.Load`、三个动画循环和 `Speed/Moving/Running/OnFoot` 参数；启动 Field，实际按 WASD、Shift、F 验证移动/奔跑/上下车显示。
- 预期：阿砾随玩家移动并在步行时播放 Walk/Run；停止时 Idle；上车隐藏人物，返回步行时恢复；角色底部落地，无灰盒重叠。
- 实际：未执行。Unity 锁由 User 持有。
- 结论：`BLOCKED-无法真实验证`，等待 Unity 锁释放。
