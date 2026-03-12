using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
// 注意：移除了所有与 Colossal.UI 和 System.IO 相关的冗余引用

namespace Transit_Scope
{
    public class Mod : IMod
    {
        public static ILog Log = LogManager.GetLogger($"{nameof(Transit_Scope)}.{nameof(Mod)}").SetShowsErrorsInUI(false);

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info(nameof(OnLoad));
            
            updateSystem.UpdateAt<TransitScopeToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<TransitScopeUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<TransitScopeSystem>(SystemUpdatePhase.UIUpdate);

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
            {
                Log.Info($"Current mod asset at {asset.path}");
                Log.Info("Transit Scope 后端系统已就绪，前端 UI 模块已交由官方 Toolchain 自动接管！");
            }
        }

        public void OnDispose()
        {
            Log.Info(nameof(OnDispose));
        }
    }
}