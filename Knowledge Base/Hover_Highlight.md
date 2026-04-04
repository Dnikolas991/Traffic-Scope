Cities: Skylines II 原版悬停高亮（Hover Highlight）实现机制深度解析

这份文档总结了对 《都市：天际线 2》（CS2）原版代码的逆向分析结果，旨在解释游戏如何通过 ECS（Entity Component System）架构实现鼠标悬停对象时的蓝色高亮效果。

  ---

一、 核心架构概念：双轨制高亮

CS2 的高亮机制并非简单的“改变材质颜色”，而是通过 Outline（描边）渲染层 实现的。根据场景不同，存在两条平行的逻辑路径：

1. 动态悬停轨（主要路径）：通过创建一个“影子实体”（Temp Entity）来覆盖原实体。这种方式支持所有工具预览，是鼠标晃动时看到高亮的来源。
2. 持久选中轨（辅助路径）：直接在原实体（Original Entity）上添加 Highlighted 标签组件。通常用于点击选中建筑后的持续高亮。

  ---

二、 动态悬停高亮链路（逻辑全流程）

这是最通用的实现方式，适用于道路、建筑、子元素等。

1. 射线检测阶段 (Raycast)
* 输入：Game.Tools.ToolRaycastSystem 每帧根据鼠标屏幕坐标发射射线。
* 结果：碰撞结果存入 m_RaycastResult，包含命中的 Entity、HitPosition、CellIndex 等。

2. 定义阶段 (Definition Creation)
* 执行系统：当前活跃的工具系统（默认为 Game.Tools.DefaultToolSystem）。
* 动作：当检测到射线命中的实体发生变化时，系统会通过 EntityCommandBuffer (ECB) 创建一个携带 Game.Tools.CreationDefinition 组件的临时实体。
* 关键标记：
    * CreationDefinition.m_Original = hitEntity（指向被悬停的原实体）。
    * CreationDefinition.m_Flags |= CreationFlags.Select（核心：标记此定义为选中预览态）。

3. 数据转换阶段 (Apply System)
* 执行系统：Game.Tools.ApplyObjectsSystem（建筑/对象）或 Game.Tools.ApplyNetSystem（道路/管线）。
* 动作：消费 CreationDefinition 实体，并生成一个真正的 Game.Tools.Temp 临时渲染实体。
* 组件继承：该临时实体的 Game.Tools.Temp 组件会将 CreationFlags.Select 转换为 TempFlags.Select。

4. 渲染消费阶段 (Batch Rendering)
* 执行系统：Game.Rendering.BatchInstanceSystem。
* 逻辑：在更新 GPU 实例数据（Instance Data）时，Job 会检查实体组件：

1     // 简化逻辑
2     if ((cullingData.m_Flags & PreCullingFlags.Temp) != 0U) {
3         Temp temp = m_TempData[entity];
4         if ((temp.m_Flags & TempFlags.Select) != 0U) {
5             meshLayer |= MeshLayer.Outline; // 强制分配到描边层
6             subMeshFlags |= SubMeshFlags.OutlineOnly; // 告诉 Shader 只画描边
7         }
8     }
* Shader 表现：Shader 接收到 MeshLayer.Outline 标记后，会根据 _HoveredColor（原版为 0.5, 0.5, 1.0, 1.0）在模型外围绘制轮廓。

  ---

三、 对象分类处理差异

1. 静态对象（建筑、树木、道具）
* 处理类：ObjectToolSystem / DefaultToolSystem。
* 逻辑：创建 CreationDefinition 时，通常会附带 ObjectDefinition 组件，用于同步原实体的位置和旋转，确保“高亮影子”与原物体完全重合。

2. 道路与管线（Net / Lane）
* 处理类：NetToolSystem。
* 逻辑：
    * 道路高亮不仅是整体，还包括节点（Node）和曲线（Curve）。
    * 高亮实体会携带 Game.Tools.NetCourse 组件，存储被悬停路段的贝塞尔曲线数据。
    * Gizmo 辅助：EditorGizmoSystem 会检查道路实体是否有 Highlighted 或 TempFlags.Select，并在其上方绘制蓝色的节点圆点或中心路径线。

  ---

四、 模组开发实现指南

如果你想在自己的模组工具中实现原版一致的高亮效果，请遵循以下 ECS 步骤：

方案 A：创建悬停预览（推荐，原版一致感强）
1. 在 Job 中创建实体：
   1     Entity e = commandBuffer.CreateEntity();
2. 添加定义组件：
   1     commandBuffer.AddComponent(e, new CreationDefinition {
   2         m_Original = targetEntity,
   3         m_Flags = CreationFlags.Select // 必须包含 Select
   4     });
3. 如果是建筑，添加 ObjectDefinition 同步坐标；如果是道路，添加 NetCourse 同步曲线。
4. 触发刷新：添加 Game.Common.Updated 组件。

方案 B：直接标记原实体（更轻量，适合持久选中）
1. 添加高亮标签：

1     EntityManager.AddComponent<Game.Tools.Highlighted>(targetEntity);
2. 强制重绘批次：
   1     EntityManager.AddComponent<Game.Common.Updated>(targetEntity);

  ---

五、 关键常量参考

* 标准高亮颜色 (RGBA): (128, 128, 255, 255) / 十六进制 #8080FFFF。
* 渲染图层: MeshLayer.Outline (Bitmask)。
* 核心组件:
    * Game.Tools.Highlighted (Tag Component)
    * Game.Tools.Temp (携带 TempFlags.Select)
    * Game.Tools.CreationDefinition (携带 CreationFlags.Select)

  ---

总结：CS2 的悬停高亮本质上是创建了一个开启了 Outline 图层渲染的临时副本实体。这种设计允许高亮效果与各种工具逻辑（如移动、升级、拆除）完美解耦。