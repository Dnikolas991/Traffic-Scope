using Colossal.UI.Binding;
using Game.UI;

namespace Transit_Scope.code
{
    public partial class TransitScopeUISystem : UISystemBase
    {
        private ValueBinding<bool> m_ActiveBinding;
        private TransitScopeToolSystem m_TransitScopeToolSystem;

        public bool IsActive => m_ActiveBinding.value;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_TransitScopeToolSystem = World.GetOrCreateSystemManaged<TransitScopeToolSystem>();

            AddBinding(m_ActiveBinding = new ValueBinding<bool>("transitScope", "isActive", false));
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
        }
    }
}
