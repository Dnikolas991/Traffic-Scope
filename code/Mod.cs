using Game;
using Game.Modding;
using Game.SceneFlow;


namespace Transit_Scope.code
{
    public class Mod : IMod
    {

        public void OnLoad(UpdateSystem updateSystem)
        {
            Logger.Info(nameof(OnLoad));
            
            //注册相关组件
            updateSystem.UpdateAt<TransitScopeToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<TransitScopeUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<TransitScopeSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<TransitScopeOverlaySystem>(SystemUpdatePhase.UIUpdate);

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
            {
                Logger.Info($"Current mod asset at {asset.path}");
                Logger.Info("Transit Scope 后端系统已就绪，前端 UI 模块已交由官方 Toolchain 自动接管！");
            }
        }

        public void OnDispose()
        {
            Logger.Info(nameof(OnDispose));
        }
    }
}
