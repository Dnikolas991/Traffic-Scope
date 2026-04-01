using Colossal.Mathematics;
using Game;
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
    /// 该系统专门负责 hover 视觉。
    /// confirm 后的原生 selected marker 仍由 ToolSystem.selected 接管。
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
            Logger.Info("TransitScopeOverlaySystem 已启动");
        }

        protected override void OnUpdate()
        {
            // 只在工具处于激活状态且当前 hover 还没有确认锁定时绘制 overlay。
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

            if (hoveredKind == TransitScopeToolSystem.SelectionKind.Road)
            {
                DrawRoadHover(overlayBuffer, hoveredEntity);
                return;
            }

            if (hoveredKind == TransitScopeToolSystem.SelectionKind.Building)
            {
                DrawBuildingHover(overlayBuffer, hoveredEntity);
            }
        }

        /// <summary>
        /// 道路与轨道 hover 使用双层曲线：
        /// 1. 更宽更淡的蒙版。
        /// 2. 更细的淡蓝描边。
        /// </summary>
        private void DrawRoadHover(OverlayRenderSystem.Buffer overlayBuffer, Entity edgeEntity)
        {
            if (!EntityManager.HasComponent<Curve>(edgeEntity))
            {
                return;
            }

            Curve curveData = EntityManager.GetComponentData<Curve>(edgeEntity);
            float roadWidth = GetRoadVisualWidth(edgeEntity);
            float fillWidth = roadWidth + TransitScopeOverlayColors.RoadFillPadding;
            float outlineCurveWidth = roadWidth + TransitScopeOverlayColors.RoadOutlinePadding;

            TransitScopeOverlayHelpers.DrawCurve(
                overlayBuffer,
                curveData.m_Bezier,
                Color.clear,
                TransitScopeOverlayColors.RoadFill,
                0.01f,
                fillWidth);

            TransitScopeOverlayHelpers.DrawCurve(
                overlayBuffer,
                curveData.m_Bezier,
                TransitScopeOverlayColors.RoadOutline,
                Color.clear,
                TransitScopeOverlayColors.RoadOutlineWidth,
                outlineCurveWidth);
        }

        /// <summary>
        /// 建筑当前回到较窄的地块边框方案。
        /// </summary>
        private void DrawBuildingHover(OverlayRenderSystem.Buffer overlayBuffer, Entity buildingEntity)
        {
            if (!TryGetBuildingHalfSize(buildingEntity, out float3 halfSize))
            {
                return;
            }

            Game.Objects.Transform transform = EntityManager.GetComponentData<Game.Objects.Transform>(buildingEntity);
            transform.m_Position.y += TransitScopeOverlayColors.BuildingLift;

            TransitScopeOverlayHelpers.CalculateRectangleLines(
                transform,
                TransitScopeOverlayColors.BuildingOutlineWidth,
                halfSize,
                out Line3.Segment left,
                out Line3.Segment right,
                out Line3.Segment back,
                out Line3.Segment front);

            TransitScopeOverlayHelpers.DrawLine(overlayBuffer, left, TransitScopeOverlayColors.BuildingOutline, TransitScopeOverlayColors.BuildingOutlineWidth, projected: true);
            TransitScopeOverlayHelpers.DrawLine(overlayBuffer, right, TransitScopeOverlayColors.BuildingOutline, TransitScopeOverlayColors.BuildingOutlineWidth, projected: true);
            TransitScopeOverlayHelpers.DrawLine(overlayBuffer, back, TransitScopeOverlayColors.BuildingOutline, TransitScopeOverlayColors.BuildingOutlineWidth, projected: true);
            TransitScopeOverlayHelpers.DrawLine(overlayBuffer, front, TransitScopeOverlayColors.BuildingOutline, TransitScopeOverlayColors.BuildingOutlineWidth, projected: true);
        }

        /// <summary>
        /// 道路实际视觉宽度优先尝试从 EdgeGeometry 的左右边界估算。
        /// 这样不同道路、轨道、地铁线的 hover 蒙版都会自动跟着变宽窄。
        /// </summary>
        private float GetRoadVisualWidth(Entity edgeEntity)
        {
            const float fallbackWidth = 10.5f;

            if (!EntityManager.HasComponent<EdgeGeometry>(edgeEntity))
            {
                return fallbackWidth;
            }

            EdgeGeometry edgeGeometry = EntityManager.GetComponentData<EdgeGeometry>(edgeEntity);
            float startWidth = math.distance(edgeGeometry.m_Start.m_Left.a, edgeGeometry.m_Start.m_Right.a);
            float endWidth = math.distance(edgeGeometry.m_End.m_Left.d, edgeGeometry.m_End.m_Right.d);
            return math.max(fallbackWidth, (startWidth + endWidth) * 0.5f);
        }

        /// <summary>
        /// 从 prefab 读取建筑地块尺寸。
        /// 这里继续沿用 Move It 常见的 lot size * 4 的世界长度换算方式。
        /// </summary>
        private bool TryGetBuildingHalfSize(Entity buildingEntity, out float3 halfSize)
        {
            halfSize = new float3(8f, 0f, 8f);

            if (!EntityManager.HasComponent<Game.Objects.Transform>(buildingEntity))
            {
                return false;
            }

            if (!EntityManager.HasComponent<PrefabRef>(buildingEntity))
            {
                return true;
            }

            PrefabRef prefabRef = EntityManager.GetComponentData<PrefabRef>(buildingEntity);
            Entity prefabEntity = prefabRef.m_Prefab;
            if (prefabEntity == Entity.Null || !EntityManager.Exists(prefabEntity))
            {
                return true;
            }

            if (EntityManager.HasComponent<BuildingExtensionData>(prefabEntity))
            {
                BuildingExtensionData extensionData = EntityManager.GetComponentData<BuildingExtensionData>(prefabEntity);
                halfSize = new float3(
                    math.max(1f, extensionData.m_LotSize.x * 4f),
                    0f,
                    math.max(1f, extensionData.m_LotSize.y * 4f));
                return true;
            }

            if (EntityManager.HasComponent<BuildingData>(prefabEntity))
            {
                BuildingData buildingData = EntityManager.GetComponentData<BuildingData>(prefabEntity);
                halfSize = new float3(
                    math.max(1f, buildingData.m_LotSize.x * 4f),
                    0f,
                    math.max(1f, buildingData.m_LotSize.y * 4f));
            }

            return true;
        }
    }
}
