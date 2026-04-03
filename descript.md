# Transit Scope 项目说明

## 项目目标

Transit Scope 是一个 Cities: Skylines II 模组，用来查看道路和建筑的交通构成，并在 3D 场景中显示对应的高亮和导航路线。

当前统计口径：
- 道路：当前就在该道路上的流量 + 当前仍然有效且尚未被 agent 消费完的未来路线流量
- 建筑：当前仍然有效且尚未被 agent 消费完、且目的地为该建筑的输入流量

## 当前后端结构

核心系统位于 `code/`：

- `Mod.cs`
  - 模组入口
  - 向游戏注册工具、UI、主控和 Overlay 系统

- `SelectionToolSystem.cs`
  - 自定义选择工具
  - 负责 Hover、Confirm、取消选择
  - 把命中的实体统一解析为道路边或建筑

- `TrafficFlowSystem.cs`
  - 每帧重建“剩余有效路线快照”
  - 基于 `PathOwner.m_ElementIndex + PathElement 剩余切片 + *CurrentLane 当前所在边` 计算 future traffic
  - 为选中道路/建筑输出统计和路线高亮数据

- `SelectionSystem.cs`
  - 先推进 `TrafficFlowSystem` 的路线快照
  - 监听当前选择
  - 将分析结果转换成 UI 使用的统计面板数据

- `OverlaySystem.cs`
  - 绘制 Hover 高亮
  - 绘制当前选中对象对应的未来路线高亮

- `UIBridgeSystem.cs`
  - 管理前后端绑定
  - 向前端推送统计 JSON

## 当前导航统计架构

### 为什么不能只看 Ready 或新结果

之前的问题在于把“路径已经求出来”误当成了“未来仍然会经过这里”。

这两者不是一回事：
- 一条路径可以已经生成，但前半段已经被 agent 走掉
- 一条路径可以处于重算中，但旧路径此刻仍在被 agent 使用
- 一条路径也可能刚被改道替换，缓冲区里短时间内还留有旧痕迹

所以当前项目不再统计“本帧新算了多少路线”，而是统计：

**已经分配给 agent，并且从 agent 当前消费位置往后看，仍然还有哪些未来路段没有走到。**

### 当前实现依据

`TrafficFlowSystem` 当前依赖这些原版组件：

- `PathOwner`
  - `m_ElementIndex`
  - 表示当前已经消费到路径缓冲区的哪个位置

- `PathInformation`
  - 保存 `m_Origin / m_Destination / m_Methods / m_State`

- `PathElement` buffer
  - 保存完整路径元素序列

- `HumanCurrentLane / CarCurrentLane / TrainCurrentLane / WatercraftCurrentLane / AircraftCurrentLane`
  - 表示 agent 当前实际所在 lane

### 当前实现逻辑

每帧对所有 `PathOwner + PathInformation + PathElement` 实体进行扫描：

1. 先过滤明显无效的路径状态
   - `Failed`
   - `Obsolete`
   - `DivertObsolete`
   - `CachedObsolete`

2. 取 `PathOwner.m_ElementIndex` 之后的剩余 `PathElement`

3. 把剩余元素解析成真实道路边实体
   - 连续重复边去重

4. 根据 `*CurrentLane` 推导 agent 当前所在边
   - 如果剩余路径的第一条边就是当前所在边，则从 future route 中剔除
   - 因为这条边属于“当前位置正在走的边”，不属于“未来尚未走到的边”

5. 对过渡态路径做两帧稳定确认
   - `Pending`
   - `Scheduled`
   - `Append`
   - `Updated`
   - `Divert`

只有连续两帧剩余路径签名一致，才纳入稳定 future traffic 统计。

### 当前快照模型

`TrafficModels.cs` 中定义了：

- `ActiveAssignedRoute`
  - `OwnerEntity`
  - `Destination`
  - `CurrentEdge`
  - `OwnerState`
  - `InfoState`
  - `Methods`
  - `ElementIndex`
  - `TotalElements`
  - `RemainingElements`
  - `RemainingRouteHash`
  - `StableFrameCount`
  - `IsStable`
  - `Traffic`
  - `RemainingRouteEdges`

- `RouteFrameStats`
  - `ObservedOwners`
  - `StableOwners`
  - `RemainingSegments`

## 当前辅助类

- `EntityResolver.cs`
  - 把命中的实体、Owner 链和临时实体还原成真正的道路边或建筑

- `TrafficClassifier.cs`
  - 统一流量分类口径
  - 保证当前流量和未来流量的分类字段一致

- `TrafficModels.cs`
  - `TrafficCounters`
  - `ActiveAssignedRoute`
  - `RouteFrameStats`
  - `SelectionAnalysis`

- `SelectionStats.cs`
  - 前端统计面板使用的数据结构
  - 负责 JSON 序列化

- `OverlayHelpers.cs`
  - Overlay 曲线、线段、建筑轮廓绘制辅助

- `OverlayColors.cs`
  - Overlay 颜色和宽度参数

## 前端结构

位于 `UI/src/`：

- `index.tsx`
  - 注册 UI 入口

- `SelectionButton.tsx`
  - 顶部按钮
  - 控制开启/关闭选择模式

- `StatsPanel.tsx`
  - 统计面板
  - 渲染饼图和分类列表

- `SelectionIcon.tsx`
  - 按钮图标

- `bindings.ts`
  - 前后端绑定定义

## 命名约定

项目内部不再使用 `Scope*` 前缀，类名按职责直接命名。

当前命名特点：
- 系统类按职责命名，例如 `SelectionToolSystem / TrafficFlowSystem / SelectionSystem / OverlaySystem / UIBridgeSystem`
- 辅助类按用途命名，例如 `EntityResolver / TrafficClassifier / OverlayHelpers / OverlayColors`
- 前端组件按职责命名，例如 `SelectionButton / StatsPanel / SelectionIcon`

保留不变的内容：
- 模组名仍然是 `Transit Scope`
- 绑定命名空间仍然使用 `transitScope`
- `UI/mod.json` 中的 mod id 仍然是 `Transit Scope`

## 构建说明

- 纯 C# 编译可使用：

```powershell
dotnet msbuild .\Transit Scope.csproj /t:Compile
```

- 如果要走完整构建：

```powershell
dotnet build .\Transit Scope.sln
```

- 游戏工具链后处理默认关闭，避免本机工具链不完整时构建失败
- 如需显式开启：

```powershell
dotnet build -p:RunGameToolchainTargets=true
```

## 当前风险和后续验证点

1. 需要进游戏验证各类 agent 的 `m_ElementIndex` 是否确实和视觉路线消费进度一致
2. 需要重点验证改道、目标变化、交通工具切换时，两帧稳定确认是否足够抑制旧新路线重叠
3. 如果还存在瞬时重复统计，下一步要继续往 `HumanNavigationSystem / CarNavigationSystem / TripResetSystem / PathOwnerTargetMovedSystem` 的运行时链路下钻
4. 部分旧文件仍有历史遗留的乱码中文注释，后续可以继续清理
