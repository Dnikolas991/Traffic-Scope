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

            // 保留这个绑定：
            // 1）前端仍然可以有一个“确认”按钮做调试
            // 2）但真正的场景点击确认，现在已经放回 TransitScopeToolSystem.OnUpdate()
            AddBinding(new TriggerBinding("transitScope", "confirm", OnConfirmSelection));

            Mod.Log.Info("TransitScopeUISystem 已启动并完成数据绑定注册");
        }

        private void OnToggleMode(bool active)
        {
            Mod.Log.Info($"UI toggle 收到: active = {active}, old = {m_ActiveBinding.value}");

            // 避免重复值导致无意义切换
            if (m_ActiveBinding.value == active)
            {
                Mod.Log.Info($"UI toggle 忽略重复值: {active}");
                return;
            }

            m_ActiveBinding.Update(active);

            if (active)
            {
                m_TransitScopeToolSystem.EnableSelectionMode();
                Mod.Log.Info("UI 指令：开启道路选择模式");
            }
            else
            {
                m_TransitScopeToolSystem.DisableSelectionMode();
                Mod.Log.Info("UI 指令：关闭道路选择模式");
            }
        }

        private void OnConfirmSelection()
        {
            if (!m_ActiveBinding.value)
            {
                Mod.Log.Info("UI confirm 被忽略：工具当前未激活");
                return;
            }

            // 这里只作为调试/备用入口保留。
            // 正常场景点击确认已经在 ToolSystem 内部处理。
            m_TransitScopeToolSystem.ConfirmHoveredRoad();
            Mod.Log.Info("UI 指令：确认当前悬停道路");
        }
    }
}