using Colossal.UI.Binding;
using Game.UI;
using Unity.Entities;

namespace Transit_Scope.code
{
    /// <summary>
    /// 前后端 UI 绑定系统。
    /// 除了工具开关外，现在还负责把统计结果推送给前端。
    /// </summary>
    public partial class TransitScopeUISystem : UISystemBase
    {
        private ValueBinding<bool> m_ActiveBinding;
        private ValueBinding<bool> m_HasStatsBinding;
        private ValueBinding<string> m_StatsJsonBinding;

        private TransitScopeToolSystem m_TransitScopeToolSystem;

        public bool IsActive => m_ActiveBinding.value;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_TransitScopeToolSystem = World.GetOrCreateSystemManaged<TransitScopeToolSystem>();

            AddBinding(m_ActiveBinding = new ValueBinding<bool>("transitScope", "isActive", false));
            AddBinding(m_HasStatsBinding = new ValueBinding<bool>("transitScope", "hasStats", false));
            AddBinding(m_StatsJsonBinding = new ValueBinding<string>("transitScope", "statsJson", string.Empty));

            AddBinding(new TriggerBinding<bool>("transitScope", "toggle", OnToggleMode));
            AddBinding(new TriggerBinding("transitScope", "confirm", OnConfirmSelection));

            SyncBindings();
            Logger.Info("TransitScopeUISystem 已启动");
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            SyncBindings();
        }

        private void OnToggleMode(bool active)
        {
            if (m_ActiveBinding.value == active)
            {
                return;
            }

            m_ActiveBinding.Update(active);

            if (active)
            {
                m_TransitScopeToolSystem.EnableSelectionMode();
            }
            else
            {
                m_TransitScopeToolSystem.DisableSelectionMode();
                ClearStats();
            }

            SyncBindings();
        }

        private void OnConfirmSelection()
        {
            if (!m_ActiveBinding.value)
            {
                return;
            }

            m_TransitScopeToolSystem.ConfirmHoveredTarget();
            SyncBindings();
        }

        private void SyncBindings()
        {
            if (m_TransitScopeToolSystem == null)
            {
                return;
            }

            m_ActiveBinding.Update(m_TransitScopeToolSystem.IsSelecting);

            // 当用户取消当前选中对象时，图表也要一起清空。
            if (m_TransitScopeToolSystem.SelectedEntity == Entity.Null && m_HasStatsBinding.value)
            {
                ClearStats();
            }
        }

        /// <summary>
        /// 向前端推送新的统计卡片数据。
        /// 这里绑定 JSON 字符串，避免复杂对象绑定在不同版本里出现兼容问题。
        /// </summary>
        internal void PresentStats(TransitScopeSelectionStats stats)
        {
            if (stats == null)
            {
                ClearStats();
                return;
            }

            m_HasStatsBinding.Update(true);
            m_StatsJsonBinding.Update(stats.ToJson());
        }

        /// <summary>
        /// 清空当前统计展示。
        /// </summary>
        internal void ClearStats()
        {
            m_HasStatsBinding.Update(false);
            m_StatsJsonBinding.Update(string.Empty);
        }
    }
}
