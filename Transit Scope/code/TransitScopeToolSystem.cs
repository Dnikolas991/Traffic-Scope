using Game.Common;
using Game.Net;
using Game.Notifications;
using Game.Prefabs;
using Game.Tools;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine.InputSystem;

namespace Transit_Scope.code
{
    public partial class TransitScopeToolSystem : ToolBaseSystem
    {
        /// <summary>
        /// 工具状态：
        /// Default        - 未运行
        /// SelectingRoad  - 已进入道路选择模式
        /// </summary>
        public enum State
        {
            Default,
            SelectingRoad
        }

        private ToolSystem m_ToolSystem;
        private State m_State = State.Default;

        /// <summary>
        /// 鼠标当前悬停到的道路
        /// </summary>
        public Entity HoveredEdge { get; private set; } = Entity.Null;

        /// <summary>
        /// 当前已锁定的道路
        /// </summary>
        public Entity SelectedEdge { get; private set; } = Entity.Null;

        /// <summary>
        /// 当前帧是否产生了新的“确认选择事件”
        /// TransitScopeSystem 只消费这个事件，不参与工具状态机
        /// </summary>
        public bool HasNewSelection { get; private set; }

        public bool IsSelecting => m_State == State.SelectingRoad;
        public bool HasLockedSelection => SelectedEdge != Entity.Null;

        public int HoveredEdgeId => HoveredEdge != Entity.Null ? HoveredEdge.Index : -1;
        public int SelectedEdgeId => SelectedEdge != Entity.Null ? SelectedEdge.Index : -1;

        /// <summary>
        /// 给 UI 用的状态文字
        /// </summary>
        public string StatusText
        {
            get
            {
                if (!IsSelecting)
                {
                    return "未激活";
                }

                if (SelectedEdge != Entity.Null)
                {
                    return $"已锁定道路 #{SelectedEdge.Index}";
                }

                if (HoveredEdge != Entity.Null)
                {
                    return $"悬停道路 #{HoveredEdge.Index}";
                }

                return "请将鼠标移动到道路上";
            }
        }

        public override string toolID => "TransitScopeTool";

        protected override void OnCreate()
        {
            base.OnCreate();

            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();

            // 工具默认关闭，只在 activeTool 切换到自己时运行
            Enabled = false;
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();

            m_State = State.SelectingRoad;
            HoveredEdge = Entity.Null;
            SelectedEdge = Entity.Null;
            HasNewSelection = false;

            RefreshVisualSelection();
            Logger.Info("TransitScopeTool 已开启");
        }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();

            m_State = State.Default;
            HoveredEdge = Entity.Null;
            SelectedEdge = Entity.Null;
            HasNewSelection = false;

            RefreshVisualSelection();
            Logger.Info("TransitScopeTool 已关闭");
        }

        public override PrefabBase GetPrefab()
        {
            return null;
        }

        public override bool TrySetPrefab(PrefabBase prefab)
        {
            return false;
        }

        /// <summary>
        /// 由 UI 调用，激活工具
        /// </summary>
        public void EnableSelectionMode()
        {
            if (m_ToolSystem == null)
            {
                return;
            }

            HoveredEdge = Entity.Null;
            SelectedEdge = Entity.Null;
            HasNewSelection = false;

            if (m_ToolSystem.activeTool != this)
            {
                m_ToolSystem.selected = Entity.Null;
                m_ToolSystem.activeTool = this;
            }

            RefreshVisualSelection();
        }

        /// <summary>
        /// 由 UI 调用，退出工具
        /// </summary>
        public void DisableSelectionMode()
        {
            if (m_ToolSystem == null)
            {
                return;
            }

            HoveredEdge = Entity.Null;
            SelectedEdge = Entity.Null;
            HasNewSelection = false;

            if (m_ToolSystem.activeTool == this)
            {
                m_ToolSystem.selected = Entity.Null;
                m_ToolSystem.activeTool = m_DefaultToolSystem;
            }

            RefreshVisualSelection();
        }

        /// <summary>
        /// 对外保留的“确认当前悬停道路”入口。
        /// UI confirm 和场景左键都走这里。
        /// 
        /// 逻辑：
        /// 1. 没悬停到道路 -> 不处理
        /// 2. 点到当前已锁定道路 -> 取消锁定
        /// 3. 点到另一条道路 -> 锁定新道路，并触发一次新选择事件
        /// </summary>
        public void ConfirmHoveredRoad()
        {
            if (!IsValidEdge(HoveredEdge))
            {
                return;
            }

            // 再次点击同一条已锁定道路：取消选择
            if (SelectedEdge != Entity.Null && SelectedEdge == HoveredEdge)
            {
                ClearSelectionInternal(emitSelectionEvent: false);
                Logger.Info("已取消道路锁定");
                return;
            }

            // 锁定当前悬停道路
            SelectedEdge = HoveredEdge;
            HasNewSelection = true;

            RefreshVisualSelection();
            Logger.Info($"已锁定道路 #{SelectedEdge.Index}");
        }

        /// <summary>
        /// 主系统消费完“新选择事件”后调用
        /// </summary>
        public void ClearNewSelectionFlag()
        {
            HasNewSelection = false;
        }

        /// <summary>
        /// 手动取消当前锁定
        /// 这里给 UI 或以后快捷键扩展预留
        /// </summary>
        public void ClearSelection()
        {
            ClearSelectionInternal(emitSelectionEvent: false);
        }

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();

            // 当前工具只做道路 Edge 选择，保持最小可用配置
            m_ToolRaycastSystem.collisionMask = CollisionMask.OnGround | CollisionMask.Overground | CollisionMask.Underground;
            m_ToolRaycastSystem.typeMask = TypeMask.Net;
            m_ToolRaycastSystem.raycastFlags = RaycastFlags.SubElements | RaycastFlags.Cargo | RaycastFlags.Passenger | RaycastFlags.EditorContainers;
            m_ToolRaycastSystem.netLayerMask = Layer.Road | Layer.PublicTransportRoad | Layer.Pathway;
            m_ToolRaycastSystem.iconLayerMask = IconLayerMask.None;
            m_ToolRaycastSystem.utilityTypeMask = UtilityTypes.None;
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (m_FocusChanged)
            {
                return inputDeps;
            }

            if (m_ToolSystem == null || m_ToolSystem.activeTool != this)
            {
                return inputDeps;
            }

            // 每帧先清空一次“新选择事件”
            // 真正确认选择时会重新置 true
            HasNewSelection = false;

            HoveredEdge = TryGetHoveredRoadEdge();

            // 刷新当前视觉选择：
            // 未锁定时 -> 高亮悬停道路
            // 已锁定时 -> 高亮已锁定道路
            RefreshVisualSelection();

            // 左键点击场景时确认/切换/取消
            if (CanAcceptSceneClick() && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                ConfirmHoveredRoad();
            }

            return inputDeps;
        }

        /// <summary>
        /// 当前这一帧是否允许处理场景点击
        /// </summary>
        private bool CanAcceptSceneClick()
        {
            if (m_State != State.SelectingRoad)
            {
                return false;
            }

            if ((m_ToolRaycastSystem.raycastFlags & (RaycastFlags.DebugDisable | RaycastFlags.UIDisable)) != 0)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 根据当前状态刷新工具可视选中对象
        /// 
        /// 规则：
        /// - 已锁定时，优先显示 SelectedEdge
        /// - 未锁定时，显示 HoveredEdge
        /// - 都没有时，清空高亮
        /// </summary>
        private void RefreshVisualSelection()
        {
            if (m_ToolSystem == null)
            {
                return;
            }

            Entity visualEntity = SelectedEdge != Entity.Null ? SelectedEdge : HoveredEdge;
            m_ToolSystem.selected = visualEntity;
        }

        /// <summary>
        /// 内部清空锁定状态
        /// </summary>
        private void ClearSelectionInternal(bool emitSelectionEvent)
        {
            SelectedEdge = Entity.Null;

            if (emitSelectionEvent)
            {
                HasNewSelection = true;
            }

            RefreshVisualSelection();
        }

        private Entity TryGetHoveredRoadEdge()
        {
            if (!GetRaycastResult(out Entity hitEntity, out _))
            {
                return Entity.Null;
            }

            Entity edgeEntity = ResolveRoadEdge(hitEntity);

            if (!IsValidEdge(edgeEntity))
            {
                return Entity.Null;
            }

            return edgeEntity;
        }

        private bool IsValidEdge(Entity entity)
        {
            if (entity == Entity.Null)
            {
                return false;
            }

            if (!EntityManager.Exists(entity))
            {
                return false;
            }

            if (!EntityManager.HasComponent<Edge>(entity))
            {
                return false;
            }

            return true;
        }

        private Entity ResolveRoadEdge(Entity entity)
        {
            if (entity == Entity.Null)
            {
                return Entity.Null;
            }

            if (!EntityManager.Exists(entity))
            {
                return Entity.Null;
            }

            // 1）直接命中道路 Edge
            if (EntityManager.HasComponent<Edge>(entity))
            {
                return entity;
            }

            // 2）命中子实体时，通过 Owner 向上找
            if (EntityManager.HasComponent<Owner>(entity))
            {
                Owner owner = EntityManager.GetComponentData<Owner>(entity);

                if (owner.m_Owner != Entity.Null && EntityManager.Exists(owner.m_Owner))
                {
                    if (EntityManager.HasComponent<Edge>(owner.m_Owner))
                    {
                        return owner.m_Owner;
                    }
                }
            }

            // 3）命中 Temp 时，回溯 original
            if (EntityManager.HasComponent<Temp>(entity))
            {
                Temp temp = EntityManager.GetComponentData<Temp>(entity);

                if (temp.m_Original != Entity.Null && EntityManager.Exists(temp.m_Original))
                {
                    if (EntityManager.HasComponent<Edge>(temp.m_Original))
                    {
                        return temp.m_Original;
                    }

                    if (EntityManager.HasComponent<Owner>(temp.m_Original))
                    {
                        Owner owner = EntityManager.GetComponentData<Owner>(temp.m_Original);

                        if (owner.m_Owner != Entity.Null && EntityManager.Exists(owner.m_Owner))
                        {
                            if (EntityManager.HasComponent<Edge>(owner.m_Owner))
                            {
                                return owner.m_Owner;
                            }
                        }
                    }
                }
            }

            return Entity.Null;
        }
    }
}