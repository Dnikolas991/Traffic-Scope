using Colossal.Mathematics;
using Game.Rendering;
using Unity.Mathematics;
using UnityEngine;

namespace Transit_Scope.code
{
    /// <summary>
    /// 所有 Overlay 绘制辅助方法。
    /// 这里主要处理数学几何转换，将 3D Bounds 转换为世界空间的 12 条边。
    /// </summary>
    internal static class TransitScopeOverlayHelpers
    {
        public static readonly OverlayRenderSystem.StyleFlags Projected = OverlayRenderSystem.StyleFlags.Projected;
        public static readonly OverlayRenderSystem.StyleFlags Fixed = 0;

        /// <summary>
        /// 绘制 Bezier 曲线。用于道路高亮。
        /// </summary>
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

        /// <summary>
        /// 绘制单条线段。用于建筑 3D 线框。
        /// </summary>
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

        /// <summary>
        /// 根据建筑的本地 3D Bounds 和世界坐标 Transform，计算出世界空间的 12 条线框边。
        /// </summary>
        /// <param name="transform">建筑的世界坐标变换</param>
        /// <param name="localBounds">建筑在 prefab 中定义的本地包围盒</param>
        /// <param name="expand">外扩量，防止线条埋进模型</param>
        /// <returns>12 条 3D 线段</returns>
        public static Line3.Segment[] GetBuilding3DOutline(Game.Objects.Transform transform, Bounds3 localBounds, float expand)
        {
            float3 min = localBounds.min - new float3(expand, expand, expand);
            float3 max = localBounds.max + new float3(expand, expand, expand);

            // 获取本地空间的 8 个顶点
            float3[] v = new float3[8];
            v[0] = new float3(min.x, min.y, min.z);
            v[1] = new float3(max.x, min.y, min.z);
            v[2] = new float3(max.x, min.y, max.z);
            v[3] = new float3(min.x, min.y, max.z);
            v[4] = new float3(min.x, max.y, min.z);
            v[5] = new float3(max.x, max.y, min.z);
            v[6] = new float3(max.x, max.y, max.z);
            v[7] = new float3(min.x, max.y, max.z);

            // 将所有顶点根据建筑的旋转和位置变换到世界空间
            for (int i = 0; i < 8; i++)
            {
                v[i] = transform.m_Position + math.mul(transform.m_Rotation, v[i]);
            }

            // 返回 12 条线段，勾勒出长方体轮廓
            return new[]
            {
                // 底面四边
                new Line3.Segment(v[0], v[1]), new Line3.Segment(v[1], v[2]),
                new Line3.Segment(v[2], v[3]), new Line3.Segment(v[3], v[0]),
                // 顶面四边
                new Line3.Segment(v[4], v[5]), new Line3.Segment(v[5], v[6]),
                new Line3.Segment(v[6], v[7]), new Line3.Segment(v[7], v[4]),
                // 垂直连接边
                new Line3.Segment(v[0], v[4]), new Line3.Segment(v[1], v[5]),
                new Line3.Segment(v[2], v[6]), new Line3.Segment(v[3], v[7])
            };
        }
    }
}
