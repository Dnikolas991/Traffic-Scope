# Transit Scope 项目说明

## 项目目标

Transit Scope 是一个 Cities: Skylines II 模组。
它提供一套独立的选择工具，用来查看道路和建筑的交通构成，并在 3D 场景中显示相关高亮和路径。

当前统计口径：
- 道路：当前就在该道路上的流量 + 未来路径会经过该道路的流量
- 建筑：未来目的地为该建筑的输入流量

## 当前后端结构

核心系统位于 `code/`：

- `Mod.cs`
  - 模组入口
  - 向游戏注册各个系统

- `ScopeToolSystem.cs`
  - 自定义选择工具
  - 负责 Hover、Confirm、取消选择
  - 将命中对象统一解析成道路边或建筑

- `ScopeTrafficFlowSystem.cs`
  - 按当前选中对象做交通分析
  - 负责采集当前道路流量、未来路径流量、建筑输入流量
  - 生成选中对象对应的导航路径边集合

- `ScopeSystem.cs`
  - 监听当前选择
  - 调用分析服务
  - 把分析结果转换成 UI 统计数据

- `ScopeOverlaySystem.cs`
  - 绘制 Hover 高亮
  - 绘制当前选中对象对应的导航路径高亮

- `ScopeUISystem.cs`
  - 管理前后端绑定
  - 向前端推送统计 JSON

## 后端辅助类

- `ScopeEntityResolver.cs`
  - 负责把命中实体、Owner 链、Temp 实体还原成真正的道路边或建筑

- `ScopeTrafficClassifier.cs`
  - 统一交通分类口径
  - 避免当前流量和未来流量分类不一致

- `ScopeTrafficModels.cs`
  - `TrafficCounters`
  - `ScopeSelectionAnalysis`

- `ScopeSelectionStats.cs`
  - 前端统计面板使用的数据结构
  - 负责 JSON 序列化

- `ScopeOverlayHelpers.cs`
  - Overlay 曲线、线段、建筑轮廓绘制辅助

- `ScopeOverlayColors.cs`
  - Overlay 颜色和宽度参数

## 前端结构

位于 `UI/src/`：

- `index.tsx`
  - 注册 UI 入口

- `ScopeButton.tsx`
  - 顶部按钮
  - 控制开启/关闭选择模式

- `ScopeStatsPanel.tsx`
  - 统计面板
  - 渲染饼图和分类列表

- `ScopeIcon.tsx`
  - 按钮图标

- `bindings.ts`
  - 前后端绑定定义

## 当前命名约定

为了避免类名前缀过长，项目内部统一使用 `Scope*`：

- `ScopeToolSystem`
- `ScopeSystem`
- `ScopeTrafficFlowSystem`
- `ScopeOverlaySystem`
- `ScopeUISystem`

保留不变的内容：
- 模组名仍然是 `Transit Scope`
- 绑定命名空间仍然使用 `transitScope`
- `UI/mod.json` 中的 mod id 仍然是 `Transit Scope`

## 已知事实

- 项目可以通过 `dotnet build` 构建成功
- 构建时会同时执行 UI 的 webpack 打包
- 游戏工具链后处理默认已关闭，避免本机工具链不完整导致构建失败
- 如果需要完整后处理，可使用：

```powershell
dotnet build -p:RunGameToolchainTargets=true
```

## 下次继续时建议优先检查

1. 进游戏验证道路“未来路径流量”和原版路线显示是否完全一致
2. 验证建筑输入流量是否符合预期
3. 清理仍然存在的乱码中文注释
4. 如果要增强面板，可把“当前流量”和“未来流量”拆开显示
