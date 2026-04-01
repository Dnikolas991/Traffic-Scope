using Colossal.Mathematics;
using Game.Rendering;
using Unity.Mathematics;
using UnityEngine;

namespace Transit_Scope.code
{
    /// <summary>
    /// 所有 overlay 绘制辅助方法集中放在这里。
    /// 这样 OverlaySystem 只负责“决定画什么”，具体怎么画交给 helper。
    /// </summary>
    internal static class TransitScopeOverlayHelpers
    {
        /// <summary>
        /// projected 风格会把线条和曲线更自然地贴合地形，适合道路 hover。
        /// </summary>
        public static readonly OverlayRenderSystem.StyleFlags Projected = OverlayRenderSystem.StyleFlags.Projected;

        /// <summary>
        /// 固定 3D 线框更适合建筑立体轮廓，否则垂直边线会被投影到地面，不够像“包住模型”。
        /// </summary>
        public static readonly OverlayRenderSystem.StyleFlags Fixed = 0;

        /// <summary>
        /// 绘制一条 Bezier 曲线 overlay。
        /// 道路与轨道的 hover 填充和描边都会通过这个方法完成。
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
        /// 绘制一条普通线段。
        /// projected=true 时更适合地面矩形和辅助线，projected=false 时更适合 3D 立体轮廓。
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
        /// 绘制虚线，当前主要用于地块退化方案中的朝向辅助线。
        /// </summary>
        public static void DrawDashedLine(
            OverlayRenderSystem.Buffer buffer,
            Line3.Segment line,
            Color color,
            float width,
            float dashLength,
            float gapLength,
            bool projected)
        {
            buffer.DrawDashedLine(
                color,
                color,
                width,
                projected ? Projected : Fixed,
                line,
                width,
                dashLength,
                gapLength);
        }

        /// <summary>
        /// 按 position + rotation 把局部偏移量旋转到世界空间。
        /// 主要用于地块矩形 fallback 方案。
        /// </summary>
        public static float3 RotateAroundPivot(float3 position, quaternion rotation, float3 offset)
        {
            return position + math.mul(rotation, offset);
        }

        /// <summary>
        /// 根据建筑 transform 和半尺寸，计算地块矩形四条边。
        /// 当拿不到真实 3D bounds 时，退回这个方案。
        /// </summary>
        public static void CalculateRectangleLines(
            Game.Objects.Transform transform,
            float width,
            float3 halfSize,
            out Line3.Segment left,
            out Line3.Segment right,
            out Line3.Segment back,
            out Line3.Segment front)
        {
            float3 position = transform.m_Position;
            quaternion rotation = transform.m_Rotation;

            float3 a = new(halfSize.x, halfSize.y, -halfSize.z);
            a.x -= width / 2f;
            a.z += width;
            a = RotateAroundPivot(position, rotation, a);

            float3 b = halfSize;
            b.x -= width / 2f;
            b.z -= width;
            b = RotateAroundPivot(position, rotation, b);
            left = new Line3.Segment(a, b);

            a = -halfSize;
            a.x += width / 2f;
            a.z += width;
            a = RotateAroundPivot(position, rotation, a);

            b = new float3(-halfSize.x, halfSize.y, halfSize.z);
            b.x += width / 2f;
            b.z -= width;
            b = RotateAroundPivot(position, rotation, b);
            right = new Line3.Segment(a, b);

            a = -halfSize;
            a.z += width / 2f;
            a = RotateAroundPivot(position, rotation, a);

            b = new float3(halfSize.x, halfSize.y, -halfSize.z);
            b.z += width / 2f;
            b = RotateAroundPivot(position, rotation, b);
            back = new Line3.Segment(a, b);

            a = new float3(-halfSize.x, halfSize.y, halfSize.z);
            a.z -= width / 2f;
            a = RotateAroundPivot(position, rotation, a);

            b = halfSize;
            b.z -= width / 2f;
            b = RotateAroundPivot(position, rotation, b);
            front = new Line3.Segment(a, b);
        }

        /// <summary>
        /// 根据一个 3D 包围盒生成 12 条边线。
        /// 这样可以把建筑整个模型体积勾勒出来，而不是只围绕地基画一圈。
        /// </summary>
        public static Line3.Segment[] CalculateBoundsLines(Bounds3 bounds)
        {
            float3 min = bounds.min;
            float3 max = bounds.max;

            float3 fbl = new(min.x, min.y, min.z);
            float3 fbr = new(max.x, min.y, min.z);
            float3 bbl = new(min.x, min.y, max.z);
            float3 bbr = new(max.x, min.y, max.z);

            float3 ftl = new(min.x, max.y, min.z);
            float3 ftr = new(max.x, max.y, min.z);
            float3 btl = new(min.x, max.y, max.z);
            float3 btr = new(max.x, max.y, max.z);

            return new[]
            {
                new Line3.Segment(fbl, fbr),
                new Line3.Segment(fbr, bbr),
                new Line3.Segment(bbr, bbl),
                new Line3.Segment(bbl, fbl),

                new Line3.Segment(ftl, ftr),
                new Line3.Segment(ftr, btr),
                new Line3.Segment(btr, btl),
                new Line3.Segment(btl, ftl),

                new Line3.Segment(fbl, ftl),
                new Line3.Segment(fbr, ftr),
                new Line3.Segment(bbr, btr),
                new Line3.Segment(bbl, btl)
            };
        }

        /// <summary>
        /// 对 bounds 做轻微外扩，避免线条和模型表面完全重合。
        /// </summary>
        public static Bounds3 ExpandBounds(Bounds3 bounds, float amount)
        {
            float3 expand = new(amount, amount, amount);
            return new Bounds3(bounds.min - expand, bounds.max + expand);
        }
    }
}
