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

            updateSystem.UpdateAt<ScopeToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<ScopeUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<ScopeSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<ScopeOverlaySystem>(SystemUpdatePhase.UIUpdate);

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
            {
                Logger.Info($"Current mod asset at {asset.path}");
                Logger.Info("Transit Scope 后端系统已就绪，前端 UI 模块已交由官方 Toolchain 自动接管。");
            }
        }

        public void OnDispose()
        {
            Logger.Info(nameof(OnDispose));
        }
    }
}
