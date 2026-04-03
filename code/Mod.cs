using Game;
using Game.Modding;
using Game.SceneFlow;

namespace Transit_Scope.code
{
    public class Mod : IMod
    {
        public void OnLoad(UpdateSystem updateSystem)
        {
            Logger.Info("Transit Scope 启动");

            //选择模块是工具类
            updateSystem.UpdateAt<SelectionToolSystem>(SystemUpdatePhase.ToolUpdate);
            //绑定前端和后端，是ui基类
            updateSystem.UpdateAt<UIBridgeSystem>(SystemUpdatePhase.UIUpdate);
            //主控逻辑注册为基本的通用类
            updateSystem.UpdateAt<SelectionSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<OverlaySystem>(SystemUpdatePhase.UIUpdate);

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
            {
                Logger.Info($"Current mod asset at {asset.path}");
                Logger.Info("Transit Scope 完成注册");
            }
        }

        public void OnDispose()
        {
            Logger.Info("Transit Scope 关闭");
        }
    }
}
