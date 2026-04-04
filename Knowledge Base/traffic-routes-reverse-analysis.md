# Cities: Skylines II 原版“查看/切换交通路线”逆向分析

本文档整理了对原版 `Cities: Skylines II` 中“查看交通路线 / 切换交通路线”功能的逆向结果，目标是为后续 AI 或模组开发者提供一份足够详细的上下文，使其能够复现原版相同或高度接近的交通路线读取与显示逻辑。

重点不是公共交通线路编辑工具，而是选中对象后，在右侧 `Selected Info` 动作区里切换显示“交通路线”的那套原版实现。

## 结论摘要

- 这个功能的 UI 入口不在 `ToolUISystem`，而在 `Game.UI.InGame.ActionsSection`。
- 真正负责收集和生成“交通路线显示数据”的系统是 `Game.Tools.TrafficRoutesSystem`。
- 该系统不是把路线明细通过 UI binding 传给前端绘制，而是在 ECS 世界里创建/更新 `LivePath` 路线实体，然后由 `Game.Rendering.RouteRenderSystem` 直接在世界内渲染。
- 前端只拿到少量状态数据：
  - 是否显示交通路线
  - 交通路线颜色表
  - 当前选中对象是否支持交通路线按钮
- 路线数据来源是路径缓存/导航数据，核心依赖 `Game.Pathfind.PathElement`，不是传统意义上的“车流统计总线”、“lane flow 排名”或“segment 流量榜单”。
- 原版逻辑会对路径源去重，并按交通方式聚合成最多 6 组显示路线：
  - car
  - watercraft
  - aircraft
  - train
  - human
  - bicycle
- 每组最多收集 `200` 个 path source，超过上限不再继续增加。

---

## 1. 功能入口

### 1.1 UI 入口系统

已确认 UI 入口位于：

- `Game.UI.InGame.ActionsSection`

这个 section 属于选中对象信息面板的一部分，组名是：

- `ActionsSection`

该系统在 `OnCreate()` 中创建了以下与交通路线相关的绑定：

- Trigger:
  - `toggleTrafficRoutes`
- Value:
  - `trafficRoutesVisible`
  - `trafficRouteColors`

同时它还维护一个布尔属性：

- `hasTrafficRoutes`

该属性通过 `OnWriteProperties()` 写到前端，决定当前选中对象是否显示“交通路线”按钮。

### 1.2 UI 到系统的调用链

前端点击：

- `ActionsSection.toggleTrafficRoutes`

对应后端：

- `Game.UI.InGame.ActionsSection::OnToggleTrafficRoutes()`

该方法的行为非常直接：

1. 读取 `m_TrafficRoutesSystem.routesVisible`
2. 取反
3. 写回 `m_TrafficRoutesSystem.routesVisible`
4. 调用 `SelectedInfoUISystem.SetDirty()`

也就是说，按钮本身只是一个布尔开关，不负责传输路线明细。

### 1.3 不是哪个系统

容易混淆但不是本功能核心的系统：

- `Game.Tools.RouteToolSystem`

这个是公共交通线路编辑工具，用于创建/修改 transport route waypoint，不是“查看交通路线”的显示逻辑。

---

## 2. 当前选中对象如何获取

### 2.1 核心来源

负责提供当前选中对象的是：

- `Game.Tools.ToolSystem`

关键属性：

- `selected`
- `selectedIndex`

在 `TrafficRoutesSystem.OnUpdate()` 中，如果 `routesVisible == true`，会取：

- `ToolSystem.selected`

作为当前分析目标实体。

同时 `ToolSystem.selectedIndex` 也会被传入 `FillTargetMapJob`，用于处理 `Aggregate` 这类复合网络对象时的“当前具体选中哪一条 edge”。

### 2.2 哪些选中对象允许显示交通路线按钮

`ActionsSection.OnProcess()` 会判断当前 `selectedEntity` 是否属于支持交通路线的类型。已确认支持以下实体类别：

- `Game.Buildings.Building`
- `Game.Net.Aggregate`
- `Game.Net.Node`
- `Game.Net.Edge`
- `Game.Routes.TransportStop`
- `Game.Objects.OutsideConnection`
- `Game.Creatures.Human`
- `Game.Vehicles.Vehicle`
- `Game.Citizens.Citizen`
- `Game.Citizens.Household`

如果命中这些类型之一，则：

- `hasTrafficRoutes = true`

否则按钮不显示。

这说明原版“交通路线查看”不仅针对道路网络，也支持人、车、居民、家庭、站点、外部连接、建筑等对象。

---

## 3. 前端到底拿到了什么

## 3.1 已确认传给前端的字段

目前已确认 `ActionsSection` 传给前端的与本功能有关的字段只有：

- `hasTrafficRoutes: bool`
- `trafficRoutesVisible: bool`
- `trafficRouteColors: Color32[]`

其中 `trafficRouteColors` 是长度固定为 6 的颜色数组。

### 3.2 没有发现传给前端的字段

没有发现通过 `ValueBinding` / `TriggerBinding` / `RawValueBinding` 发给前端的路线明细数据，例如：

- 路线列表
- 起点 / 终点
- 控制点
- 折线点数组
- 贝塞尔曲线控制点
- 车流权重
- 数量
- path id
- lane id
- direction
- vehicle type per path

### 3.3 结论

“交通路线”的几何并不是前端画的。

真正路线显示流程是：

1. UI 开关改变 `TrafficRoutesSystem.routesVisible`
2. `TrafficRoutesSystem` 在 ECS 里创建/更新 `LivePath` 路线实体
3. `RouteRenderSystem` 直接把这些路线渲染到世界里

也就是说：

- 前端只是控制器和状态面板
- 世界中的线条由渲染系统直接绘制

对于模组开发，这一点非常重要：

如果你想复现原版效果，正确方向不是去找“前端接收了哪些路线点”，而是去跟 ECS 路线实体和渲染缓冲。

---

## 4. 路线颜色来源

`ActionsSection.OnProcess()` 会懒加载一次 `trafficRouteColors`。

颜色来源：

- `Game.Prefabs.RouteConfigurationData`

它包含 6 个“路径可视化 prefab”引用：

- `m_CarPathVisualization`
- `m_WatercraftPathVisualization`
- `m_AircraftPathVisualization`
- `m_TrainPathVisualization`
- `m_HumanPathVisualization`
- `m_BicyclePathVisualization`

然后通过 `PrefabSystem.GetPrefab<LivePathPrefab>()` 取出 prefab，再读：

- `Game.Prefabs.RoutePrefab::get_color()`

最终组成长度为 6 的 `Color32[]`。

因此前端显示的交通路线颜色表，本质上是“6 类 live path visualization prefab 的颜色表”。

顺序已确认是：

1. car
2. watercraft
3. aircraft
4. train
5. human
6. bicycle

---

## 5. 负责路线收集的核心系统

核心系统：

- `Game.Tools.TrafficRoutesSystem`

核心字段：

- `routesVisible`
- `m_LivePathQuery`
- `m_PathSourceQuery`
- `m_RouteConfigQuery`
- `m_UpdateFrameIndex`

核心内部 job：

- `FillTargetMapJob`
- `FindPathSourcesJob`
- `UpdateLivePathsJob`

从结构上看，这套系统的任务是：

1. 把当前选中对象扩展成一个“目标实体集合”
2. 在全局 path source 候选中找出与目标集合有关的路径源
3. 按交通方式聚合成 live path route
4. 维护这些 live path route 的 segment 列表

---

## 6. 查询与总体执行流程

## 6.1 查询

### `m_LivePathQuery`

要求：

- `Game.Routes.LivePath`
- `Game.Routes.Route`
- 排除 `Game.Common.Deleted`

用途：

- 读取当前已有的 live path route 实体，用于增量更新和回收

### `m_PathSourceQuery`

构造条件：

- `All: Game.Simulation.UpdateFrame`
- `Any: Game.Pathfind.PathOwner` 或 `Game.Vehicles.TrainCurrentLane`
- `None: Game.Common.Deleted`, `Game.Tools.Temp`

用途：

- 枚举潜在 path source 候选实体

### `m_RouteConfigQuery`

要求：

- `Game.Prefabs.RouteConfigurationData`

用途：

- 读取 6 类可视化 prefab 配置

## 6.2 `OnUpdate()` 执行轮廓

`TrafficRoutesSystem.OnUpdate()` 的整体流程可以概括为：

1. 如果 `routesVisible == false`：
   - 选中实体视为 `Entity.Null`
2. 如果当前没有选中目标且也没有旧的 live path：
   - 直接返回
3. 判断当前选中对象是否属于可分析类型
   - `Building / Aggregate / Node / Edge / TransportStop / OutsideConnection`
   - 如果不是这些类型，仍会继续做一部分清理逻辑，但不会构建目标图
4. 如果属于可分析类型：
   - 建立 `m_TargetMap`
   - 递归展开相关子对象
   - 在 `m_PathSourceQuery` 中按 `UpdateFrame` 分片扫描 path source
5. 无论是否命中 target map，最后都会执行 `UpdateLivePathsJob`
   - 把 path source 转成 live path route / route segment
   - 清理旧 segment
   - 删除空 route

### `m_UpdateFrameIndex` 轮询

已确认：

- `m_UpdateFrameIndex` 每次 `+1`
- 达到 `16` 后回到 `0`

同时 `m_PathSourceQuery` 会加 `UpdateFrame(index)` 的 shared component filter。

这意味着：

- 原版不是每帧全量扫描所有 path source
- 它把源按 `UpdateFrame` 分帧轮询，降低开销

这会影响复现策略：

- 如果模组要求完全实时，就不能简单照抄这个分帧筛选
- 如果要尽量贴近原版，应保留类似分帧轮询

---

## 7. 从选中对象构造目标集合：`FillTargetMapJob`

这个 job 的输出是：

- `NativeHashSet<Entity> m_TargetMap`

它不是简单只放一个 `selectedEntity`，而是把与当前选中对象有关的一整批网络/对象实体都加入集合。

## 7.1 基础行为

首先无条件加入：

- `selectedEntity`

然后递归调用：

- `AddSubLanes(selected)`
- `AddSubNets(selected)`
- `AddSubAreas(selected)`
- `AddSubObjects(selected)`

## 7.2 额外扩展来源

### `SpawnLocationElement`

如果选中对象有：

- `DynamicBuffer<SpawnLocationElement>`

则把所有：

- `SpawnLocationElement.m_SpawnLocation`

加入目标集合。

### `Attached.Parent`

如果选中对象有：

- `Game.Objects.Attached`

则对其 `m_Parent` 再做一轮：

- `AddSubLanes(parent)`
- `AddSubNets(parent)`
- `AddSubAreas(parent)`
- `AddSubObjects(parent)`

### `Renter`

如果选中对象有：

- `DynamicBuffer<Game.Buildings.Renter>`

则把所有：

- `Renter.m_Renter`

加入目标集合。

### `AggregateElement`

如果选中对象有：

- `DynamicBuffer<Game.Net.AggregateElement>`

逻辑分两种：

1. `selectedIndex` 有效且在范围内
   - 只处理 `AggregateElement[selectedIndex].m_Edge`
2. 否则
   - 处理 aggregate 的全部 edge

处理方式是对 edge 调用：

- `AddSubLanes(edge)`

这说明 aggregate 模式下，原版会尽量利用当前“子元素索引”细化到具体 edge。

### `ConnectedRoute`

如果选中对象有：

- `DynamicBuffer<Game.Routes.ConnectedRoute>`

则把每个：

- `ConnectedRoute.m_Waypoint`

加入目标集合。

### `OutsideConnection + Owner`

如果选中对象本身是：

- `Game.Objects.OutsideConnection`

并且还能取到：

- `Game.Common.Owner`

则对：

- `Owner.m_Owner`

执行：

- `AddSubLanes(owner)`

## 7.3 子结构递归逻辑

### `AddSubLanes(entity)`

读取：

- `DynamicBuffer<Game.Net.SubLane>`

仅当：

- `SubLane.m_PathMethods != 0`

时，才把：

- `SubLane.m_SubLane`

加入 `m_TargetMap`。

这是非常关键的过滤：

- 原版并不是把所有 sublane 都当成目标
- 只把具备 pathfind 能力的 sublane 纳入考虑

### `AddSubNets(entity)`

读取：

- `DynamicBuffer<Game.Net.SubNet>`

对每个 `SubNet.m_SubNet` 调用：

- `AddSubLanes(subNet)`

### `AddSubAreas(entity)`

读取：

- `DynamicBuffer<Game.Areas.SubArea>`

对每个 `SubArea.m_Area`：

- `AddSubLanes(area)`
- `AddSubAreas(area)`

说明 area 也会递归深入。

### `AddSubObjects(entity)`

读取：

- `DynamicBuffer<Game.Objects.SubObject>`

对每个 `SubObject.m_SubObject`：

- `AddSubLanes(subObject)`
- `AddSubNets(subObject)`
- `AddSubAreas(subObject)`
- `AddSubObjects(subObject)`

这是最深的一层递归入口之一。

## 7.4 对模组复现的意义

如果模组只拿“当前选中 edge 或 building”去匹配路径，通常会比原版少很多命中。

原版实际上构造的是一个“扩展目标图”，包含：

- 本体
- 子 lane
- 子 net
- 子 area
- 子 object
- spawn locations
- renters
- connected route waypoints
- attached parent 的子结构
- outside connection 的 owner 子 lane

因此想做“与原版一致”的复现，必须先复刻 `TargetMap` 逻辑，而不是直接查单个实体。

---

## 8. 如何找出相关路径源：`FindPathSourcesJob`

这个 job 遍历 `m_PathSourceQuery` 中的候选实体，并把符合条件的 source entity 推进：

- `NativeQueue<Entity> m_PathSourceQueue`

它匹配路径源的方式不是单一条件，而是并行检查多种“当前 path source 是否与目标集合有关”的证据。

## 8.1 命中条件一：`PathOwner + PathElement`

如果实体有：

- `Game.Pathfind.PathOwner`
- `DynamicBuffer<Game.Pathfind.PathElement>`

则从：

- `PathOwner.m_ElementIndex`

开始遍历 path element。

遍历过程中：

- 如果 `PathElementFlags & 0x4 != 0`，跳过该元素
- 否则检查：
  - `PathElement.m_Target` 是否在 `m_TargetMap`

只要命中，就认为该实体相关。

这说明原版核心判定之一是：

- 某个路径缓存中的 target 链，是否指向当前选中对象扩展出来的目标集合

## 8.2 命中条件二：`Common.Target`

如果实体有：

- `Game.Common.Target`

则检查：

- `Target.m_Target` 是否在 `m_TargetMap`

命中则加入 path source 队列。

## 8.3 命中条件三：当前 lane 命中

分别检查：

- `HumanCurrentLane.m_Lane`
- `CarCurrentLane.m_Lane`
- `WatercraftCurrentLane.m_Lane`
- `AircraftCurrentLane.m_Lane`
- `TrainCurrentLane.m_Front.m_Lane`
- `TrainCurrentLane.m_Rear.m_Lane`

是否在 `m_TargetMap`。

## 8.4 命中条件四：navigation lane 命中

分别遍历：

- `CarNavigationLane`
- `WatercraftNavigationLane`
- `AircraftNavigationLane`
- `TrainNavigationLane`

检查每个元素的：

- `m_Lane`

是否在 `m_TargetMap`。

## 8.5 公交特殊过滤

job 内有一段逻辑与：

- `CurrentVehicle`
- `TransformFrame`
- `PublicTransport`

有关。

从 IL 行为看，高概率含义是：

- 某些带 `CurrentVehicle` 的 source 只有在其 vehicle 属于 `PublicTransport` 时，才按一种特定路径入队

这是针对乘客/搭乘状态的一层过滤，避免普通 vehicle occupant 被过度匹配。

## 8.6 入队对象是谁

命中后入队的 entity 有两种：

1. 如果当前实体有 `Controller` 且 `Controller.m_Controller != Null`
   - 入队 controller 实体
2. 否则
   - 入队当前实体本身

这意味着原版更偏向把“控制器实体”作为统一 path source，而不是每个子 vehicle / segment 自己单独作为 source。

---

## 9. 如何生成和维护显示路线：`UpdateLivePathsJob`

该 job 是整个系统里最关键的“聚合与去重”逻辑所在。

它的目标不是构建“全部候选路线清单”，而是把相关 path source 聚合为少数几条可视化 route。

## 9.1 先扫描已有 LivePath

job 开始时会遍历所有已有 `LivePath` route：

- 读取 route entity
- 读取 route 的 `PrefabRef`
- 读取 route 的 `DynamicBuffer<RouteSegment>`

然后建立两个 map：

### `livePathEntities : prefab -> LivePathEntityData`

字段：

- `m_Entity`
- `m_SegmentCount`
- `m_HasNewSegments`

说明：

- 每个“可视化 prefab 类型”只对应一个 live path route 实体

### `pathSourceFound : sourceEntity -> bool`

初始会把当前已有 route 的每个 segment 的 `PathSource.m_Entity` 记为：

- `false`

含义：

- 这个 source 曾经存在于旧 route 中，但本轮还没被重新确认

## 9.2 再从当前选中对象周边追加 path source

在消费 `m_PathSourceQueue` 之前，job 还会额外从当前选中实体周边扩展 path source：

### 当前 transport

如果选中实体有：

- `CurrentTransport`

则先把 `selectedEntity` 替换为：

- `CurrentTransport.m_CurrentTransport`

### controller 折叠

如果该实体有：

- `Vehicles.Controller`

且 `m_Controller != Null`，会继续替换为 controller。

### 直接追加

之后会尝试对以下对象调用 `AddLivePath()`：

- 当前实体本身
- `CurrentVehicle.m_Vehicle`
- `Controller.m_Controller`
- 车辆编组中的乘客
- 车辆本身乘客
- household 全体成员

这说明原版不仅依赖全局扫描出来的 source，也会从当前选中对象及其直接关联人/车/住户主动补路径。

## 9.3 消费 `m_PathSourceQueue`

如果 queue 已创建，则逐个 `Dequeue()`，对每个 source 调用：

- `AddLivePath(source, livePathEntities, pathSourceFound)`

## 9.4 `AddLivePath()` 的分类逻辑

### 先看 source 是否有 `PathElement` buffer

没有则直接返回。

### 再看这个 source 是否已经处理过

如果 `pathSourceFound.TryGetValue(source, out found)`：

- 若 found == true
  - 说明本轮已经处理过，直接返回
- 若 found == false
  - 说明它来自旧 route，但本轮第一次重新确认
  - 先把它改成 true，再返回

这里有一个需要特别注意的行为：

- 对于已经存在于旧 route 且本轮重新确认的 source，第一次命中时不会再追加新 segment
- 它只会把状态改成 true，后续依靠已有 segment 保留

这就是原版去重和增量维护的重要一环。

### 如果 source 以前没出现过，则按交通方式分类

分类顺序已确认：

1. `Human` -> `m_HumanPathVisualization`
2. `Watercraft` -> `m_WatercraftPathVisualization`
3. `Aircraft` -> `m_AircraftPathVisualization`
4. `Train` -> `m_TrainPathVisualization`
5. `Bicycle` -> `m_BicyclePathVisualization`
6. 默认 -> `m_CarPathVisualization`

注意：

- 默认分支就是 car，不需要显式 `Car` 组件

## 9.5 按 prefab 聚合

分类后会取对应的：

- `RouteConfigurationData.[Type]PathVisualization`

再从：

- `ComponentLookup<RouteData>`

读取该 prefab 的 `RouteData`。

然后以 prefab entity 为 key 查询：

- `livePathEntities`

### 如果不存在

创建一个新的 route 实体：

- `CreateEntity(RouteData.m_RouteArchetype)`
- 设置 `PrefabRef(prefab)`
- 设置 `Game.Routes.Color(RouteData.m_Color)`

并在 `livePathEntities[prefab]` 中登记：

- `m_Entity = newRoute`
- `m_SegmentCount = 1`
- `m_HasNewSegments = true`

### 如果已存在

说明该交通方式的 live path route 已存在。

此时：

- `m_SegmentCount++`
- 如果新 count 已达到 `m_SourceCountLimit`
  - 直接 return，不再新增 segment
- 否则：
  - `m_HasNewSegments = true`

### 聚合含义

这已经明确说明：

- 原版不是“一条 source 一条 route”
- 而是“同一交通方式的所有 source 聚合到一个 live path route 下”
- route 下通过多个 `RouteSegment` 表示各个 source

因此显示出来的“交通路线”其实是：

- 每种交通方式 1 条 route 实体
- route 下挂很多 segment entity

## 9.6 创建 segment

每个新 source 会创建一个 segment entity：

- `CreateEntity(RouteData.m_SegmentArchetype)`

并写入：

- `PrefabRef(prefab)`
- `Owner(routeEntity)`
- `PathSource.m_Entity = sourceEntity`

最后：

- `AppendToBuffer<RouteSegment>(routeEntity, new RouteSegment(segmentEntity))`

这说明 route 本体并不直接存几何，而是通过 segment entity 间接引用 path source。

---

## 10. 去重、保留、删除、裁剪

这部分是复现原版行为时最容易偏差的地方。

## 10.1 去重一：pathSourceFound

`pathSourceFound` 的 key 是：

- source entity

作用：

- 防止同一 source 在同一轮被重复加入多个 segment
- 区分“旧 route 已存在但本轮又命中”和“全新 source”

## 10.2 去重二：按 prefab 聚合

`livePathEntities` 的 key 是：

- path visualization prefab entity

所以所有同类 source 都会聚合到同一个 live path route。

这意味着没有发生这些分组：

- 按 lane 分组
- 按 edge 分组
- 按 segment 分组
- 按 direction 分组
- 按 OD 起终点分组
- 按 path cache hash 分组

已确认发生的分组只有：

- 按交通方式对应的 visualization prefab 分组

## 10.3 上限裁剪

`UpdateLivePathsJob` 写死：

- `m_SourceCountLimit = 200`

当某个 prefab 对应的 route 已经收集到 200 个 source 后：

- 后续新的 source 不再追加 segment

因此：

- 原版不是显示全部相关路径源
- 而是每类最多保留 200 个 source 对应的 segment

这是一个明确的截断上限。

## 10.4 旧 segment 清理

在 job 末尾，会再次遍历已有的 live path route。

对每个 route 的 `RouteSegment`：

1. 读取 segment 对应的 `PathSource`
2. 查 `pathSourceFound[source]`
3. 如果值是 false：
   - 进一步检查该 source 的 `UpdateFrame`
   - 如果与当前 `m_UpdateFrameIndex` 相同，则把该 segment 标记 `Deleted`
4. 如果值是 true：
   - 把该 segment 压缩保留到前部

最后：

- 对 route 的 segment buffer 做 `RemoveRange()`
- 如果删到 0 个，且本轮 `m_HasNewSegments == false`
  - 把 route 实体标记 `Deleted`
- 如果本轮 `m_HasNewSegments == true`
  - 把 route 标记 `Updated`

### 含义

原版并不是每帧完全重建所有 live path。

它更像是：

- 维护一组增量 route
- 对旧 source 做重新确认
- 不再命中的 segment 按分帧节奏逐步回收

这也是为什么 `UpdateFrame` 会参与清理判定。

---

## 11. 这套系统到底按什么维度统计

用户关心的几个可能维度，这里统一回答。

## 11.1 不是按 lane 统计

虽然匹配阶段会检查：

- current lane
- navigation lane
- targetMap 中的 sublane

但最终输出不是“lane -> count”。

lane 在这里只是命中相关 path source 的判据之一。

## 11.2 不是按 edge / segment 统计

`AggregateElement.edge`、`SubLane`、`SubNet` 都参与目标构建和匹配，但最终 route 不会按 edge/segment 聚合成单独组。

## 11.3 不是按 connection / path / direction 明细分组

没有发现：

- direction 维度分组
- connection pair 分组
- origin-destination 分组
- path hash / route hash 分组

## 11.4 也不是传统 vehicle type 枚举统计

严格说它是按“可视化 prefab 类型”聚合，和 vehicle type 相近，但不是任意 vehicle subtype。

固定只有 6 类：

- car
- watercraft
- aircraft
- train
- human
- bicycle

## 11.5 路径数据来源是 path cache / AI 路径数据

最接近真实来源的描述是：

- `PathOwner + PathElement` 路径缓存
- 当前车道/导航车道
- 当前 transport / 当前 vehicle / household / passenger 等直接关系

因此如果要概括：

- 原版显示的是“与当前选中对象有关的实时/近期导航路径源的可视化聚合”

而不是：

- 所有历史路径
- 纯交通流量热力线
- 单纯 lane 占用率

---

## 12. 原版显示的到底是“全部候选路线”还是“筛选后的路线”

答案是：

- 不是全部候选路线
- 是与当前选中对象相关、经过筛选、去重、按 6 类聚合、且带 200 上限裁剪后的 live path 集合

更具体地说，原版保留的是：

1. 与 `TargetMap` 有关联的 path source
2. 当前选中对象及其直接关联 transport/vehicle/passenger/household source
3. 每个 source 去重一次
4. 每种交通方式合并到一个 route
5. 每种交通方式最多保留 200 个 source segment

被排除的包括：

- 不在 `TargetMap` 关联链中的 source
- `SubLane.m_PathMethods == 0` 的 lane
- 某些 `PathElementFlags & 0x4` 的 path element
- 旧 route 中已失效且本轮未重新命中的 source
- 超过 200 上限的额外 source

---

## 13. 路线显示不是前端画，而是世界渲染

负责世界渲染的系统：

- `Game.Rendering.RouteRenderSystem`

### 13.1 它画什么

它渲染带以下特征的 route 实体：

- `Game.Routes.Route`
- 且 `Any` 命中：
  - `TransportLine`
  - `WorkRoute`
  - `LivePath`
  - `VerifiedPath`
- 排除：
  - `HiddenRoute`
  - `Deleted`
  - `Game.Tools.Hidden`

### 13.2 它如何决定要不要画

有两种来源：

1. 全局 query 方式
   - 根据当前 active tool 的 `requireRoutes`
   - 或 infomode 的 `InfoviewRouteData`
2. 选中对象关联 route 集合方式
   - 通过 `ShouldRenderRoutes(HashSet<Entity>&)` 收集当前选中对象关联 route

### 13.3 它如何找到当前选中对象相关的 route

`RouteRenderSystem.ShouldRenderRoutes(HashSet<Entity>&)` 的逻辑：

1. 先尝试把 `selected` 本身当 route 加入
2. 如果选中 building 且 attached 到 parent，则切换到 parent
3. 如果实体有 `CurrentRoute`，加入 `CurrentRoute.m_Route`
4. 如果有 `SubObject`，递归读 `ConnectedRoute`
5. 如果有 `SubRoute`，把其 `m_Route` 加入

所以渲染层也会额外处理当前选中对象直接拥有或连接的 route。

### 13.4 真正几何来源

`RouteRenderSystem` 自己不计算曲线，而是依赖：

- `Game.Rendering.RouteBufferSystem`

它通过：

- `RouteBufferIndex`
- `RouteBufferSystem.GetBuffer(...)`

拿到：

- material
- compute buffer
- bounds
- size

然后在 `RenderRoute()` 中：

- 设置 `colossal_RouteSegmentBuffer`
- 设置 `colossal_RouteColor`
- 设置 `colossal_RouteSize`
- `DrawMeshInstancedIndirect`

这说明：

- 路径几何最终是进了 GPU route buffer
- 世界中的路线是渲染系统直接画出来的

---

## 14. 对模组开发最实用的复现建议

如果目标是“写一个模组读取出和原版相同的交通路线”，建议不要从 UI 入手，而是按下面层次复现。

## 14.1 最小可用复现

### 第一步：获取当前选中对象

从：

- `ToolSystem.selected`
- `ToolSystem.selectedIndex`

取得当前目标。

### 第二步：构造 `TargetMap`

尽量复刻 `FillTargetMapJob`：

- selected 自身
- sublanes，且仅保留 `PathMethods != 0`
- subnets
- subareas
- subobjects
- spawn locations
- renters
- connected route waypoints
- attached parent 的展开
- aggregate edge + selectedIndex 特化
- outside connection owner

### 第三步：扫描 path source

尽量按原版条件匹配：

- `PathOwner + PathElement.m_Target`
- `Common.Target.m_Target`
- current lane
- navigation lane
- controller/public transport 逻辑

### 第四步：按 6 类聚合

通过 source 身上的组件判断：

- Human
- Watercraft
- Aircraft
- Train
- Bicycle
- default car

然后映射到：

- `RouteConfigurationData.[Type]PathVisualization`

### 第五步：每类限制 200

原版上限：

- `200`

### 第六步：如果想和原版渲染一致

继续追：

- `RouteBufferSystem`
- `RouteBufferIndex`
- `RouteSegment`
- `PathSource`

如果只是要“读出相同路线列表”，则不一定需要完整复现渲染缓冲。

## 14.2 如果你只想在模组里导出“同原版命中的路径源”

建议导出结构：

- selected entity
- target map entity set
- matched path source list
- source category
- source current lane / navigation lane
- source path element targets
- source controller
- whether truncated by 200 limit

这样即使暂时不复刻世界渲染，也能先验证匹配逻辑是否和原版一致。

## 14.3 如果你要显示和原版同样的颜色

不要硬编码颜色，直接从：

- `RouteConfigurationData`

读取：

- `m_CarPathVisualization`
- `m_WatercraftPathVisualization`
- `m_AircraftPathVisualization`
- `m_TrainPathVisualization`
- `m_HumanPathVisualization`
- `m_BicyclePathVisualization`

再通过对应 prefab 的：

- `RoutePrefab.color`

得到颜色。

---

## 15. 已确认 / 高概率推测 / 待验证

## 15.1 已确认

- UI 入口在 `Game.UI.InGame.ActionsSection`
- 按钮绑定名是 `toggleTrafficRoutes`
- UI 只拿到：
  - `trafficRoutesVisible`
  - `trafficRouteColors`
  - `hasTrafficRoutes`
- 当前分析对象来自 `ToolSystem.selected / selectedIndex`
- 主逻辑系统是 `Game.Tools.TrafficRoutesSystem`
- 路线来源是 path cache / AI 路径相关数据
- `FillTargetMapJob` 会递归展开子对象与 lane
- `AddSubLanes()` 只保留 `PathMethods != 0` 的 sublane
- `FindPathSourcesJob` 会检查：
  - `PathOwner + PathElement`
  - `Common.Target`
  - current lane
  - navigation lane
- `UpdateLivePathsJob` 会按 6 类交通方式聚合
- 每类 source 上限 `200`
- 会对 source 去重
- 会复用旧 live path route 并增量更新
- 世界中的线由 `RouteRenderSystem` 渲染，不是前端画

## 15.2 高概率推测

- `PathElementFlags & 0x4` 是一种不应参与当前目标匹配的 path element 标记
- `RouteBufferSystem` 负责把 `RouteSegment -> PathSource -> PathElement` 转成 GPU 路线几何
- 世界内看到的交通路线更接近“当前导航路径源可视化”，而不是流量统计图

## 15.3 待验证

- `RouteBufferSystem` 内是否进一步做了：
  - 曲线拟合
  - 采样
  - 简化
  - 裁剪
  - 段合并
- `PathElementFlags` 每个 bit 的精确定义
- `PathSource`、`RouteSegment` 的完整字段含义
- `LivePathPrefab` / `RouteData` 对渲染宽度、段长、颜色的全部影响

---

## 16. 后续最值得补充反编译的类

如果要把这份文档继续补到“足以 1:1 复刻原版世界渲染路线几何”，优先级如下：

1. `Game.Rendering.RouteBufferSystem`
2. `Game.Routes.PathElement`
3. `Game.Pathfind.PathElementFlags`
4. `Game.Routes.PathSource`
5. `Game.Routes.RouteSegment`
6. `Game.Prefabs.RouteData`
7. `Game.Prefabs.RouteConfigurationData`
8. `Game.Rendering.RouteBufferIndex`
9. `Game.Rendering.RouteRenderSystem` 其余辅助方法

其中最关键的是：

- `RouteBufferSystem`

因为当前已经基本搞清楚：

- 入口
- 选中对象来源
- 路径命中逻辑
- 去重
- 聚合
- 上限
- 颜色来源

剩下最大的黑盒就是：

- route segment 最终如何转换为可画的几何缓冲

---

## 17. 适合直接搜索的关键词

- `ActionsSection`
- `toggleTrafficRoutes`
- `trafficRoutesVisible`
- `trafficRouteColors`
- `hasTrafficRoutes`
- `TrafficRoutesSystem`
- `FillTargetMapJob`
- `FindPathSourcesJob`
- `UpdateLivePathsJob`
- `PathSource`
- `LivePath`
- `RouteSegment`
- `RouteConfigurationData`
- `RouteRenderSystem`
- `RouteBufferSystem`
- `RouteBufferIndex`
- `PathElement`
- `PathElementFlags`

---

## 18. 面向另一个 AI 的一句话说明

如果另一个 AI 要基于这份文档写模组，请把原版“交通路线”理解为：

- “以当前选中对象为中心，递归扩展出一组目标网络/对象实体，再从全局 path source 中找出与这些目标相关的路径源，将这些 source 按 6 种交通方式聚合成 live path route，并由渲染系统直接在世界中绘制；前端只负责按钮、可见状态和颜色表。”

