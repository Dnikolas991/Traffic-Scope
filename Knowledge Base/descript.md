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
    - 基于 ECS 组件做交通分类
    - 聚合为 9 类统计桶
    - 在无流量时仍输出完整 bucket 集合

- `RouteStatisticsModels.cs`
  - 统计快照、中间记录、前端 DTO。
  - 定义最终分类枚举与固定输出顺序。

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
- 车类分类优先基于 ECS 组件存在性，而不是字符串匹配
- 车类分类使用统一入口函数，按“更具体功能组件优先，普通 Car 兜底”的顺序判断
- 最终按 9 类可视化类型聚合：
  - `CargoFreight`
  - `PrivateCar`
  - `PublicTransport`
  - `PublicService`
  - `Watercraft`
  - `Aircraft`
  - `Train`
  - `Human`
  - `Bicycle`
- 每类保留原版等价的 `200 source` 上限

### 车辆分类规则

车类实体的分类优先级如下：

1. `PublicService`
   - 命中任一公共服务车辆组件时归类到公共服务，例如：
   - `Ambulance`
   - `FireEngine`
   - `GarbageTruck`
   - `Hearse`
   - `MaintenanceVehicle`
   - `ParkMaintenanceVehicle`
   - `PoliceCar`
   - `PostVan`
   - `PrisonerTransport`
   - `RoadMaintenanceVehicle`
   - `WorkVehicle`

2. `PublicTransport`
   - 命中 `PublicTransport`、`PassengerTransport`、`Taxi`

3. `CargoFreight`
   - 命中 `CargoTransport`、`DeliveryTruck`、`GoodsDeliveryVehicle`

4. `PrivateCar`
   - 命中 `PersonalCar`
   - 或命中 `Car` 且不属于以上三类

其余大类保持：

- `Watercraft`
- `Aircraft`
- `Train`
- `Human`
- `Bicycle`

### 分类候选实体

为了避免只检查单一实体导致误判，分类时会依次检查一组候选实体：

- 当前 `source entity`
- `Controller`
- `CurrentVehicle`
- `CurrentVehicle` 对应的 `Controller`

这样可以兼容路径源实体、控制器实体和当前载具实体之间的差异，同时保持分类逻辑集中、可维护、可扩展。

## UI 展示约定

- 前端只消费后端输出的 bucket，不自行推断统计类别
- 饼图、legend、tooltip 都以 `RouteVisualizationKind` 的固定顺序和本地化 key 为准
- `Car` 不再作为最终展示项出现，界面上只显示细分后的四类车流

### 无流量显示规则

当 `matchedSourceCount == 0` 时：

- 后端仍然输出完整的 9 个 bucket，所有 `sourceCount = 0`
- 前端仍然显示完整饼图区域
- 饼图使用安全的占位渲染，不做除零计算
- 中央文案显示 `no traffic`
- legend 仍保留全部分类项
- 不允许出现 `null` 面板、`NaN`、`Infinity` 或空 legend 崩溃

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
