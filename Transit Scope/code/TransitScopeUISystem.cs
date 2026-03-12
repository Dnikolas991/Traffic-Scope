using Colossal.UI.Binding;
using Game.UI;

namespace Transit_Scope
{
    public partial class TransitScopeUISystem : UISystemBase
    {
        // 声明一个值绑定，用于向前端实时同步状态
        private ValueBinding<bool> m_ActiveBinding;
        
        private TransitScopeToolSystem m_TransitScopeToolSystem;
        
        // 供 ECS 流量系统读取的公共属性
        public bool IsActive => m_ActiveBinding.value;

        protected override void OnCreate()
        {
            base.OnCreate();
            
            m_TransitScopeToolSystem = World.GetOrCreateSystemManaged<TransitScopeToolSystem>();
            
            // 1. 注册值绑定：前端可以通过 useValue 监听这个值
            AddBinding(m_ActiveBinding = new ValueBinding<bool>("transitScope", "isActive", false));
            
            // 2. 注册触发器：接收前端发来的 toggle 指令
            AddBinding(new TriggerBinding<bool>("transitScope", "toggle", OnToggleMode));
            
            Mod.Log.Info("TransitScopeUISystem 已启动并完成数据绑定注册！");
        }

        private void OnToggleMode(bool active)
        {
            // 更新绑定值。调用 Update 后，游戏会自动通知所有监听此值的前端 React 组件重新渲染
            m_ActiveBinding.Update(active);
            if (active)
            {
                m_TransitScopeToolSystem.EnableSelectionMode();
                Mod.Log.Info("UI指令：已开启 Transit Scope 选择模式");
            }
            else
            {
                m_TransitScopeToolSystem.DisableSelectionMode();
                Mod.Log.Info("UI指令：已关闭 Transit Scope 选择模式");
            }
        }
    }
}