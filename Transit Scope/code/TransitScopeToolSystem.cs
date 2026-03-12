using Game.Prefabs;
using Game.Tools;
using Unity.Entities;
using Unity.Jobs;

namespace Transit_Scope
{
    /// <summary>
    /// Transit Scope 的选择工具
    /// 目前先实现：
    /// 1. 能被 ToolSystem 激活
    /// 2. 能进入“选择模式”
    /// 3. 读取当前 selected 实体
    /// 后续再补道路过滤、左键确认、高亮等功能
    /// </summary>
    public partial class TransitScopeToolSystem : ToolBaseSystem
    {
        private ToolSystem m_ToolSystem;

        public bool IsSelecting { get; private set; }

        /// <summary>
        /// 当前已确认/读取到的实体
        /// 后续可改成只接受 Edge
        /// </summary>
        public Entity SelectedEntity { get; private set; } = Entity.Null;

        /// <summary>
        /// ToolBaseSystem 要求实现的工具 ID
        /// 字符串保持稳定即可
        /// </summary>
        public override string toolID => "TransitScopeTool";

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();

            Mod.Log.Info("TransitScopeToolSystem 已创建");
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            IsSelecting = true;
            Mod.Log.Info("Transit Scope 已进入选择模式");
        }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();
            IsSelecting = false;
            Mod.Log.Info("Transit Scope 已退出选择模式");
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (m_ToolSystem == null)
                return inputDeps;

            if (m_ToolSystem.activeTool != this)
                return inputDeps;

            Entity current = m_ToolSystem.selected;

            if (current == Entity.Null)
                return inputDeps;

            if (!EntityManager.Exists(current))
                return inputDeps;

            if (current == SelectedEntity)
                return inputDeps;

            SelectedEntity = current;
            Mod.Log.Info($"Transit Scope 选中实体: {SelectedEntity.Index}");

            return inputDeps;
        }

        /// <summary>
        /// ToolBaseSystem 抽象成员：返回当前 prefab
        /// 这个工具当前不依赖 prefab，所以返回 null
        /// </summary>
        public override PrefabBase GetPrefab()
        {
            return null;
        }

        /// <summary>
        /// ToolBaseSystem 抽象成员：尝试设置 prefab
        /// 当前工具不处理 prefab，直接拒绝即可
        /// </summary>
        public override bool TrySetPrefab(PrefabBase prefab)
        {
            return false;
        }

        public void EnableSelectionMode()
        {
            if (m_ToolSystem == null)
                return;

            SelectedEntity = Entity.Null;

            if (m_ToolSystem.activeTool != this)
            {
                m_ToolSystem.activeTool = this;
            }
        }

        public void DisableSelectionMode()
        {
            if (m_ToolSystem == null)
                return;

            if (m_ToolSystem.activeTool == this)
            {
                SelectedEntity = Entity.Null;

                // 这里先简单清掉当前工具
                // 如果你项目里这里报错，再改成切回 DefaultToolSystem
                m_ToolSystem.activeTool = null;
            }
        }
    }
}