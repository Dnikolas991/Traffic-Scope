using Game;
using Unity.Entities;

namespace Transit_Scope.code
{
    /// <summary>
    /// Coordinates selection changes, route statistics refresh, and panel presentation.
    /// </summary>
    public partial class SelectionSystem : GameSystemBase
    {
        private const int RefreshIntervalFrames = 30;

        private SelectionToolSystem m_ToolSystem;
        private UIBridgeSystem m_UISystem;
        private TrafficFlowSystem m_FlowSystem;

        private Entity m_LastAnalyzedEntity = Entity.Null;
        private SelectionToolSystem.SelectionKind m_LastAnalyzedKind = SelectionToolSystem.SelectionKind.None;
        private int m_FramesUntilNextRefresh;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_ToolSystem = World.GetOrCreateSystemManaged<SelectionToolSystem>();
            m_UISystem = World.GetOrCreateSystemManaged<UIBridgeSystem>();
            m_FlowSystem = World.GetOrCreateSystemManaged<TrafficFlowSystem>();

            Logger.Info("SelectionSystem 已切换为原版式线路统计刷新逻辑。");
        }

        protected override void OnUpdate()
        {
            Entity selectedEntity = m_ToolSystem.SelectedEntity;
            Entity sourceEntity = m_ToolSystem.SelectedSourceEntity != Entity.Null ? m_ToolSystem.SelectedSourceEntity : selectedEntity;
            SelectionToolSystem.SelectionKind selectedKind = m_ToolSystem.SelectedKind;
            bool hasNewSelection = m_ToolSystem.HasNewSelection;

            if (selectedEntity == Entity.Null ||
                selectedKind == SelectionToolSystem.SelectionKind.None ||
                !EntityManager.Exists(selectedEntity))
            {
                ClearPresentationState();
                m_ToolSystem.ClearNewSelectionFlag();
                return;
            }

            bool selectionChanged =
                hasNewSelection ||
                sourceEntity != m_LastAnalyzedEntity ||
                selectedKind != m_LastAnalyzedKind;

            if (!selectionChanged && m_FramesUntilNextRefresh > 0)
            {
                m_FramesUntilNextRefresh--;
                m_ToolSystem.ClearNewSelectionFlag();
                return;
            }

            RouteStatisticsSnapshot snapshot = m_FlowSystem.RefreshRouteStatistics(sourceEntity, selectedKind, m_ToolSystem.SelectedIndex);
            m_ToolSystem.ClearNewSelectionFlag();

            m_LastAnalyzedEntity = sourceEntity;
            m_LastAnalyzedKind = selectedKind;
            m_FramesUntilNextRefresh = RefreshIntervalFrames;

            Logger.Info(
                $"[RouteStats] Selection refresh selected={selectedKind} display=#{selectedEntity.Index} source=#{sourceEntity.Index} " +
                $"snapshot={(snapshot != null ? "ok" : "null")}");

            if (snapshot == null)
            {
                ClearPresentationState();
                return;
            }

            m_UISystem.PresentStats(RouteStatisticsPanelPayload.FromSnapshot(snapshot));
        }

        private void ClearPresentationState()
        {
            m_LastAnalyzedEntity = Entity.Null;
            m_LastAnalyzedKind = SelectionToolSystem.SelectionKind.None;
            m_FramesUntilNextRefresh = 0;

            m_FlowSystem.ClearRouteStatistics();
            m_UISystem.ClearStats();
        }
    }
}
