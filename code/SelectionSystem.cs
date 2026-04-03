using Game;
using Unity.Entities;

namespace Transit_Scope.code
{
    // 负责把“当前选中对象”转化为可展示的统计面板数据。
    // 
    // 这个系统不再自己解析 ECS 明细，而是只承担三件事：
    // 1. 监听选择变化。
    // 2. 定时刷新当前选中对象的分析结果。
    // 3. 将分析结果转换成 UI 可用的统计结构。
    // 
    // 这样可以把“数据采集”和“数据显示”清晰拆开，避免业务逻辑继续堆叠。
    public partial class SelectionSystem : GameSystemBase
    {
        //连续未更换对象时的刷新频率——30帧
        private const uint RefreshIntervalFrames = 30;

        //提供选中的对象
        private SelectionToolSystem m_ToolSystem;
        //前后端
        private UIBridgeSystem m_UISystem;
        private TrafficFlowSystem m_FlowSystem;

        //上一次分析过的实体
        private Entity m_LastAnalyzedEntity = Entity.Null;
        //上次分析的类型
        private SelectionToolSystem.SelectionKind m_LastAnalyzedKind = SelectionToolSystem.SelectionKind.None;
        //帧数计数器
        private uint m_FrameCounter;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_ToolSystem = World.GetOrCreateSystemManaged<SelectionToolSystem>();
            m_UISystem = World.GetOrCreateSystemManaged<UIBridgeSystem>();
            m_FlowSystem = World.GetOrCreateSystemManaged<TrafficFlowSystem>();

            Logger.Info("SelectionSystem 已启动，正在监听选中对象并刷新统计面板。");
        }

        protected override void OnUpdate()
        {
            // 先推进一帧导航路线账本，再处理当前选择的展示逻辑。
            // 这样当前帧 UI 看到的 future traffic 始终基于最新的生命周期统计结果。
            m_FlowSystem.AdvanceRouteLedger();

            //从工具类获取选中的实体
            Entity selectedEntity = m_ToolSystem.SelectedEntity;
            SelectionToolSystem.SelectionKind selectedKind = m_ToolSystem.SelectedKind;
            //是否选择了新对象
            bool hasNewSelection = m_ToolSystem.HasNewSelection;

            //未选中、类型是none、选中的实体不存在，清空页面并退出
            if (selectedEntity == Entity.Null || selectedKind == SelectionToolSystem.SelectionKind.None || !EntityManager.Exists(selectedEntity))
            {
                ClearPresentationState();
                m_ToolSystem.ClearNewSelectionFlag();
                return;
            }

            m_FrameCounter++;

            //认为选择改变的三种情况
            bool selectionChanged =
                hasNewSelection ||
                selectedEntity != m_LastAnalyzedEntity ||
                selectedKind != m_LastAnalyzedKind;

            //节流内容，未超过最小刷新间隔则不刷新
            bool reachedRefreshInterval = (m_FrameCounter % RefreshIntervalFrames) == 0;
            if (!selectionChanged && !reachedRefreshInterval)
            {
                return;
            }

            SelectionAnalysis analysis = m_FlowSystem.RefreshSelectionAnalysis(selectedEntity, selectedKind);
            m_ToolSystem.ClearNewSelectionFlag();

            //更新分析缓存
            m_LastAnalyzedEntity = selectedEntity;
            m_LastAnalyzedKind = selectedKind;

            //分析失败默认退出
            if (analysis == null)
            {
                ClearPresentationState();
                return;
            }

            //传递分析结果
            m_UISystem.PresentStats(BuildTrafficStats(analysis));
        }
        
        // 统一清理 UI 与分析系统状态。
        // 当玩家取消选择时，面板与导航路径应同时消失。
        private void ClearPresentationState()
        {
            m_LastAnalyzedEntity = Entity.Null;
            m_LastAnalyzedKind = SelectionToolSystem.SelectionKind.None;

            m_FlowSystem.ClearSelectionAnalysis();
            m_UISystem.ClearStats();
        }

        /// <summary>
        /// 把后端分析结果转换成前端可直接消费的统计卡片数据。
        /// </summary>
        private static SelectionStats BuildTrafficStats(SelectionAnalysis analysis)
        {
            TrafficCounters counters = analysis.CombinedTraffic;

            SelectionStats stats = new()
            {
                //本地化key，便于多语言支持
                TitleKey = analysis.TitleKey,
                //没查找到key的默认值
                Title = analysis.FallbackTitle,
                //副标题
                SubtitleKey = analysis.SubtitleKey,
                //副标题的插值参数
                SubtitleArg = analysis.SubtitleArgument,
                //副标题的默认文本
                Subtitle = analysis.FallbackSubtitle,
                //图表计算使用的总量，没有流量用1计算
                Total = counters.Total,
                //显示的总量，是真实值
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
        private static void AddStat(SelectionStats stats, string labelKey, string fallbackLabel, int value, string color)
        {
            if (value <= 0)
            {
                return;
            }

            stats.Items.Add(new StatItem
            {
                LabelKey = labelKey,
                Label = fallbackLabel,
                Value = value,
                Color = color
            });
        }
    }
}
