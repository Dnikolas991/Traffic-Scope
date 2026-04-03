using System.Collections.Generic;
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
    /// 负责绘制 Transit Scope 的所有空间高亮。
    /// 
    /// 当前包含两类视觉元素：
    /// 1. Hover 高亮：鼠标悬停目标的即时描边。
    /// 2. Route 高亮：当前选中对象对应的未来导航路径。
    /// </summary>
    public partial class OverlaySystem : GameSystemBase
    {
        private SelectionToolSystem m_ToolSystem;
        private TrafficFlowSystem m_FlowSystem;
        private OverlayRenderSystem m_OverlayRenderSystem;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_ToolSystem = World.GetOrCreateSystemManaged<SelectionToolSystem>();
            m_FlowSystem = World.GetOrCreateSystemManaged<TrafficFlowSystem>();
            m_OverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();

            Logger.Info("OverlaySystem 已启动，开始绘制悬停高亮和导航路径。");
        }

        protected override void OnUpdate()
        {
            OverlayRenderSystem.Buffer overlayBuffer = m_OverlayRenderSystem.GetBuffer(out JobHandle bufferHandle);
            bufferHandle.Complete();

            DrawSelectedRouteOverlay(overlayBuffer);
            DrawHoveredOverlay(overlayBuffer);
        }

        /// <summary>
        /// 绘制与当前选中对象相关的导航路径。
        /// 边的透明度和宽度会根据命中数量略微增强，让主干流向更容易被看见。
        /// </summary>
        private void DrawSelectedRouteOverlay(OverlayRenderSystem.Buffer overlayBuffer)
        {
            SelectionAnalysis analysis = m_FlowSystem.CurrentSelectionAnalysis;
            if (analysis == null || analysis.RouteEdgeWeights.Count == 0)
            {
                return;
            }

            int maxWeight = 1;
            foreach (KeyValuePair<Entity, int> routeEntry in analysis.RouteEdgeWeights)
            {
                if (routeEntry.Value > maxWeight)
                {
                    maxWeight = routeEntry.Value;
                }
            }

            foreach (KeyValuePair<Entity, int> routeEntry in analysis.RouteEdgeWeights)
            {
                Entity edgeEntity = routeEntry.Key;
                if (edgeEntity == Entity.Null || !EntityManager.Exists(edgeEntity) || !EntityManager.HasComponent<Curve>(edgeEntity))
                {
                    continue;
                }

                Curve curveData = EntityManager.GetComponentData<Curve>(edgeEntity);
                float roadWidth = GetRoadVisualWidth(edgeEntity);

                float intensity = math.saturate(routeEntry.Value / (float)maxWeight);
                float routeWidth = roadWidth + OverlayColors.RoutePadding + intensity * 2.0f;
                float outlineWidth = OverlayColors.RouteOutlineWidth + intensity * 0.6f;

                Color fillColor = OverlayColors.RouteFill;
                fillColor.a += intensity * 0.12f;

                Color outlineColor = OverlayColors.RouteOutline;
                outlineColor.a += intensity * 0.18f;

                OverlayHelpers.DrawCurve(
                    overlayBuffer,
                    curveData.m_Bezier,
                    outlineColor,
                    fillColor,
                    outlineWidth,
                    routeWidth);
            }
        }

        /// <summary>
        /// 绘制当前鼠标悬停对象的高亮。
        /// 悬停态始终优先于路线高亮，因此后绘制。
        /// </summary>
        private void DrawHoveredOverlay(OverlayRenderSystem.Buffer overlayBuffer)
        {
            if (m_ToolSystem == null || !m_ToolSystem.ShouldRenderHoverOverlay)
            {
                return;
            }

            Entity hoveredEntity = m_ToolSystem.HoveredEntity;
            SelectionToolSystem.SelectionKind hoveredKind = m_ToolSystem.HoveredKind;

            if (hoveredEntity == Entity.Null || !EntityManager.Exists(hoveredEntity))
            {
                return;
            }

            if (hoveredKind == SelectionToolSystem.SelectionKind.Road)
            {
                DrawRoadHover(overlayBuffer, hoveredEntity);
            }
            else if (hoveredKind == SelectionToolSystem.SelectionKind.Building)
            {
                DrawBuildingHover(overlayBuffer, hoveredEntity);
            }
        }

        /// <summary>
        /// 绘制道路或轨道的悬停高亮。
        /// </summary>
        private void DrawRoadHover(OverlayRenderSystem.Buffer overlayBuffer, Entity edgeEntity)
        {
            if (!EntityManager.HasComponent<Curve>(edgeEntity))
            {
                return;
            }

            Curve curveData = EntityManager.GetComponentData<Curve>(edgeEntity);
            float roadWidth = GetRoadVisualWidth(edgeEntity);

            OverlayHelpers.DrawCurve(
                overlayBuffer,
                curveData.m_Bezier,
                Color.clear,
                OverlayColors.MainFill,
                0.01f,
                roadWidth + OverlayColors.RoadFillPadding);

            OverlayHelpers.DrawCurve(
                overlayBuffer,
                curveData.m_Bezier,
                OverlayColors.MainOutline,
                Color.clear,
                OverlayColors.RoadOutlineWidth,
                roadWidth + OverlayColors.RoadOutlinePadding);
        }

        /// <summary>
        /// 绘制建筑悬停高亮。
        /// 这里基于 Prefab 的几何包围盒来绘制 3D 线框。
        /// </summary>
        private void DrawBuildingHover(OverlayRenderSystem.Buffer overlayBuffer, Entity buildingEntity)
        {
            if (!EntityManager.HasComponent<Game.Objects.Transform>(buildingEntity) ||
                !EntityManager.HasComponent<PrefabRef>(buildingEntity))
            {
                return;
            }

            Game.Objects.Transform transform = EntityManager.GetComponentData<Game.Objects.Transform>(buildingEntity);
            PrefabRef prefabReference = EntityManager.GetComponentData<PrefabRef>(buildingEntity);
            Entity prefabEntity = prefabReference.m_Prefab;

            if (!EntityManager.HasComponent<ObjectGeometryData>(prefabEntity))
            {
                return;
            }

            ObjectGeometryData geometryData = EntityManager.GetComponentData<ObjectGeometryData>(prefabEntity);
            Line3.Segment[] outlineEdges = OverlayHelpers.GetBuilding3DOutline(
                transform,
                geometryData.m_Bounds,
                OverlayColors.BuildingExpand);

            foreach (Line3.Segment edge in outlineEdges)
            {
                OverlayHelpers.DrawLine(
                    overlayBuffer,
                    edge,
                    OverlayColors.MainOutline,
                    OverlayColors.BuildingOutlineWidth,
                    projected: false);
            }
        }

        /// <summary>
        /// 获取道路近似可视宽度。
        /// 如果某条边没有几何数据，则使用保守的回退值。
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
