using Colossal.Mathematics;
using Game.Rendering;
using Unity.Mathematics;
using UnityEngine;

namespace Transit_Scope.code
{
    /// <summary>
    /// Overlay 绘制辅助函数。
    /// </summary>
    internal static class ScopeOverlayHelpers
    {
        public static readonly OverlayRenderSystem.StyleFlags Projected = OverlayRenderSystem.StyleFlags.Projected;
        public static readonly OverlayRenderSystem.StyleFlags Fixed = 0;

        public static void DrawCurve(
            OverlayRenderSystem.Buffer buffer,
            Bezier4x3 curve,
            Color outlineColor,
            Color fillColor,
            float outlineWidth,
            float curveWidth)
        {
            buffer.DrawCurve(
                outlineColor,
                fillColor,
                outlineWidth,
                Projected,
                curve,
                curveWidth,
                1f);
        }

        public static void DrawLine(
            OverlayRenderSystem.Buffer buffer,
            Line3.Segment line,
            Color color,
            float width,
            bool projected)
        {
            buffer.DrawLine(
                color,
                color,
                width,
                projected ? Projected : Fixed,
                line,
                width,
                new float2());
        }

        public static Line3.Segment[] GetBuilding3DOutline(Game.Objects.Transform transform, Bounds3 localBounds, float expand)
        {
            float3 min = localBounds.min - new float3(expand, expand, expand);
            float3 max = localBounds.max + new float3(expand, expand, expand);

            float3[] vertices = new float3[8];
            vertices[0] = new float3(min.x, min.y, min.z);
            vertices[1] = new float3(max.x, min.y, min.z);
            vertices[2] = new float3(max.x, min.y, max.z);
            vertices[3] = new float3(min.x, min.y, max.z);
            vertices[4] = new float3(min.x, max.y, min.z);
            vertices[5] = new float3(max.x, max.y, min.z);
            vertices[6] = new float3(max.x, max.y, max.z);
            vertices[7] = new float3(min.x, max.y, max.z);

            for (int index = 0; index < vertices.Length; index++)
            {
                vertices[index] = transform.m_Position + math.mul(transform.m_Rotation, vertices[index]);
            }

            return new[]
            {
                new Line3.Segment(vertices[0], vertices[1]),
                new Line3.Segment(vertices[1], vertices[2]),
                new Line3.Segment(vertices[2], vertices[3]),
                new Line3.Segment(vertices[3], vertices[0]),
                new Line3.Segment(vertices[4], vertices[5]),
                new Line3.Segment(vertices[5], vertices[6]),
                new Line3.Segment(vertices[6], vertices[7]),
                new Line3.Segment(vertices[7], vertices[4]),
                new Line3.Segment(vertices[0], vertices[4]),
                new Line3.Segment(vertices[1], vertices[5]),
                new Line3.Segment(vertices[2], vertices[6]),
                new Line3.Segment(vertices[3], vertices[7])
            };
        }
    }
}
