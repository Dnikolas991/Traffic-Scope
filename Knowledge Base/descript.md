# Transit Scope 项目说明

## 项目目标

Transit Scope 是一个 `Cities: Skylines II` 模组。
它围绕“选择对象 -> 后端分析 -> 前端展示”这条链路工作，用于查看道路、轨道、建筑及其关联路径源的统计结果，并在必要时提供轻量级场景高亮。

当前项目重点是尽量贴近原版 `TrafficRoutesSystem` 的路径源匹配与分组逻辑，再把结果转换成模组自己的统计面板，而不是维护早期那套“当前流量 + future traffic”的独立口径。

## 目录结构

后端代码统一放在 `code/` 下，并按职责拆成五个并列目录：

- `code/Core`
  - 入口与基础运行能力。
  - 当前包含：
    - `Mod.cs`
    - `Logger.cs`

- `code/Selection`
  - 选择模式与选择驱动。
  - 当前包含：
    - `SelectionToolSystem.cs`
    - `SelectionSystem.cs`

- `code/Analysis`
  - 统计分析与模型定义。
  - 当前包含：
    - `TrafficFlowSystem.cs`
    - `RouteStatisticsPipeline.cs`
    - `RouteStatisticsModels.cs`

- `code/Presentation`
  - 世界表现与前后端桥接。
  - 当前包含：
    - `OverlaySystem.cs`
    - `UIBridgeSystem.cs`

- `code/Shared`
  - 共享工具与渲染辅助。
  - 当前包含：
    - `EntityResolver.cs`
    - `OverlayHelpers.cs`
    - `OverlayColors.cs`

前端代码位于 `UI/src/`：

- `index.tsx`
- `bindings.ts`
- `SelectionButton.tsx`
- `SelectionIcon.tsx`
- `StatsPanel.tsx`
- `routeStatsContracts.ts`

## 职责划分

### Core

- `Mod.cs`
  - 模组入口。
  - 注册系统和更新阶段。

- `Logger.cs`
  - 统一日志输出。

### Selection

- `SelectionToolSystem.cs`
  - 管理选择模式。
  - 维护 hover、确认选择、取消选择。
  - 同时维护两类实体：
    - `display entity`
    - `source entity`

- `SelectionSystem.cs`
  - 监听当前选择。
  - 驱动统计刷新。
  - 将快照推送到表现层。

### Analysis

- `TrafficFlowSystem.cs`
  - 统计门面。
  - 持有当前快照。
  - 为表现层暴露边权重。

- `RouteStatisticsPipeline.cs`
  - 统计核心实现。
  - 负责：
    - 构建目标集
    - 匹配路径源
    - 追补直接关联 source
    - 聚合为 6 类统计桶

- `RouteStatisticsModels.cs`
  - 统计快照、中间记录、前端 DTO。

### Presentation

- `OverlaySystem.cs`
  - 负责场景中的 hover 高亮。
  - 选中态尽量交给原版表现。

- `UIBridgeSystem.cs`
  - 负责前后端绑定。
  - 推送面板 JSON。
  - 清理无效面板状态。

### Shared

- `EntityResolver.cs`
  - 把命中实体、子实体、Owner 链和临时实体还原成稳定业务实体。

- `OverlayHelpers.cs`
  - 提供 Overlay 绘制辅助。

- `OverlayColors.cs`
  - 管理 Overlay 颜色和尺寸常量。

## 设计约束

1. 后端是真实状态来源。
   - 目标集、路径源匹配、聚合结果都必须在后端完成。
   - 前端只能消费结果，不能自行修正统计口径。

2. 目录按职责并列，不搞特权目录。
   - 统计分析只是 `Analysis` 的一部分。
   - 选择、展示、共享能力与统计分析是同级模块。

3. 统计模型与统计流程分离。
   - 模型留在 `RouteStatisticsModels.cs`
   - 流程留在 `RouteStatisticsPipeline.cs`

4. Overlay 只是表现层。
   - 不允许让 Overlay 反向决定统计结果。
   - 选中视觉优先复用原版。

5. 新功能必须先判断归属层级。
   - 入口注册放 `Core`
   - 选择逻辑放 `Selection`
   - 统计分析放 `Analysis`
   - 前端桥接与世界表现放 `Presentation`
   - 通用工具放 `Shared`

## 当前统计口径

当前统计实现基于原版 `TrafficRoutesSystem` 的逆向理解与项目对齐：

- 从选中实体构建 `TargetSet`
- 使用 `PathOwner / Target / CurrentLane / NavigationLane` 扫描 path sources
- 对当前交通工具、乘客、家庭成员等直接关联 source 做追补
- 按 6 类可视化类型聚合：
  - `Car`
  - `Watercraft`
  - `Aircraft`
  - `Train`
  - `Human`
  - `Bicycle`
- 每类保留原版等价的 `200 source` 上限

## 构建与部署

常用命令：

```powershell
dotnet msbuild .\Transit Scope.csproj /t:Compile
```

```powershell
dotnet build .\Transit Scope.csproj
```

```powershell
cd .\UI
npm run build
```

说明：

- `dotnet build` 会走完整后处理与部署链路。
- UI webpack 会直接把前端产物写入本地 Mods 目录。
- 当前项目使用官方 `Mod.targets` 的默认部署行为，不要再覆盖同名部署目标。

## 维护建议

1. 先确定改动属于哪一层。
2. 先修正后端口径，再调整前端展示。
3. 复用共享工具，不要平行复制同类能力。
4. 不要再为单个功能单独开“特权目录”。
