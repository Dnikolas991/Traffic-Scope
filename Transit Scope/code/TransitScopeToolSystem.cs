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
        public enum State
        {
            Default,
            Selecting
        }

        public enum SelectionKind
        {
            None = 0,
            Road = 1,
            Building = 2
        }

        private ToolSystem m_GameToolSystem;
        private State m_State = State.Default;

        public Entity HoveredEntity { get; private set; } = Entity.Null;
        public SelectionKind HoveredKind { get; private set; } = SelectionKind.None;

        public Entity SelectedEntity { get; private set; } = Entity.Null;
        public SelectionKind SelectedKind { get; private set; } = SelectionKind.None;

        public bool HasNewSelection { get; private set; }
        public bool IsSelecting => m_State == State.Selecting;
        public bool HasConfirmedHoveredTarget =>
            SelectedEntity != Entity.Null &&
            SelectedEntity == HoveredEntity &&
            SelectedKind == HoveredKind;
        public bool ShouldRenderHoverOverlay =>
            IsSelecting &&
            HoveredEntity != Entity.Null &&
            HoveredKind != SelectionKind.None &&
            !HasConfirmedHoveredTarget;

        public override string toolID => "TransitScopeTool";

        protected override void OnCreate()
        {
            base.OnCreate();

            m_GameToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            Enabled = false;
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();

            m_State = State.Selecting;
            ResetState();
            UpdateNativeSelectionMarker();
            Logger.Info("TransitScopeTool 已开启");
        }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();

            m_State = State.Default;
            ResetState();
            UpdateNativeSelectionMarker();
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

        public void EnableSelectionMode()
        {
            if (m_GameToolSystem == null)
            {
                return;
            }

            ResetState();

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

        public void ConfirmHoveredTarget()
        {
            if (!IsValidSelection(HoveredEntity, HoveredKind))
            {
                return;
            }

            if (SelectedEntity != Entity.Null &&
                SelectedEntity == HoveredEntity &&
                SelectedKind == HoveredKind)
            {
                ClearSelectionInternal();
                Logger.Info("已取消当前锁定");
                return;
            }

            SelectedEntity = HoveredEntity;
            SelectedKind = HoveredKind;
            HasNewSelection = true;

            UpdateNativeSelectionMarker();

            if (SelectedKind == SelectionKind.Road)
            {
                Logger.Info($"已锁定道路/轨道 #{SelectedEntity.Index}");
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

        public void ClearNewSelectionFlag()
        {
            HasNewSelection = false;
        }

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();

            m_ToolRaycastSystem.collisionMask =
                CollisionMask.OnGround | CollisionMask.Overground | CollisionMask.Underground;

            // 综合选择模式同时允许命中 Net 和建筑类静态对象。
            m_ToolRaycastSystem.typeMask = TypeMask.Net | TypeMask.StaticObjects;

            m_ToolRaycastSystem.raycastFlags =
                RaycastFlags.SubElements |
                RaycastFlags.Cargo |
                RaycastFlags.Passenger |
                RaycastFlags.EditorContainers;

            // 参考 Move It，补上 TrainTrack / SubwayTrack，使铁路和地铁可选。
            m_ToolRaycastSystem.netLayerMask =
                Layer.Road |
                Layer.PublicTransportRoad |
                Layer.Pathway |
                Layer.TrainTrack |
                Layer.SubwayTrack |
                Layer.TramTrack;

            m_ToolRaycastSystem.iconLayerMask = IconLayerMask.None;
            m_ToolRaycastSystem.utilityTypeMask = UtilityTypes.None;
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (m_FocusChanged)
            {
                return inputDeps;
            }

            if (m_GameToolSystem == null || m_GameToolSystem.activeTool != this)
            {
                return inputDeps;
            }

            HasNewSelection = false;

            UpdateHoveredTarget();

            if (SelectedEntity != Entity.Null && !EntityManager.Exists(SelectedEntity))
            {
                ClearSelectionInternal();
            }

            UpdateNativeSelectionMarker();

            if (CanAcceptSceneClick() &&
                Mouse.current != null &&
                Mouse.current.leftButton.wasPressedThisFrame)
            {
                ConfirmHoveredTarget();
            }

            if (CanAcceptSceneClick() &&
                Mouse.current != null &&
                Mouse.current.rightButton.wasPressedThisFrame)
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
        /// 只把“已确认锁定”的对象写给原生 selected
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

        private void UpdateHoveredTarget()
        {
            HoveredEntity = Entity.Null;
            HoveredKind = SelectionKind.None;

            if (!GetRaycastResult(out Entity hitEntity, out _))
            {
                return;
            }

            // 综合模式下优先尝试解析 Net；命中不到再退回建筑。
            Entity net = ResolveRoadEdge(hitEntity);
            if (IsValidRoad(net))
            {
                HoveredEntity = net;
                HoveredKind = SelectionKind.Road;
                return;
            }

            Entity building = ResolveBuildingEntity(hitEntity);
            if (IsValidBuilding(building))
            {
                HoveredEntity = building;
                HoveredKind = SelectionKind.Building;
            }
        }

        private bool IsValidSelection(Entity entity, SelectionKind kind)
        {
            return kind switch
            {
                SelectionKind.Road => IsValidRoad(entity),
                SelectionKind.Building => IsValidBuilding(entity),
                _ => false
            };
        }

        private bool IsValidRoad(Entity entity)
        {
            if (entity == Entity.Null)
            {
                return false;
            }

            if (!EntityManager.Exists(entity))
            {
                return false;
            }

            return EntityManager.HasComponent<Edge>(entity);
        }

        private bool IsValidBuilding(Entity entity)
        {
            if (entity == Entity.Null)
            {
                return false;
            }

            if (!EntityManager.Exists(entity))
            {
                return false;
            }

            if (EntityManager.HasComponent<Edge>(entity))
            {
                return false;
            }

            return EntityManager.HasComponent<PrefabRef>(entity) &&
                   (EntityManager.HasComponent<Game.Buildings.Building>(entity) ||
                    EntityManager.HasComponent<Game.Objects.Transform>(entity));
        }

        private Entity ResolveRoadEdge(Entity entity)
        {
            if (entity == Entity.Null || !EntityManager.Exists(entity))
            {
                return Entity.Null;
            }

            if (EntityManager.HasComponent<Edge>(entity))
            {
                return entity;
            }

            if (EntityManager.HasComponent<Owner>(entity))
            {
                Owner owner = EntityManager.GetComponentData<Owner>(entity);

                if (owner.m_Owner != Entity.Null &&
                    EntityManager.Exists(owner.m_Owner) &&
                    EntityManager.HasComponent<Edge>(owner.m_Owner))
                {
                    return owner.m_Owner;
                }
            }

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

                        if (owner.m_Owner != Entity.Null &&
                            EntityManager.Exists(owner.m_Owner) &&
                            EntityManager.HasComponent<Edge>(owner.m_Owner))
                        {
                            return owner.m_Owner;
                        }
                    }
                }
            }

            return Entity.Null;
        }

        private Entity ResolveBuildingEntity(Entity entity)
        {
            if (entity == Entity.Null || !EntityManager.Exists(entity))
            {
                return Entity.Null;
            }

            Entity current = entity;

            for (int i = 0; i < 8; i++)
            {
                if (!EntityManager.HasComponent<Owner>(current))
                {
                    break;
                }

                Owner owner = EntityManager.GetComponentData<Owner>(current);

                if (owner.m_Owner == Entity.Null || !EntityManager.Exists(owner.m_Owner))
                {
                    break;
                }

                current = owner.m_Owner;
            }

            if (IsValidBuilding(current))
            {
                return current;
            }

            if (IsValidBuilding(entity))
            {
                return entity;
            }

            if (EntityManager.HasComponent<Temp>(entity))
            {
                Temp temp = EntityManager.GetComponentData<Temp>(entity);
                if (temp.m_Original != Entity.Null && IsValidBuilding(temp.m_Original))
                {
                    return temp.m_Original;
                }
            }

            return Entity.Null;
        }
    }
}
