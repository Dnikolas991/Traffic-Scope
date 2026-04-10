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
    /// Draws lightweight hover feedback in the world.
    /// Selected-object visuals are left to the vanilla game.
    /// </summary>
    public partial class OverlaySystem : GameSystemBase
    {
        private SelectionToolSystem m_ToolSystem;
        private OverlayRenderSystem m_OverlayRenderSystem;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_ToolSystem = World.GetOrCreateSystemManaged<SelectionToolSystem>();
            m_OverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();

            Logger.Info("OverlaySystem 已启动，仅绘制悬停高亮。");
        }

        protected override void OnUpdate()
        {
            OverlayRenderSystem.Buffer overlayBuffer = m_OverlayRenderSystem.GetBuffer(out JobHandle bufferHandle);
            bufferHandle.Complete();

            DrawHoveredOverlay(overlayBuffer);
        }

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
        }

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
