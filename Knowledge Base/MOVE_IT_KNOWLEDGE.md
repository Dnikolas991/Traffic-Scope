<thinking>
先做全库扫描，确认这是一个 Cities: Skylines II Mod，采用 C# ECS 后端加 React/TypeScript UI 的双端结构。随后按“入口与构建配置 -> Tool/Manager/Action/Selection 主链路 -> Searcher/Snapper/Overlay 辅助链路 -> UI 绑定与发布说明”的顺序交叉验证，避免只看单点文件。归纳时优先提炼可迁移知识：状态机、抽象边界、数据模型、工程约定，以及直接影响稳定性的隐性约束。凡是代码中的注释、已知问题、版本不一致、反射 Hack、硬编码路径、缺少测试/CI 等，都单列为 [技术债]，不与推荐实践混淆。无法从仓库确认的运行环境、部署流水线、外部依赖仓库能力统一标记为“待确认”。
</thinking>

# 1. 项目概览与领域模型

## 核心业务目标与解决的痛点
- 项目A是 Cities: Skylines II 的对象编辑 Mod，核心目标是在游戏内提供“选择、移动、旋转、局部操控、批量工具”能力，覆盖建筑、植物、道具、贴花、地表、节点、路段曲线等对象。[推荐参考文件] [Mod.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Mod.cs), [LongDescription.md](/D:/cs2/CS2-MoveIt/Code/MoveIt/Properties/Stable/LongDescription.md)
- 真实痛点不是“渲染 UI”，而是“在 ECS 世界里以低侵入方式维持对象选择、历史回退、悬停反馈、局部控制点、吸附和地形更新的一致性”。

## 关键用户故事/核心场景
- 用户开启工具后，通过单选或框选获得一个 Selection，再对其拖拽平移、右键旋转、撤销/重做。
- 用户进入 Manipulation Mode，对可操控子对象进行细粒度编辑，目前主要是路段控制点。
- 用户通过 Toolbox 执行一次性批处理工具，例如对齐地形高度、对齐对象高度、按目标方向旋转。

## 核心领域概念（Domain Concepts）字典简述
- `MIT`: 整个工具运行时中枢，统一持有系统、管理器、状态和查询。[Definitions.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Tool/Definitions.cs)
- `Moveable`: 对“可被 Move It 操作的对象”的统一抽象，屏蔽建筑、节点、路段、控制点等差异。[Moveable.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Moveables/Moveable.cs)
- `MVDefinition`: 轻量对象标识，包含实体、类型、父子关系、是否操控态等，贯穿 Selection、Hover、Action、Manager。
- `SelectionNormal` / `SelectionManip`: 两套选择语义，前者面向对象级选择，后者面向可操控子对象选择。[SelectionNormal.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Selection/SelectionNormal.cs), [SelectionManip.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Selection/SelectionManip.cs)
- `Action` + `QueueManager`: 所有可撤销行为的统一执行与历史管理模型。[Action.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Actions/Action.cs), [QueueManager.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Managers/QueueManager.cs)
- `Searcher`: 搜索可命中对象；`Snapper`: 变换时做吸附候选评估；`Overlay`: 提供视觉反馈。[Searcher.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Searcher/Searcher.cs), [Snapper.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Snapper/Snapper.cs), [OverlaySystem.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Overlays/OverlaySystem.cs)

# 2. 技术栈与工程基建

## 核心语言/框架/依赖库版本限制
- 后端使用 `.NET Framework 4.7.2` + `C# 9`，运行于 CS2 Mod Toolchain，依赖 `Game.dll`、`Unity.Entities`、`Unity.Burst`、`Colossal.*` 等游戏程序集。[MoveIt.csproj](/D:/cs2/CS2-MoveIt/Code/MoveIt/MoveIt.csproj)
- 前端使用 `React 18.2` + `TypeScript 4.8` + `Webpack 5`，Node 要求 `>=18`。[package.json](/D:/cs2/CS2-MoveIt/UI/package.json), [tsconfig.json](/D:/cs2/CS2-MoveIt/UI/tsconfig.json)
- 工程还依赖外部共享代码 `QCommon2`，通过相对路径导入，不在本仓库内。[MoveIt.csproj](/D:/cs2/CS2-MoveIt/Code/MoveIt/MoveIt.csproj)

## 开发、测试、构建、部署的标准化流程
- C# 主工程通过 `CSII_TOOLPATH` 引入 `Mod.props/Mod.targets`，构建后会触发 `npm run build --prefix ..\..\UI`，再复制 UI 构建产物到部署目录，说明后端构建是主入口，UI 构建被嵌入 MSBuild 流程。[MoveIt.csproj](/D:/cs2/CS2-MoveIt/Code/MoveIt/MoveIt.csproj)
- UI 单独支持 `npm run build`、`npm run dev`、`npx create-csii-ui-mod update/clean`，输出路径由 `CSII_USERDATAPATH` 决定，直接写到游戏 Mod 目录。[webpack.config.js](/D:/cs2/CS2-MoveIt/UI/webpack.config.js)
- 仓库内未发现自动化测试、CI、静态检查脚本或部署流水线定义，此部分为“待确认”。

## 配置管理与环境变量规范
- 关键环境变量有 `CSII_TOOLPATH`、`CSII_USERDATAPATH`；缺失时 UI 构建直接失败，说明这是硬依赖而非可选配置。[webpack.config.js](/D:/cs2/CS2-MoveIt/UI/webpack.config.js), [MoveIt.csproj](/D:/cs2/CS2-MoveIt/Code/MoveIt/MoveIt.csproj)
- 运行配置存在双源：发布版元数据在 `PublishConfiguration.xml`，前端元数据在 `UI/mod.json`，后端程序集版本在 `csproj`。[PublishConfiguration.xml](/D:/cs2/CS2-MoveIt/Code/MoveIt/Properties/Stable/PublishConfiguration.xml), [mod.json](/D:/cs2/CS2-MoveIt/UI/mod.json), [MoveIt.csproj](/D:/cs2/CS2-MoveIt/Code/MoveIt/MoveIt.csproj)
- 国际化同样是双源：C# 嵌入 `Code/MoveIt/l10n/*.json`，UI 读取 `UI/src/lang/en-US.json`；Debug 模式甚至有把翻译导出到固定本地路径的代码。[Mod.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Mod.cs)

# 3. 架构设计与模块协作

## 系统整体架构模式
- 核心模式是“ECS Tool 内核 + Manager 协调层 + Action 历史层 + UI 绑定层”。不是纯 MVC，也不是纯事件总线，而是以 `MIT` 为中心的 orchestration 架构。[Definitions.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Tool/Definitions.cs)
- 行为驱动靠 `Action.Phase` 状态推进，`QueueManager` 每帧驱动当前 action 的相位流转。这比直接在输入事件里改实体更可回退、更可插拔。[Action.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Actions/Action.cs), [QueueManager.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Managers/QueueManager.cs)

## 核心模块划分及其职责边界
- `Tool/*`: 工具生命周期、状态机、输入后的高层编排。[Lifecycle.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Tool/Lifecycle.cs), [Tool.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Tool/Tool.cs), [OnUpdate.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Tool/OnUpdate.cs)
- `Managers/*`: 面向运行时资源的协调层，分别管理 Moveable、Hover、Queue、Toolbox、ControlPoint、Input。
- `Actions/*`: 所有可撤销业务动作，包含选择、模式切换、变换、工具箱动作。
- `Selection/*`: 选择语义与选择生命周期，封装“主选对象”和“连带子对象”的差异。
- `Searcher/*`、`Snapper/*`: 负责命中、候选筛选、吸附评估，避免把空间搜索逻辑散落到输入或动作代码里。
- `UI/*` 和 `UI/src/*`: 后端序列化 panel state，前端只消费状态并回传 trigger，保持 UI 轻逻辑。

## 模块间的通信机制与数据流向
- 输入从游戏/快捷键进入 `MIT_InputSystem` 或 UI trigger，最终都映射到 `MIT` 方法或 `Action.Phase` 变更。[InputSystem.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Systems/InputSystem.cs), [UISystem.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/UI/UISystem.cs)
- 每帧 `MIT.OnUpdate` 依次更新 UI 焦点、Selection 中心、Hover、InputManager、Toolbox，再调用 `Queue.FireAction()` 执行业务。[OnUpdate.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Tool/OnUpdate.cs)
- `Moveable` 同时承担领域对象适配器和 Overlay 反馈挂载点；Selection/Hover/Action 都通过 `MVDefinition -> Moveable` 访问具体对象。
- UI 通过 `ValueBinding` 拉取状态，通过 `TriggerBinding` 回推用户操作，没有直接共享业务逻辑。[UISystem.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/UI/UISystem.cs), [bindings.tsx](/D:/cs2/CS2-MoveIt/UI/src/bindings.tsx)

## 入口文件（Entry Points）与初始化流程
- 后端入口为 [Mod.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Mod.cs)：注册设置、加载本地化、向 `UpdateSystem` 挂接 Tool/Input/UI/Overlay 等 ECS System。
- 工具入口为 [Lifecycle.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Tool/Lifecycle.cs)：`OnCreate` 创建依赖系统和查询，`OnGamePreload` 初始化运行时 manager，`OnStartRunning` 启用输入/覆盖层/tooltip，`OnStopRunning` 统一回收。
- UI 入口为 [index.tsx](/D:/cs2/CS2-MoveIt/UI/src/index.tsx)：向游戏 UI 注入工具按钮、主面板、调试面板、按键冲突确认弹窗。

# 4. 核心业务逻辑与状态流转

## 最关键的 1-3 个业务流程解析

### 4.1 启用工具 -> 悬停/选择 -> 进入可操作态
- `RequestEnable` 接管当前 active tool，初始化输入状态。[Lifecycle.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Tool/Lifecycle.cs)
- 每帧 `HoverManager` 通过 `Searcher.SearchRay` + 过滤器 + vanilla raycast 结果找最合适的命中对象，再设置 HoverHolder 与 Overlay 标记。[HoverManager.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Managers/HoverManager.cs), [Searcher.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Searcher/Searcher.cs)
- `SelectAction`/`SelectMarqueeAction` 依据单选、增选、框选和操作模式更新 `Selection`，同时驱动 Moveable 的 `OnSelect/OnDeselect` 以同步 Overlay 和生命周期。[SelectAction.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Actions/Select/SelectAction.cs), [SelectMarqueeAction.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Actions/Select/SelectMarqueeAction.cs)

### 4.2 平移/旋转 -> Undo/Redo
- `MoveStart`/`RotationStart` 会根据当前 hover/selection 决定是直接变换还是先隐式创建一个选择动作再变换。[Tool.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Tool/Tool.cs)
- `TransformAction` 在每帧计算 `MoveDelta/AngleDelta`，支持精细模式和吸附；真正的数据更新在 `TransformBase.Transform()`，同时处理邻接网络、控制点和地形更新。[TransformAction.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Actions/Transform/TransformAction.cs), [TransformBase.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Actions/Transform/TransformBase.cs)
- Undo/Redo 不是简单反向 delta，而是切换 `m_Old/m_New` 两份状态快照，再走统一 Finalise/Cleanup，确保附带更新同样执行。

### 4.3 普通选择模式 <-> Manipulation Mode
- 模式切换被建模为独立 `ModeSwitchAction`，可以进入历史栈，并尝试从过往 mode switch 恢复该模式下的选择集。[ModeSwitchAction.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Actions/ModeSwitchAction.cs)
- 这是高迁移价值设计：模式切换不是 UI 布尔值，而是有历史语义、有状态修复逻辑的业务动作。

## 核心状态机（State Machine）或数据生命周期
- 工具状态机：`Default`、`ApplyButtonHeld`、`SecondaryButtonHeld`、`DrawingSelection`、`ToolActive`。[Definitions.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Tool/Definitions.cs)
- Action 状态机：`Initialise -> Do -> Finalise -> Cleanup -> Complete`，Undo/Redo 也是相位之一。[Action.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Actions/Action.cs)
- Selection 生命周期：`_Buffer(显式选中)` -> `UpdateFull()` 扩展为 `_BufferFull(含子对象)` -> `CalculateCenter()` 计算几何中心和半径。[SelectionBase.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Selection/SelectionBase.cs)
- Moveable 生命周期：`Factory/GetOrCreate` 创建 -> 被 Hover/Selection/Current Action 引用 -> `RemoveIfUnused` 回收。[MoveablesManager.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Managers/MoveablesManager.cs)

## 决定项目成败的核心约束与校验规则
- 选择数量硬限制 `MAX_SELECTION_SIZE = 10000`，避免无界操作。[SelectionBase.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Selection/SelectionBase.cs)
- 普通模式与操控模式的可选对象集合不同，`SelectionNormal` 和 `SelectionManip` 的 `GetObjectsToTransform*()` 明确分叉，不能混用。
- 过滤器关闭时不生效，打开后才按勾选项过滤；控制点始终保留在 mask 中。[FilterSectionStates.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/UI/FilterSectionStates.cs)
- 变换后必须补做邻居网络、建筑连接、地形更新，否则结果会“看起来移动了，系统状态没补齐”。[TransformBase.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Actions/Transform/TransformBase.cs)

# 5. 接口约定与数据规约

## 核心数据结构与实体模型
- `PanelState`, `TopRowButtonStates`, `FilterSectionState`, `ToolboxSectionState` 是 UI 后端序列化对象，前后端以字段名契约通信。[PanelState.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/UI/PanelState.cs), [panelState.tsx](/D:/cs2/CS2-MoveIt/UI/src/mit-mainpanel/panelState.tsx)
- `SelectionState` 保存 action 历史中的选择快照，是 Undo/Redo 的最小业务快照。[SelectionState.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Selection/SelectionState.cs)
- `State`/`TransformStateOld/New` 保存变换前后对象姿态，是变换动作的可回滚基础。此仓库未单独展开全部实现细节，但用途明确。

## API 设计规范
- 前后端接口不是 REST/GraphQL，而是游戏内 `ValueBinding` + `TriggerBinding`。[UISystem.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/UI/UISystem.cs)
- 命名规约统一以 `MIT_` 作为 binding 前缀，如 `MIT_ToolEnabled`、`MIT_PanelButtonPress`、`MIT_PanelCheckboxChange`。[bindings.tsx](/D:/cs2/CS2-MoveIt/UI/src/bindings.tsx)
- UI 只发“意图”而不是业务结果，例如只传 `section/id/value`，状态永远以后端为准，这是值得迁移的约束。

## 全局异常处理与日志埋点机制
- 全局采用 `QLog` / `QLoggerCO`，日志大量带上下文、bundle key 和 debug dump，适合复杂 ECS 调试。[Definitions.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Tool/Definitions.cs)
- 没有统一错误码体系，异常处理以 `try/catch + 日志 + 尽量降级继续` 为主。
- 调试能力较强：Debug Panel、Overlay Freeze、Debug lines、Save Logs To Desktop。对复杂交互工具，这类诊断入口比单元测试更早期有效，但不能替代测试。

# 6. 沉淀与复用资产 (重点)

## 推荐复用模块
- `Moveable` 抽象层：适合迁移到项目B作为“异构对象统一操作接口”。它把对象特性、父子关系、覆盖层更新和选择反馈统一了。[推荐参考文件] [Moveable.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Moveables/Moveable.cs), [MoveablesManager.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Managers/MoveablesManager.cs)
- `Action + QueueManager`：强烈建议复用其“相位驱动 + 环形历史队列 + 快照归档”的思路，用于任何需要撤销/重做的交互编辑器。[推荐参考文件] [Action.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Actions/Action.cs), [QueueManager.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Managers/QueueManager.cs)
- `SelectionBase` 双缓冲设计：显式选中集合和扩展后完整集合分离，非常适合处理“父对象被选中后子对象也要联动”的场景。[推荐参考文件] [SelectionBase.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Selection/SelectionBase.cs)
- `Searcher + FilterManager`：把空间搜索与筛选条件解耦，适合任何图形/地图/编辑器型项目。[推荐参考文件] [Searcher.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Searcher/Searcher.cs), [FilterManager.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Searcher/FilterManager.cs)
- `PanelState -> ValueBinding -> React` 的 UI 同步模式：后端持有真状态，前端只做显示和触发，这对复杂工具型面板非常稳。[推荐参考文件] [UISystem.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/UI/UISystem.cs), [mainpanel.tsx](/D:/cs2/CS2-MoveIt/UI/src/mit-mainpanel/mainpanel.tsx)

## 优秀设计模式
- 统一领域标识对象 `MVDefinition`，避免在各层直接传 Entity 和一堆附属元数据。
- 模式切换也进入 action history，而不是散落为多个布尔变量。
- 视觉反馈不直接绑定在输入代码，而是作为独立 Overlay System 根据当前领域状态重绘。
- 工具箱工具通过 `ToolBoxTool` 元数据注册，允许 UI 与动作类解耦扩展。[ToolboxManager.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Managers/ToolboxManager.cs)

## 隐性工程约定
- 命名前缀明显：`MIT_` 表示系统/绑定/组件域；`MV*` 表示 Moveable 实体；`FO*` 表示 foldout UI 状态。
- 目录按“运行时职责”分层，而不是按技术层粗分，这对大型工具型项目更清晰。
- 大量 `Debug*` / `Refresh()` / `UpdateFull()` / `GetOrCreate()` 命名说明团队默认接受“显式状态修复”而非完全依赖被动一致性。
- 本地化 key 由 C# 作为主命名来源，UI 通过 fallback JSON 兜底，说明“服务端命名、前端展示”的主导权在后端。

# 7. 技术债与避坑指南 (重点)

## 避免参考文件
- [Tool.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Tool/Tool.cs): [技术债] 通过反射写 `TerrainSystem.m_UpdateArea` 私有字段，说明缺少稳定官方扩展点。项目B应封装兼容层，禁止业务逻辑直接反射框架私有字段。
- [moveit-button.tsx](/D:/cs2/CS2-MoveIt/UI/src/mit-button/moveit-button.tsx), [buttonRow.tsx](/D:/cs2/CS2-MoveIt/UI/src/mit-mainpanel/buttonRow.tsx): [技术债] 通过“假变量引用图片”强迫 Webpack 打包资源，属于构建层 Hack，不应当作前端资源管理范式。
- [mainpanel.tsx](/D:/cs2/CS2-MoveIt/UI/src/mit-mainpanel/mainpanel.tsx): [技术债] 面板 X 位置通过 DOM 查询 `MoveItIcon` 和手工几何换算得到，耦合具体 UI 结构，迁移价值低。

## 已知缺陷与临时方案
- [技术债] Undo/Redo 相关稳定性问题被文档明确列为已知问题，`QueueManager` 中还有关于 Finalise 作用于错误 action 的注释，说明历史栈实现存在边界复杂性。[LongDescription.md](/D:/cs2/CS2-MoveIt/Code/MoveIt/Properties/Stable/LongDescription.md), [QueueManager.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Managers/QueueManager.cs)
- [技术债] `SelectionBase.Refresh()` 有“BUG this removed newly recreated CPs”注释，说明控制点重建与选择刷新存在时序脆弱点。[SelectionBase.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Selection/SelectionBase.cs)
- [技术债] `TransformBase` 明确有“Hack to work around the lack of unaltered original terrain height”注释，表明地形适配不是完整建模而是补丁式处理。[TransformBase.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Actions/Transform/TransformBase.cs)
- [技术债] `Mod.cs` 中 Debug 导出本地化文件写死了作者本机路径 `C:\Users\TJ\source\repos\...`，这类路径绝不能进入项目B主干。[Mod.cs](/D:/cs2/CS2-MoveIt/Code/MoveIt/Mod.cs)
- [技术债] 项目严重依赖外部 `QCommon2` 共享代码，但仓库未 vendor in，导致新项目复制时可移植性差。[MoveIt.csproj](/D:/cs2/CS2-MoveIt/Code/MoveIt/MoveIt.csproj)
- [技术债] 无测试、无 CI、无回归脚本。对交互型编辑器意味着功能增长后回归风险会指数上升。

## 冲突与差异
- [技术债] 版本信息不一致：`Code/MoveIt/MoveIt.csproj` 和发布配置是 `0.5.14`，但 `UI/mod.json` 仍是 `0.5.7`。这是典型双源配置漂移。[MoveIt.csproj](/D:/cs2/CS2-MoveIt/Code/MoveIt/MoveIt.csproj), [mod.json](/D:/cs2/CS2-MoveIt/UI/mod.json), [PublishConfiguration.xml](/D:/cs2/CS2-MoveIt/Code/MoveIt/Properties/Stable/PublishConfiguration.xml)
- [技术债] 发布配置声明依赖 `Unified Icon Library`，但 `UI/mod.json` 的 `dependencies` 为空；依赖真实生效点在发布侧还是运行侧，需要项目B避免这种多处定义。[PublishConfiguration.xml](/D:/cs2/CS2-MoveIt/Code/MoveIt/Properties/Stable/PublishConfiguration.xml), [mod.json](/D:/cs2/CS2-MoveIt/UI/mod.json)
- 文档称“目前只有 segments 可操控”，代码层确实主要围绕 `Segment/ControlPoint` 构建 manipulation 逻辑，二者基本一致；更广的可操控对象能力暂无证据，项目B不要臆测扩展能力。

# 8. 对项目 B 的架构建议

## 如果项目B从零开始，最值得优先保留的 3 条核心经验
- 把“用户操作”建模成有生命周期的 `Action`，并统一进入历史队列；不要在输入事件里直接改实体。
- 把“领域对象适配”抽成 `Moveable` 一类的统一接口，隔离不同对象类型的差异，避免选择/变换/覆盖层到处写类型分支。
- 保持“后端真状态 + 前端薄交互”模式，UI 只发意图，不持有最终业务状态。

## 项目B架构设计时必须避开的 2 个雷区
- 不要复制多源版本/配置/本地化体系。项目B必须把版本、依赖、i18n 源头收敛到单一真源，否则发布与运行态必然漂移。
- 不要让反射 Hack、硬编码路径、无测试的时序修复留在核心链路。项目A能跑不等于这种方式可扩展。

# 9. AI 辅助开发上下文 (AI Context)

项目B可参考项目A的核心模式：后端维护单一真状态，前端只做 ValueBinding/Trigger 风格的薄交互；所有可撤销行为统一抽象为 Action，并通过 QueueManager 驱动 `Initialise/Do/Finalise/Cleanup/Complete` 相位；选择系统必须区分“显式选择集合”和“展开后的完整作用集合”，避免父子对象联动失控；异构对象必须通过统一适配层处理，类似 `Moveable + MVDefinition`，禁止业务代码散落类型分支。目录建议按职责拆分：`Tool` 负责编排，`Managers` 负责运行时资源，`Actions` 负责业务行为，`Selection/Searcher/Snapper/Overlay` 负责可组合能力，`UI` 只放状态桥接和展示组件。命名沿用强前缀约束，如 `MIT_`/`MV`/`FO`，并保持状态对象与 UI 接口同名。红线：禁止双源版本号、禁止把配置分散在发布文件和运行文件、禁止写死本机路径、禁止核心流程依赖反射私有字段、禁止让前端持有业务真状态、禁止新增功能不接入撤销/重做模型、禁止无回归验证地改动选择/模式切换/控制点刷新链路。
