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
    /// Custom selection tool for Transit Scope.
    /// It resolves the hovered or confirmed hit into a display entity and a source entity.
    /// </summary>
    public partial class SelectionToolSystem : ToolBaseSystem
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
        public Entity HoveredSourceEntity { get; private set; } = Entity.Null;

        public Entity SelectedEntity { get; private set; } = Entity.Null;
        public SelectionKind SelectedKind { get; private set; } = SelectionKind.None;
        public Entity SelectedSourceEntity { get; private set; } = Entity.Null;
        public int SelectedIndex => m_GameToolSystem?.selectedIndex ?? -1;

        /// <summary>
        /// Signals that the confirmed selection changed this frame.
        /// </summary>
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

        public override string toolID => "ScopeTool";

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

        /// <summary>
        /// Confirms the current hovered target.
        /// Clicking the same object again clears the selection.
        /// </summary>
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
                Logger.Info("已取消当前锁定对象。");
                return;
            }

            SelectedEntity = HoveredEntity;
            SelectedKind = HoveredKind;
            SelectedSourceEntity = HoveredSourceEntity;
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

        public void ClearNewSelectionFlag()
        {
            HasNewSelection = false;
        }

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();

            m_ToolRaycastSystem.collisionMask =
                CollisionMask.OnGround | CollisionMask.Overground | CollisionMask.Underground;

            m_ToolRaycastSystem.typeMask = TypeMask.Net | TypeMask.StaticObjects;

            m_ToolRaycastSystem.raycastFlags =
                RaycastFlags.SubElements |
                RaycastFlags.Cargo |
                RaycastFlags.Passenger |
                RaycastFlags.EditorContainers;

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

            if (CanAcceptSceneClick() && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                ConfirmHoveredTarget();
            }

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
        /// Writes the confirmed selection back to the vanilla tool state.
        /// Source entity is preferred so statistics stay aligned with the game.
        /// </summary>
        private void UpdateNativeSelectionMarker()
        {
            if (m_GameToolSystem == null)
            {
                return;
            }

            m_GameToolSystem.selected = SelectedSourceEntity != Entity.Null
                ? SelectedSourceEntity
                : SelectedEntity;
        }

        private void ResetState()
        {
            HoveredEntity = Entity.Null;
            HoveredKind = SelectionKind.None;
            HoveredSourceEntity = Entity.Null;
            SelectedEntity = Entity.Null;
            SelectedKind = SelectionKind.None;
            SelectedSourceEntity = Entity.Null;
            HasNewSelection = false;
        }

        private void ClearSelectionInternal()
        {
            ResetState();
            UpdateNativeSelectionMarker();
        }

        /// <summary>
        /// Resolves the current raycast hit into hover state.
        /// Roads are preferred; buildings are used as fallback.
        /// </summary>
        private void UpdateHoveredTarget()
        {
            HoveredEntity = Entity.Null;
            HoveredKind = SelectionKind.None;
            HoveredSourceEntity = Entity.Null;

            if (!GetRaycastResult(out Entity hitEntity, out _))
            {
                return;
            }

            HoveredSourceEntity = hitEntity;

            Entity roadEntity = EntityResolver.ResolveRoadEdge(EntityManager, hitEntity);
            if (EntityResolver.IsRoad(EntityManager, roadEntity))
            {
                HoveredEntity = roadEntity;
                HoveredKind = SelectionKind.Road;
                return;
            }

            Entity buildingEntity = EntityResolver.ResolveBuildingEntity(EntityManager, hitEntity);
            if (EntityResolver.IsBuilding(EntityManager, buildingEntity))
            {
                HoveredEntity = buildingEntity;
                HoveredKind = SelectionKind.Building;
            }
        }

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
