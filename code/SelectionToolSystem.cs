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
    /// <summary>
    /// Transit Scope 自定义选择工具。
    /// 
    /// 该工具只做一件事：把玩家当前 Hover / Confirm 的对象
    /// 统一解析成“道路边”或“建筑”这两类业务对象，并把选择状态暴露给其他系统。
    /// </summary>
    public partial class SelectionToolSystem : ToolBaseSystem
    {
        //状态：是否在选择模式
        public enum State
        {
            Default,
            Selecting
        }

        //命中对象类型
        public enum SelectionKind
        {
            None = 0,
            Road = 1,
            Building = 2
        }

        //原版工具系统
        private ToolSystem m_GameToolSystem;
        private State m_State = State.Default;

        //悬停对象
        public Entity HoveredEntity { get; private set; } = Entity.Null;
        public SelectionKind HoveredKind { get; private set; } = SelectionKind.None;

        //选择对象
        public Entity SelectedEntity { get; private set; } = Entity.Null;
        public SelectionKind SelectedKind { get; private set; } = SelectionKind.None;

        /// <summary>
        /// 用于通知统计系统“这次选择刚刚发生变化，需要立即刷新”。
        /// </summary>
        public bool HasNewSelection { get; private set; }

        //公开的便捷属性提供状态
        public bool IsSelecting => m_State == State.Selecting;

        /// <summary>
        /// 当 Hover 对象和已确认对象一致时，不再重复绘制 Hover 高亮。
        /// </summary>
        public bool HasConfirmedHoveredTarget =>
            SelectedEntity != Entity.Null &&
            SelectedEntity == HoveredEntity &&
            SelectedKind == HoveredKind;

        //悬停高亮的总开关
        public bool ShouldRenderHoverOverlay =>
            IsSelecting &&
            HoveredEntity != Entity.Null &&
            HoveredKind != SelectionKind.None &&
            !HasConfirmedHoveredTarget;

        public override string toolID => "ScopeTool";

        protected override void OnCreate()
        {
            base.OnCreate();

            //创建时默认处于关闭状态
            m_GameToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            Enabled = false;
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();

            m_State = State.Selecting;
            ResetState();
            UpdateNativeSelectionMarker();
            Logger.Info("选择模式已开启。");
        }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();

            m_State = State.Default;
            ResetState();
            UpdateNativeSelectionMarker();
            Logger.Info("选择模式已关闭。");
        }

        //强制要求实现的接口，本工具不进行修改
        public override PrefabBase GetPrefab()
        {
            return null;
        }

        public override bool TrySetPrefab(PrefabBase prefab)
        {
            return false;
        }

        public void EnableSelectionMode()
        {
            if (m_GameToolSystem == null)
            {
                return;
            }

            ResetState();

            //从别的工具切换过来时清楚状态
            if (m_GameToolSystem.activeTool != this)
            {
                m_GameToolSystem.selected = Entity.Null;
                m_GameToolSystem.activeTool = this;
            }

            UpdateNativeSelectionMarker();
        }

        public void DisableSelectionMode()
        {
            if (m_GameToolSystem == null)
            {
                return;
            }

            ResetState();

            if (m_GameToolSystem.activeTool == this)
            {
                m_GameToolSystem.selected = Entity.Null;
                m_GameToolSystem.activeTool = m_DefaultToolSystem;
            }

            UpdateNativeSelectionMarker();
        }

        /// <summary>
        /// 确认当前 Hover 对象。
        /// 如果再次点击同一对象，则视为取消选择。
        /// </summary>
        public void ConfirmHoveredTarget()
        {
            //防止选中过程中实体失效
            if (!IsValidSelection(HoveredEntity, HoveredKind))
            {
                return;
            }

            //再次点击取消选择
            if (SelectedEntity != Entity.Null &&
                SelectedEntity == HoveredEntity &&
                SelectedKind == HoveredKind)
            {
                ClearSelectionInternal();
                Logger.Info("已取消当前锁定对象。");
                return;
            }

            //更新选择
            SelectedEntity = HoveredEntity;
            SelectedKind = HoveredKind;
            HasNewSelection = true;

            UpdateNativeSelectionMarker();

            if (SelectedKind == SelectionKind.Road)
            {
                Logger.Info($"已锁定道路或轨道 #{SelectedEntity.Index}");
            }
            else if (SelectedKind == SelectionKind.Building)
            {
                Logger.Info($"已锁定建筑 #{SelectedEntity.Index}");
            }
        }

        public void ClearSelection()
        {
            ClearSelectionInternal();
        }

        //给后端消费选择对象
        public void ClearNewSelectionFlag()
        {
            HasNewSelection = false;
        }

        //框架接入部分
        public override void InitializeRaycast()
        {
            base.InitializeRaycast();

            //允许命中地上高架地下（地下功能待实现）
            m_ToolRaycastSystem.collisionMask =
                CollisionMask.OnGround | CollisionMask.Overground | CollisionMask.Underground;

            // 同时允许命中道路网络和静态物体，满足道路/建筑混合选择需求。
            m_ToolRaycastSystem.typeMask = TypeMask.Net | TypeMask.StaticObjects;

            //允许命中子物体
            m_ToolRaycastSystem.raycastFlags =
                RaycastFlags.SubElements |
                RaycastFlags.Cargo |
                RaycastFlags.Passenger |
                RaycastFlags.EditorContainers;

            // 补充轨道相关层，保证铁路、地铁、有轨电车都可以被命中。
            m_ToolRaycastSystem.netLayerMask =
                Layer.Road |
                Layer.PublicTransportRoad |
                Layer.Pathway |
                Layer.TrainTrack |
                Layer.SubwayTrack |
                Layer.TramTrack;

            //不让 UI 图标层干扰命中
            m_ToolRaycastSystem.iconLayerMask = IconLayerMask.None;
            //不让公用设施类型过滤参与
            m_ToolRaycastSystem.utilityTypeMask = UtilityTypes.None;
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            //焦点变化不处理(切换窗口等)
            if (m_FocusChanged)
            {
                return inputDeps;
            }

            //只有自己是激活工具才进行处理
            if (m_GameToolSystem == null || m_GameToolSystem.activeTool != this)
            {
                return inputDeps;
            }

            HasNewSelection = false;
            //更新悬停对象，支持预览功能
            UpdateHoveredTarget();

            if (SelectedEntity != Entity.Null && !EntityManager.Exists(SelectedEntity))
            {
                ClearSelectionInternal();
            }

            //每帧都把选中对象同步为工具的选中对象
            UpdateNativeSelectionMarker();

            if (CanAcceptSceneClick() && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                ConfirmHoveredTarget();
            }

            //右键清楚
            if (CanAcceptSceneClick() && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                ClearSelectionInternal();
            }

            return inputDeps;
        }

        private bool CanAcceptSceneClick()
        {
            if (m_State != State.Selecting)
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
        /// 只把已经确认的对象写回原版 selected 标记。
        /// Hover 对象不应污染原版选中状态。
        /// </summary>
        private void UpdateNativeSelectionMarker()
        {
            if (m_GameToolSystem == null)
            {
                return;
            }

            m_GameToolSystem.selected = SelectedEntity;
        }

        private void ResetState()
        {
            HoveredEntity = Entity.Null;
            HoveredKind = SelectionKind.None;
            SelectedEntity = Entity.Null;
            SelectedKind = SelectionKind.None;
            HasNewSelection = false;
        }

        private void ClearSelectionInternal()
        {
            ResetState();
            UpdateNativeSelectionMarker();
        }

        /// <summary>
        /// 每帧更新 Hover 对象。
        /// 优先解析道路，解析失败后再尝试解析建筑。
        /// </summary>
        private void UpdateHoveredTarget()
        {
            HoveredEntity = Entity.Null;
            HoveredKind = SelectionKind.None;

            //从工具射线系统获取命中实体
            if (!GetRaycastResult(out Entity hitEntity, out _))
            {
                return;
            }

            //先认为命中道路并在命中子组件时还原为大道路
            Entity roadEntity = EntityResolver.ResolveRoadEdge(EntityManager, hitEntity);
            if (EntityResolver.IsRoad(EntityManager, roadEntity))
            {
                HoveredEntity = roadEntity;
                HoveredKind = SelectionKind.Road;
                return;
            }

            //不是道路则尝试解析建筑
            Entity buildingEntity = EntityResolver.ResolveBuildingEntity(EntityManager, hitEntity);
            if (EntityResolver.IsBuilding(EntityManager, buildingEntity))
            {
                HoveredEntity = buildingEntity;
                HoveredKind = SelectionKind.Building;
            }
        }

        //脏态防御
        private bool IsValidSelection(Entity entity, SelectionKind kind)
        {
            return kind switch
            {
                SelectionKind.Road => EntityResolver.IsRoad(EntityManager, entity),
                SelectionKind.Building => EntityResolver.IsBuilding(EntityManager, entity),
                _ => false
            };
        }
    }
}
