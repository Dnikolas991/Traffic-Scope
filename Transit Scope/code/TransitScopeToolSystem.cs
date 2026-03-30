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
        // 工具状态
        public enum State
        {
            Default,
            SelectingRoad
        }

        private ToolSystem m_ToolSystem;
        private State m_State = State.Default;

        // 鼠标当前悬停到的道路
        public Entity HoveredEdge { get; private set; } = Entity.Null;

        // 用户最后一次确认选择的道路
        public Entity SelectedEdge { get; private set; } = Entity.Null;

        // 当前帧是否刚产生一次新的选择
        public bool HasNewSelection { get; private set; }

        public bool IsSelecting => m_State == State.SelectingRoad;

        public override string toolID => "TransitScopeTool";

        protected override void OnCreate()
        {
            base.OnCreate();

            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();

            // 参考官方/成熟工具做法：
            // 工具默认不主动运行，只有被切成 activeTool 时才进入运行态
            Enabled = false;

            Logger.Info("TransitScopeToolSystem 初始化完成");
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();

            m_State = State.SelectingRoad;
            HoveredEdge = Entity.Null;
            SelectedEdge = Entity.Null;
            HasNewSelection = false;

            Logger.Info("TransitScopeTool 已进入道路选择模式");
        }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();

            m_State = State.Default;
            HoveredEdge = Entity.Null;
            HasNewSelection = false;

            Logger.Info("TransitScopeTool 已退出道路选择模式");
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
        /// 由 UI 打开工具。
        /// 这里只负责切 activeTool，不在这里做选择逻辑。
        /// </summary>
        public void EnableSelectionMode()
        {
            if (m_ToolSystem == null)
            {
                Logger.Error("EnableSelectionMode 失败：ToolSystem 未初始化");
                return;
            }

            HoveredEdge = Entity.Null;
            SelectedEdge = Entity.Null;
            HasNewSelection = false;

            Logger.Info($"[Before Enable] activeTool = {m_ToolSystem.activeTool?.GetType().Name ?? "null"}");

            if (m_ToolSystem.activeTool != this)
            {
                m_ToolSystem.selected = Entity.Null;
                m_ToolSystem.activeTool = this;
            }

            Logger.Info($"[After Enable] activeTool = {m_ToolSystem.activeTool?.GetType().Name ?? "null"}");
        }

        /// <summary>
        /// 由 UI 关闭工具。
        /// </summary>
        public void DisableSelectionMode()
        {
            if (m_ToolSystem == null)
            {
                Logger.Error("DisableSelectionMode 失败：ToolSystem 未初始化");
                return;
            }

            HoveredEdge = Entity.Null;
            HasNewSelection = false;

            Logger.Info($"[Before Disable] activeTool = {m_ToolSystem.activeTool?.GetType().Name ?? "null"}");

            if (m_ToolSystem.activeTool == this)
            {
                m_ToolSystem.selected = Entity.Null;
                m_ToolSystem.activeTool = m_DefaultToolSystem;
            }

            Logger.Info($"[After Disable] activeTool = {m_ToolSystem.activeTool?.GetType().Name ?? "null"}");
        }

        /// <summary>
        /// 确认当前悬停道路。
        /// 这个方法既可以被 UI 调试按钮调用，也可以被工具内部的左键点击调用。
        /// </summary>
        public void ConfirmHoveredRoad()
        {
            if (HoveredEdge == Entity.Null)
            {
                Logger.Info("ConfirmHoveredRoad：当前没有悬停道路");
                return;
            }

            if (!EntityManager.Exists(HoveredEdge))
            {
                Logger.Info("ConfirmHoveredRoad：HoveredEdge 已不存在");
                return;
            }

            if (!EntityManager.HasComponent<Edge>(HoveredEdge))
            {
                Logger.Info("ConfirmHoveredRoad：HoveredEdge 不是 Edge");
                return;
            }

            SelectedEdge = HoveredEdge;
            HasNewSelection = true;

            Logger.Info($"确认选择道路 Edge: {SelectedEdge.Index}");
        }

        public void ClearNewSelectionFlag()
        {
            HasNewSelection = false;
        }

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();

            // 当前阶段只做“道路选择”，因此先保持最小可用的 Net 射线配置
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

            // 每帧先清空“本帧新选择”标记。
            // 真正确认时会在 ConfirmHoveredRoad() 里重新置为 true。
            HasNewSelection = false;

            Entity oldHovered = HoveredEdge;
            HoveredEdge = TryGetHoveredRoadEdge();

            if (oldHovered != HoveredEdge)
            {
                Logger.Info($"HoveredEdge changed: {oldHovered.Index} -> {HoveredEdge.Index}");
            }

            // 关键修复点：
            // 这里把“游戏场景里的左键点击”接回工具内部，
            // 不再只依赖 UI 的 confirm 按钮。
            //
            // 当前项目里没有看到 ModSettings/ApplyTool 的现成定义，
            // 所以先用 Unity InputSystem 做一层最小桥接。
            // 等后面你把项目自己的键位系统补齐，再替换成 ProxyAction 即可。
            if (CanAcceptSceneClick() && IsPrimaryMousePressedThisFrame())
            {
                ConfirmHoveredRoad();
            }

            return inputDeps;
        }

        /// <summary>
        /// 判断当前这一帧是否允许接受场景点击。
        /// 如果 UI 正在占用射线，或者工具当前不在选择状态，就不要处理点击。
        /// </summary>
        private bool CanAcceptSceneClick()
        {
            if (m_State != State.SelectingRoad)
            {
                return false;
            }

            // 参考成熟工具的判断方式：
            // 当 raycast 被 UI 或 debug 禁掉时，不处理 apply/click。
            if ((m_ToolRaycastSystem.raycastFlags & (RaycastFlags.DebugDisable | RaycastFlags.UIDisable)) != 0)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 当前先使用 Unity InputSystem 的鼠标左键作为“确认选择”。
        /// 这是一个不依赖额外配置文件的最小可用方案。
        /// </summary>
        private bool IsPrimaryMousePressedThisFrame()
        {
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        }

        private Entity TryGetHoveredRoadEdge()
        {
            if (!GetRaycastResult(out Entity hitEntity, out _))
            {
                Logger.Info("TryGetHoveredRoadEdge: GetRaycastResult = false");
                return Entity.Null;
            }

            Logger.Info($"TryGetHoveredRoadEdge: hitEntity = {hitEntity.Index}");

            Entity edgeEntity = ResolveRoadEdge(hitEntity);

            Logger.Info($"TryGetHoveredRoadEdge: resolvedEdge = {edgeEntity.Index}");

            if (edgeEntity == Entity.Null)
            {
                return Entity.Null;
            }

            if (!EntityManager.Exists(edgeEntity))
            {
                Logger.Info("TryGetHoveredRoadEdge: resolved edge 不存在");
                return Entity.Null;
            }

            if (!EntityManager.HasComponent<Edge>(edgeEntity))
            {
                Logger.Info("TryGetHoveredRoadEdge: resolved entity 不是 Edge");
                return Entity.Null;
            }

            return edgeEntity;
        }

        private Entity ResolveRoadEdge(Entity entity)
        {
            if (entity == Entity.Null)
            {
                Logger.Info("ResolveRoadEdge: entity = Null");
                return Entity.Null;
            }

            if (!EntityManager.Exists(entity))
            {
                Logger.Info($"ResolveRoadEdge: entity {entity.Index} 不存在");
                return Entity.Null;
            }

            Logger.Info($"ResolveRoadEdge: inspecting entity {entity.Index}");

            // 1）直接命中道路 Edge
            if (EntityManager.HasComponent<Edge>(entity))
            {
                Logger.Info("ResolveRoadEdge: 直接命中 Edge");
                return entity;
            }

            // 2）命中子实体，通过 Owner 向上回溯
            if (EntityManager.HasComponent<Owner>(entity))
            {
                Owner owner = EntityManager.GetComponentData<Owner>(entity);
                Logger.Info($"ResolveRoadEdge: 命中 Owner，owner = {owner.m_Owner.Index}");

                if (owner.m_Owner != Entity.Null && EntityManager.Exists(owner.m_Owner))
                {
                    if (EntityManager.HasComponent<Edge>(owner.m_Owner))
                    {
                        Logger.Info("ResolveRoadEdge: 通过 Owner 找到 Edge");
                        return owner.m_Owner;
                    }
                }
            }

            // 3）命中 Temp 时，回溯 original
            if (EntityManager.HasComponent<Temp>(entity))
            {
                Temp temp = EntityManager.GetComponentData<Temp>(entity);
                Logger.Info($"ResolveRoadEdge: 命中 Temp，original = {temp.m_Original.Index}");

                if (temp.m_Original != Entity.Null && EntityManager.Exists(temp.m_Original))
                {
                    if (EntityManager.HasComponent<Edge>(temp.m_Original))
                    {
                        Logger.Info("ResolveRoadEdge: 通过 Temp.original 找到 Edge");
                        return temp.m_Original;
                    }

                    if (EntityManager.HasComponent<Owner>(temp.m_Original))
                    {
                        Owner owner = EntityManager.GetComponentData<Owner>(temp.m_Original);
                        Logger.Info($"ResolveRoadEdge: Temp.original 的 Owner = {owner.m_Owner.Index}");

                        if (owner.m_Owner != Entity.Null && EntityManager.Exists(owner.m_Owner))
                        {
                            if (EntityManager.HasComponent<Edge>(owner.m_Owner))
                            {
                                Logger.Info("ResolveRoadEdge: 通过 Temp.original.Owner 找到 Edge");
                                return owner.m_Owner;
                            }
                        }
                    }
                }
            }

            Logger.Info("ResolveRoadEdge: 最终未解析到 Edge");
            return Entity.Null;
        }
    }
}