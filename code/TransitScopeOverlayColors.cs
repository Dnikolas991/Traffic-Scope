using UnityEngine;

namespace Transit_Scope.code
{
    /// <summary>
    /// 统一管理 Transit Scope 的 Overlay 视觉参数。
    /// 这里的配色和线宽参考了原版选中效果、Traffic 和 Move It，旨在提供更现代、专业的视觉反馈。
    /// </summary>
    internal static class TransitScopeOverlayColors
    {
        // --- 核心配色 (淡蓝色系) ---
        
        /// <summary>
        /// 悬停/选中时的主要描边颜色 (淡蓝色)。
        /// 具有高不透明度，确保边缘清晰可见。
        /// </summary>
        public static readonly Color MainOutline = new Color(0.0f, 0.75f, 1.0f, 0.95f);

        /// <summary>
        /// 悬停/选中时的填充蒙版颜色 (极淡蓝色)。
        /// 低不透明度 (0.12) 可以在高亮目标的同时不遮挡地图细节。
        /// </summary>
        public static readonly Color MainFill = new Color(0.0f, 0.65f, 1.0f, 0.12f);

        // --- 道路与轨道参数 ---

        /// <summary>
        /// 道路描边的线宽。
        /// </summary>
        public const float RoadOutlineWidth = 1.2f;

        /// <summary>
        /// 道路填充蒙版向外扩张的距离。
        /// 较大的 Padding (4.5f) 可以营造出类似“发光”的蒙版效果。
        /// </summary>
        public const float RoadFillPadding = 4.5f;

        /// <summary>
        /// 道路描边相对道路本体向外扩张的距离。
        /// </summary>
        public const float RoadOutlinePadding = 0.8f;

        // --- 建筑 3D 参数 ---

        /// <summary>
        /// 建筑 3D 轮廓线的宽度。
        /// </summary>
        public const float BuildingOutlineWidth = 1.5f;

        /// <summary>
        /// 建筑 3D 线框微量外扩量，防止线条和模型面重叠产生 Z-fighting 闪烁。
        /// </summary>
        public const float BuildingExpand = 0.15f;
    }
}
