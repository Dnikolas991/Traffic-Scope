using Game;
using Unity.Entities;

namespace Transit_Scope.code
{
    /// <summary>
    /// 负责把“当前选中对象”转化为可展示的统计面板数据。
    /// 
    /// 这个系统不再自己解析 ECS 明细，而是只承担三件事：
    /// 1. 监听选择变化。
    /// 2. 定时刷新当前选中对象的分析结果。
    /// 3. 将分析结果转换成 UI 可用的统计结构。
    /// 
    /// 这样可以把“数据采集”和“数据显示”清晰拆开，避免业务逻辑继续堆叠。
    /// </summary>
    public partial class ScopeSystem : GameSystemBase
    {
        private const uint RefreshIntervalFrames = 30;

        private ScopeToolSystem m_ToolSystem;
        private ScopeUISystem m_UISystem;
        private ScopeTrafficFlowSystem m_FlowSystem;

        private Entity m_LastAnalyzedEntity = Entity.Null;
        private ScopeToolSystem.SelectionKind m_LastAnalyzedKind = ScopeToolSystem.SelectionKind.None;
        private uint m_FrameCounter;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_ToolSystem = World.GetOrCreateSystemManaged<ScopeToolSystem>();
            m_UISystem = World.GetOrCreateSystemManaged<ScopeUISystem>();
            m_FlowSystem = World.GetOrCreateSystemManaged<ScopeTrafficFlowSystem>();

            Logger.Info("ScopeSystem 已启动，正在监听选中对象并刷新统计面板。");
        }

        protected override void OnUpdate()
        {
            Entity selectedEntity = m_ToolSystem.SelectedEntity;
            ScopeToolSystem.SelectionKind selectedKind = m_ToolSystem.SelectedKind;
            bool hasNewSelection = m_ToolSystem.HasNewSelection;

            if (selectedEntity == Entity.Null || selectedKind == ScopeToolSystem.SelectionKind.None || !EntityManager.Exists(selectedEntity))
            {
                ClearPresentationState();
                m_ToolSystem.ClearNewSelectionFlag();
                return;
            }

            m_FrameCounter++;

            bool selectionChanged =
                hasNewSelection ||
                selectedEntity != m_LastAnalyzedEntity ||
                selectedKind != m_LastAnalyzedKind;

            bool reachedRefreshInterval = (m_FrameCounter % RefreshIntervalFrames) == 0;
            if (!selectionChanged && !reachedRefreshInterval)
            {
                return;
            }

            ScopeSelectionAnalysis analysis = m_FlowSystem.RefreshSelectionAnalysis(selectedEntity, selectedKind);
            m_ToolSystem.ClearNewSelectionFlag();

            m_LastAnalyzedEntity = selectedEntity;
            m_LastAnalyzedKind = selectedKind;

            if (analysis == null)
            {
                ClearPresentationState();
                return;
            }

            m_UISystem.PresentStats(BuildTrafficStats(analysis));
        }

        /// <summary>
        /// 统一清理 UI 与分析系统状态。
        /// 当玩家取消选择时，面板与导航路径应同时消失。
        /// </summary>
        private void ClearPresentationState()
        {
            m_LastAnalyzedEntity = Entity.Null;
            m_LastAnalyzedKind = ScopeToolSystem.SelectionKind.None;

            m_FlowSystem.ClearSelectionAnalysis();
            m_UISystem.ClearStats();
        }

        /// <summary>
        /// 把后端分析结果转换成前端可直接消费的统计卡片数据。
        /// </summary>
        private static ScopeSelectionStats BuildTrafficStats(ScopeSelectionAnalysis analysis)
        {
            TrafficCounters counters = analysis.CombinedTraffic;

            ScopeSelectionStats stats = new()
            {
                TitleKey = analysis.TitleKey,
                Title = analysis.FallbackTitle,
                SubtitleKey = analysis.SubtitleKey,
                SubtitleArg = analysis.SubtitleArgument,
                Subtitle = analysis.FallbackSubtitle,
                Total = counters.Total,
                DisplayTotal = counters.Total
            };

            AddStat(stats, "stats.item.private_cars", "Private Cars", counters.PersonalCars, "#5DB7FF");
            AddStat(stats, "stats.item.taxis", "Taxis", counters.Taxis, "#6ECF88");
            AddStat(stats, "stats.item.cargo", "Cargo", counters.Cargo, "#F2B35E");
            AddStat(stats, "stats.item.public_transit", "Public Transit", counters.PublicTransport, "#8B9BFF");
            AddStat(stats, "stats.item.city_service", "City Service", counters.CityService, "#FF8A7A");
            AddStat(stats, "stats.item.bicycles", "Bicycles", counters.Bicycles, "#60D5C0");
            AddStat(stats, "stats.item.rail", "Rail", counters.Trains, "#C38BFF");
            AddStat(stats, "stats.item.water", "Water", counters.Watercraft, "#4FC6F0");
            AddStat(stats, "stats.item.air", "Air", counters.Aircraft, "#F28DDA");
            AddStat(stats, "stats.item.pedestrians", "Pedestrians", counters.Humans, "#D6E2F0");
            AddStat(stats, "stats.item.other_vehicles", "Other Vehicles", counters.OtherVehicles, "#9AA7B6");
            AddStat(stats, "stats.item.others", "Others", counters.Others, "#738195");

            if (stats.Items.Count == 0)
            {
                AddStat(stats, "stats.item.no_traffic", "No Traffic", 1, "#7F8EA3");
                stats.Total = 1;
                stats.DisplayTotal = 0;
            }

            return stats;
        }

        /// <summary>
        /// 仅在统计值大于 0 时才向图表添加一项，避免 UI 出现无意义的空分类。
        /// </summary>
        private static void AddStat(ScopeSelectionStats stats, string labelKey, string fallbackLabel, int value, string color)
        {
            if (value <= 0)
            {
                return;
            }

            stats.Items.Add(new ScopeStatItem
            {
                LabelKey = labelKey,
                Label = fallbackLabel,
                Value = value,
                Color = color
            });
        }
    }
}
