using Colossal.UI.Binding;
using Game.UI;

namespace Transit_Scope.code
{
    public partial class TransitScopeUISystem : UISystemBase
    {
        private ValueBinding<bool> m_ActiveBinding;
        private ValueBinding<int> m_HoveredEdgeBinding;
        private ValueBinding<int> m_SelectedEdgeBinding;
        private ValueBinding<string> m_StatusTextBinding;

        private TransitScopeToolSystem m_TransitScopeToolSystem;

        public bool IsActive => m_ActiveBinding.value;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_TransitScopeToolSystem = World.GetOrCreateSystemManaged<TransitScopeToolSystem>();

            AddBinding(m_ActiveBinding = new ValueBinding<bool>("transitScope", "isActive", false));
            AddBinding(m_HoveredEdgeBinding = new ValueBinding<int>("transitScope", "hoveredEdgeId", -1));
            AddBinding(m_SelectedEdgeBinding = new ValueBinding<int>("transitScope", "selectedEdgeId", -1));
            AddBinding(m_StatusTextBinding = new ValueBinding<string>("transitScope", "statusText", "未激活"));

            AddBinding(new TriggerBinding<bool>("transitScope", "toggle", OnToggleMode));
            AddBinding(new TriggerBinding("transitScope", "confirm", OnConfirmSelection));

            UpdateBindingsSnapshot();
            Mod.Log.Info("TransitScopeUISystem 已启动");
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            UpdateBindingsSnapshot();
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
            }

            UpdateBindingsSnapshot();
        }

        private void OnConfirmSelection()
        {
            if (!m_ActiveBinding.value)
            {
                return;
            }

            // 这里保留成备用入口：
            // 正常游戏内操作还是场景左键确认
            m_TransitScopeToolSystem.ConfirmHoveredRoad();
            UpdateBindingsSnapshot();
        }

        /// <summary>
        /// 每帧把后端工具状态同步到 UI
        /// </summary>
        private void UpdateBindingsSnapshot()
        {
            if (m_TransitScopeToolSystem == null)
            {
                return;
            }

            m_HoveredEdgeBinding.Update(m_TransitScopeToolSystem.HoveredEdgeId);
            m_SelectedEdgeBinding.Update(m_TransitScopeToolSystem.SelectedEdgeId);
            m_StatusTextBinding.Update(m_TransitScopeToolSystem.StatusText);
        }
    }
}