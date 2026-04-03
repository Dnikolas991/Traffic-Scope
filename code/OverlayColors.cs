using UnityEngine;

namespace Transit_Scope.code
{
    /// <summary>
    /// Overlay 颜色和尺寸参数。
    /// </summary>
    internal static class OverlayColors
    {
        public static readonly Color MainOutline = new(0.00f, 0.75f, 1.00f, 0.95f);
        public static readonly Color MainFill = new(0.00f, 0.65f, 1.00f, 0.12f);

        public static readonly Color RouteOutline = new(0.10f, 0.90f, 0.95f, 0.45f);
        public static readonly Color RouteFill = new(0.10f, 0.90f, 0.95f, 0.08f);

        public const float RoadOutlineWidth = 1.2f;
        public const float RoadFillPadding = 4.5f;
        public const float RoadOutlinePadding = 0.8f;

        public const float RouteOutlineWidth = 0.8f;
        public const float RoutePadding = 2.4f;

        public const float BuildingOutlineWidth = 1.5f;
        public const float BuildingExpand = 0.15f;
    }
}
