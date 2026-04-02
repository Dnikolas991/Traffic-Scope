using Colossal.UI.Binding;
using Game.UI;
using Unity.Entities;

namespace Transit_Scope.code
{
    /// <summary>
    /// 管理后端与前端之间的绑定桥接。
    /// 
    /// 当前承担的职责非常明确：
    /// 1. 控制 Transit Scope 模式是否开启。
    /// 2. 将最新统计面板数据推送给前端。
    /// 3. 在选择取消时及时清空前端显示状态。
    /// </summary>
    public partial class ScopeUISystem : UISystemBase
    {
        private ValueBinding<bool> m_ActiveBinding;
        private ValueBinding<bool> m_HasStatsBinding;
        private ValueBinding<string> m_StatsJsonBinding;

        private ScopeToolSystem m_ScopeToolSystem;

        public bool IsActive => m_ActiveBinding.value;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_ScopeToolSystem = World.GetOrCreateSystemManaged<ScopeToolSystem>();

            AddBinding(m_ActiveBinding = new ValueBinding<bool>("transitScope", "isActive", false));
            AddBinding(m_HasStatsBinding = new ValueBinding<bool>("transitScope", "hasStats", false));
            AddBinding(m_StatsJsonBinding = new ValueBinding<string>("transitScope", "statsJson", string.Empty));

            AddBinding(new TriggerBinding<bool>("transitScope", "toggle", OnToggleMode));
            AddBinding(new TriggerBinding("transitScope", "confirm", OnConfirmSelection));

            SyncBindings();
            Logger.Info("ScopeUISystem 已启动，前后端绑定已建立。");
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
                m_ScopeToolSystem.EnableSelectionMode();
            }
            else
            {
                m_ScopeToolSystem.DisableSelectionMode();
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

            m_ScopeToolSystem.ConfirmHoveredTarget();
            SyncBindings();
        }

        /// <summary>
        /// 同步激活状态，并在没有选中对象时自动清空统计面板。
        /// </summary>
        private void SyncBindings()
        {
            if (m_ScopeToolSystem == null)
            {
                return;
            }

            m_ActiveBinding.Update(m_ScopeToolSystem.IsSelecting);

            if (m_ScopeToolSystem.SelectedEntity == Entity.Null && m_HasStatsBinding.value)
            {
                ClearStats();
            }
        }

        /// <summary>
        /// 向前端推送新的统计卡片数据。
        /// </summary>
        internal void PresentStats(ScopeSelectionStats stats)
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
        /// 清空当前统计显示。
        /// </summary>
        internal void ClearStats()
        {
            m_HasStatsBinding.Update(false);
            m_StatsJsonBinding.Update(string.Empty);
        }
    }
}
