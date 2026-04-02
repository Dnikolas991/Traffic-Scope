using Colossal.Mathematics;
using Game;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Rendering;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Transit_Scope.code
{
    /// <summary>
    /// 该系统专门负责 3D 空间中的视觉高亮 (Overlay)。
    /// 这里的实现逻辑参考了 Move It 和原版选中效果，旨在提供高性能且美观的视觉反馈。
    /// </summary>
    public partial class TransitScopeOverlaySystem : GameSystemBase
    {
        private TransitScopeToolSystem m_ToolSystem;
        private OverlayRenderSystem m_OverlayRenderSystem;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_ToolSystem = World.GetOrCreateSystemManaged<TransitScopeToolSystem>();
            m_OverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
            Logger.Info("TransitScopeOverlaySystem 已启动，正在监听高亮请求");
        }

        protected override void OnUpdate()
        {
            // 只有当工具激活、且有 Hover 对象、且尚未确认为 Selected 时，才绘制 Hover Overlay。
            // 已确定的 Selected 对象由原版 Marker 渲染。
            if (m_ToolSystem == null || !m_ToolSystem.ShouldRenderHoverOverlay)
            {
                return;
            }

            OverlayRenderSystem.Buffer overlayBuffer = m_OverlayRenderSystem.GetBuffer(out JobHandle bufferHandle);
            bufferHandle.Complete();

            Entity hoveredEntity = m_ToolSystem.HoveredEntity;
            TransitScopeToolSystem.SelectionKind hoveredKind = m_ToolSystem.HoveredKind;

            if (hoveredEntity == Entity.Null || !EntityManager.Exists(hoveredEntity))
            {
                return;
            }

            // 根据选中的类型（道路或建筑）调用不同的渲染路径
            if (hoveredKind == TransitScopeToolSystem.SelectionKind.Road)
            {
                DrawRoadHover(overlayBuffer, hoveredEntity);
            }
            else if (hoveredKind == TransitScopeToolSystem.SelectionKind.Building)
            {
                DrawBuildingHover(overlayBuffer, hoveredEntity);
            }
        }

        /// <summary>
        /// 道路/轨道高亮渲染。
        /// 采用“光晕蒙版 + 细描边”方案，较好地融入原版并突出目标。
        /// </summary>
        private void DrawRoadHover(OverlayRenderSystem.Buffer overlayBuffer, Entity edgeEntity)
        {
            if (!EntityManager.HasComponent<Curve>(edgeEntity))
            {
                return;
            }

            Curve curveData = EntityManager.GetComponentData<Curve>(edgeEntity);
            float roadWidth = GetRoadVisualWidth(edgeEntity);
            
            // 1. 宽幅蒙版：使用极低透明度的淡蓝色覆盖整段道路，产生选中感。
            float fillWidth = roadWidth + TransitScopeOverlayColors.RoadFillPadding;
            TransitScopeOverlayHelpers.DrawCurve(
                overlayBuffer,
                curveData.m_Bezier,
                Color.clear,
                TransitScopeOverlayColors.MainFill,
                0.01f,
                fillWidth);

            // 2. 精细描边：沿道路边缘绘制一道亮蓝色细线，增加专业感。
            float outlineCurveWidth = roadWidth + TransitScopeOverlayColors.RoadOutlinePadding;
            TransitScopeOverlayHelpers.DrawCurve(
                overlayBuffer,
                curveData.m_Bezier,
                TransitScopeOverlayColors.MainOutline,
                Color.clear,
                TransitScopeOverlayColors.RoadOutlineWidth,
                outlineCurveWidth);
        }

        /// <summary>
        /// 建筑 3D 轮廓渲染 (参考 Move It 设计)。
        /// 通过从 Prefab 读取本地 Bounds 并结合实体 Transform，生成精准的 3D 有向包围盒 (OBB)。
        /// </summary>
        private void DrawBuildingHover(OverlayRenderSystem.Buffer overlayBuffer, Entity buildingEntity)
        {
            // 基础组件检查
            if (!EntityManager.HasComponent<Game.Objects.Transform>(buildingEntity) || 
                !EntityManager.HasComponent<PrefabRef>(buildingEntity))
            {
                return;
            }

            // 获取实例的 Transform 和对应的 Prefab 引用
            Game.Objects.Transform transform = EntityManager.GetComponentData<Game.Objects.Transform>(buildingEntity);
            PrefabRef prefabRef = EntityManager.GetComponentData<PrefabRef>(buildingEntity);
            Entity prefabEntity = prefabRef.m_Prefab;

            // 重要：从 Prefab 中获取原始的几何包围盒 (ObjectGeometryData)
            // 这比 CullingInfo 更可靠，因为它代表了模型在本地空间的原始尺寸
            if (!EntityManager.HasComponent<ObjectGeometryData>(prefabEntity))
            {
                return;
            }

            ObjectGeometryData geometryData = EntityManager.GetComponentData<ObjectGeometryData>(prefabEntity);
            
            // 使用辅助函数生成 12 条 3D 线段。
            // 这里 geometryData.m_Bounds 是模型在本地空间的尺寸，不含旋转。
            // GetBuilding3DOutline 会负责将其应用 transform.m_Rotation 进行旋转。
            Line3.Segment[] edges = TransitScopeOverlayHelpers.GetBuilding3DOutline(
                transform, 
                geometryData.m_Bounds, 
                TransitScopeOverlayColors.BuildingExpand);

            // 批量绘制所有棱边，实现 Move It 风格的 3D 描边
            foreach (var edge in edges)
            {
                TransitScopeOverlayHelpers.DrawLine(
                    overlayBuffer, 
                    edge, 
                    TransitScopeOverlayColors.MainOutline, 
                    TransitScopeOverlayColors.BuildingOutlineWidth, 
                    projected: false); // false 表示 3D 空间绘制，不贴地，保持立体感
            }
        }

        /// <summary>
        /// 自动获取道路实际宽度，确保 Overlay 宽度能自适应高速公路或羊肠小道。
        /// </summary>
        private float GetRoadVisualWidth(Entity edgeEntity)
        {
            const float fallbackWidth = 8.0f;

            if (!EntityManager.HasComponent<EdgeGeometry>(edgeEntity))
            {
                return fallbackWidth;
            }

            EdgeGeometry edgeGeometry = EntityManager.GetComponentData<EdgeGeometry>(edgeEntity);
            float startWidth = math.distance(edgeGeometry.m_Start.m_Left.a, edgeGeometry.m_Start.m_Right.a);
            float endWidth = math.distance(edgeGeometry.m_End.m_Left.d, edgeGeometry.m_End.m_Right.d);
            return math.max(2f, (startWidth + endWidth) * 0.5f);
        }
    }
}
