using UnityEngine;

namespace Transit_Scope.code
{
    /// <summary>
    /// 统一管理 Transit Scope 的 overlay 颜色与线宽。
    /// </summary>
    internal static class TransitScopeOverlayColors
    {
        /// <summary>
        /// 道路与轨道 hover 的外层描边颜色。
        /// </summary>
        public static readonly Color RoadOutline = new Color(0.60f, 0.86f, 1.00f, 0.98f);

        /// <summary>
        /// 道路与轨道 hover 的填充蒙版颜色。
        /// 透明度较低，保证下面的道路纹理仍然清晰可见。
        /// </summary>
        public static readonly Color RoadFill = new Color(0.42f, 0.74f, 1.00f, 0.11f);

        /// <summary>
        /// 建筑 hover 的边框颜色。
        /// </summary>
        public static readonly Color BuildingOutline = new Color(0.58f, 0.84f, 1.00f, 0.98f);

        /// <summary>
        /// 预留给建筑 hover 填充色，当前未启用。
        /// </summary>
        public static readonly Color BuildingFill = new Color(0.40f, 0.70f, 1.00f, 0.08f);

        /// <summary>
        /// 道路描边线宽，保持比建筑边框更细。
        /// </summary>
        public const float RoadOutlineWidth = 1.85f;

        /// <summary>
        /// 道路填充蒙版向外扩张的距离。
        /// </summary>
        public const float RoadFillPadding = 3.20f;

        /// <summary>
        /// 道路描边相对道路本体向外扩张的距离。
        /// </summary>
        public const float RoadOutlinePadding = 1.10f;

        /// <summary>
        /// 建筑边框线宽，当前按用户要求收窄一些。
        /// </summary>
        public const float BuildingOutlineWidth = 1.85f;

        /// <summary>
        /// 建筑矩形方案整体向上抬一点，避免与地面重合闪烁。
        /// </summary>
        public const float BuildingLift = 0.12f;
    }
}
