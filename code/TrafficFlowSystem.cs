using System.Collections.Generic;
using Game;
using Game.Common;
using Game.Creatures;
using Game.Net;
using Game.Pathfind;
using Game.Vehicles;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using NetSubLane = Game.Net.SubLane;

namespace Transit_Scope.code
{
    /// <summary>
    /// 负责两件彼此相关但职责不同的工作：
    /// 1. 每帧重建“当前仍然有效且尚未被 agent 消费完”的剩余导航路线快照。
    /// 2. 基于这份快照，为当前选中的道路或建筑生成统计和路线高亮数据。
    ///
    /// 这套实现不再统计“本帧新算了多少路线”，而是统计：
    /// - 路线已经生成
    /// - 已经分配给 agent
    /// - 从 agent 当前消费游标往后看，仍然还有哪些未来路段没有走到
    ///
    /// 核心依据：
    /// - PathOwner.m_ElementIndex 表示当前已经消费到的路径元素位置
    /// - PathElement buffer 存储完整路径元素序列
    /// - *CurrentLane 表示 agent 当前实际所在的 lane/位置
    /// 因此真正需要统计的，是 PathElement 从 m_ElementIndex 往后的剩余切片。
    /// </summary>
    //本工具加入游戏的模拟系统
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class TrafficFlowSystem : GameSystemBase
    {
        // 以下状态说明路径已经失效、失败，或明显是待清理的旧结果。
        // 这些 owner 不应进入“当前有效剩余路线”统计。
        private const PathFlags InvalidPathStateMask =
            PathFlags.Failed |
            PathFlags.Obsolete |
            //过期的绕路方案
            PathFlags.DivertObsolete |
            PathFlags.CachedObsolete;

        /// <summary>
        /// 以下状态表示路径正在被替换、拼接或改道。
        /// 这些状态下容易同时看到旧路径和新路径痕迹，因此要求至少连续两帧一致后再纳入稳定统计。
        /// </summary>
        private const PathFlags TransitionalPathStateMask =
            PathFlags.Pending |
            PathFlags.Scheduled |
            PathFlags.Append |
            PathFlags.Updated |
            PathFlags.Divert;

        //路径实体查询
        private EntityQuery m_PathQuery;

        /// <summary>
        /// 当前帧已经确认稳定的剩余路线快照。
        /// 道路 future traffic、建筑 inflow 和 Overlay 全部直接消费这份数据。
        /// </summary>
        private readonly Dictionary<Entity, ActiveAssignedRoute> m_ActiveRoutes = new();

        /// <summary>
        /// 上一帧观测到的剩余路线快照。
        /// 用来做跨帧稳定确认，避免改道瞬间把旧路线和新路线同时算进去。
        /// </summary>
        private readonly Dictionary<Entity, ActiveAssignedRoute> m_PreviousObservedRoutes = new();

        private readonly List<Entity> m_WorkingRouteEdges = new();
        //工作缓冲区，避免重复计数
        private readonly HashSet<Entity> m_WorkingCurrentOccupants = new();

        //选中对象的分析结果
        internal SelectionAnalysis CurrentSelectionAnalysis { get; private set; }
        //路线账本
        internal RouteFrameStats LatestRouteStats { get; private set; }

        private uint m_FrameIndex;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_PathQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    //有路径内容的才可以分析
                    ComponentType.ReadOnly<PathOwner>(),
                    ComponentType.ReadOnly<PathInformation>(),
                    ComponentType.ReadOnly<PathElement>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Game.Tools.Temp>()
                }
            });

            Logger.Info("TrafficFlowSystem 已启动，准备按剩余有效路线切片统计导航流量。");
        }

        protected override void OnUpdate()
        {
            // 当前仍由 SelectionSystem 主动驱动，避免和 UI 刷新逻辑脱节。
        }

        /// <summary>
        /// 推进一次剩余路线快照。
        /// 每帧都重新从 ECS 当前状态计算，不依赖 Ready 扫描或历史生成事件。
        /// </summary>
        internal void AdvanceRouteLedger()
        {
            m_FrameIndex++;

            //当前帧所有路线快照
            Dictionary<Entity, ActiveAssignedRoute> observedRoutes = new();
            RouteFrameStats frameStats = new()
            {
                FrameIndex = m_FrameIndex
            };

            using NativeArray<Entity> pathOwners = m_PathQuery.ToEntityArray(Allocator.Temp);
            frameStats.ObservedOwners = pathOwners.Length;

            //在遍历每个 owner，并尝试把它变成一个当前有效的 ActiveAssignedRoute
            for (int index = 0; index < pathOwners.Length; index++)
            {
                Entity owner = pathOwners[index];
                if (!TryBuildActiveRoute(owner, out ActiveAssignedRoute route))
                {
                    continue;
                }

                ApplyStability(owner, route);
                observedRoutes[owner] = route;

                if (!route.IsStable)
                {
                    continue;
                }

                //稳定路线进入可消费账本
                m_ActiveRoutes[owner] = route;
                frameStats.StableOwners++;
                frameStats.RemainingSegments += route.RemainingElements;
            }

            m_ActiveRoutes.Clear();
            foreach (KeyValuePair<Entity, ActiveAssignedRoute> pair in observedRoutes)
            {
                if (pair.Value.IsStable)
                {
                    m_ActiveRoutes[pair.Key] = pair.Value;
                }
            }

            m_PreviousObservedRoutes.Clear();
            foreach (KeyValuePair<Entity, ActiveAssignedRoute> pair in observedRoutes)
            {
                //这一帧观测结果会成为下一帧的“上一帧参考”
                m_PreviousObservedRoutes[pair.Key] = pair.Value;
            }

            LatestRouteStats = frameStats;
        }

        /// <summary>
        /// 清空当前选中对象的分析结果。
        /// 当玩家取消选择时，路线高亮和面板统计都应同步消失。
        /// </summary>
        public void ClearSelectionAnalysis()
        {
            CurrentSelectionAnalysis = null;
        }

        /// <summary>
        /// 根据当前选中的对象刷新分析结果。
        /// 这里消费的是“当前稳定的剩余路线快照”，而不是把所有路径结果都当成未来流量。
        /// </summary>
        internal SelectionAnalysis RefreshSelectionAnalysis(Entity selectedEntity, SelectionToolSystem.SelectionKind selectionKind)
        {
            if (selectedEntity == Entity.Null || !EntityManager.Exists(selectedEntity))
            {
                CurrentSelectionAnalysis = null;
                return null;
            }

            CurrentSelectionAnalysis = selectionKind switch
            {
                SelectionToolSystem.SelectionKind.Road => AnalyzeRoadSelection(selectedEntity),
                SelectionToolSystem.SelectionKind.Building => AnalyzeBuildingSelection(selectedEntity),
                _ => null
            };

            return CurrentSelectionAnalysis;
        }

        /// <summary>
        /// 从一个 path owner 构建当前仍然有效的剩余路线快照。
        /// 关键不是看路径“是不是新生成”，而是看它从当前游标往后是否还有剩余未消费的内容。
        /// </summary>
        private bool TryBuildActiveRoute(Entity owner, out ActiveAssignedRoute route)
        {
            route = null;

            if (!EntityManager.Exists(owner))
            {
                return false;
            }

            //提供消费游标和原路径信息
            PathOwner pathOwner = EntityManager.GetComponentData<PathOwner>(owner);
            PathInformation pathInfo = EntityManager.GetComponentData<PathInformation>(owner);

            if (IsInvalidPathState(pathOwner.m_State) || IsInvalidPathState(pathInfo.m_State))
            {
                return false;
            }

            DynamicBuffer<PathElement> pathElements = EntityManager.GetBuffer<PathElement>(owner);
            if (pathElements.Length == 0)
            {
                return false;
            }

            int startIndex = math.clamp(pathOwner.m_ElementIndex, 0, pathElements.Length);
            if (startIndex >= pathElements.Length)
            {
                return false;
            }

            Entity currentEdge = ResolveCurrentEdge(owner);
            if (!TryBuildRemainingRoute(owner, startIndex, currentEdge, m_WorkingRouteEdges))
            {
                return false;
            }

            route = new ActiveAssignedRoute
            {
                //路线归谁
                OwnerEntity = owner,
                //目的地
                Destination = pathInfo.m_Destination,
                //当前位置
                CurrentEdge = currentEdge,
                OwnerState = pathOwner.m_State,
                InfoState = pathInfo.m_State,
                //路径方式
                Methods = pathInfo.m_Methods,
                ElementIndex = startIndex,
                TotalElements = pathElements.Length,
                RemainingElements = math.max(0, pathElements.Length - startIndex),
                RemainingRouteHash = ComputeRouteHash(m_WorkingRouteEdges, pathInfo.m_Destination, startIndex),
                LastSeenFrame = m_FrameIndex,
                //对路线所有者进行分类统计
                Traffic = TrafficClassifier.ClassifySingle(EntityManager, owner)
            };

            CopyRouteEdges(route.RemainingRouteEdges, m_WorkingRouteEdges);
            return route.Traffic.Total > 0 && route.RemainingRouteEdges.Count > 0;
        }

        /// <summary>
        /// 通过上一帧快照做稳定确认。
        /// 如果路径处在过渡态，就要求至少连续两帧路径签名一致，才认定为当前有效 future route。
        /// </summary>
        private void ApplyStability(Entity owner, ActiveAssignedRoute route)
        {
            bool requiresStability =
                HasTransitionalState(route.OwnerState) ||
                HasTransitionalState(route.InfoState);

            //判断为稳定的四个要求
            if (m_PreviousObservedRoutes.TryGetValue(owner, out ActiveAssignedRoute previous) &&
                previous.RemainingRouteHash == route.RemainingRouteHash &&
                previous.ElementIndex == route.ElementIndex &&
                previous.CurrentEdge == route.CurrentEdge &&
                previous.Destination == route.Destination)
            {
                route.StableFrameCount = previous.StableFrameCount + 1;
            }
            else
            {
                route.StableFrameCount = 1;
            }

            //抑制改道瞬间的数据失真
            route.IsStable = !requiresStability || route.StableFrameCount >= 2;
        }

        /// <summary>
        /// 分析道路流量。
        /// 当前流量来自道路上实际存在的对象；
        /// future traffic 来自剩余路线快照中未来仍会经过该道路的对象。
        /// </summary>
        private SelectionAnalysis AnalyzeRoadSelection(Entity selectedEdge)
        {
            if (!EntityResolver.IsRoad(EntityManager, selectedEdge))
            {
                return null;
            }

            SelectionAnalysis analysis = new()
            {
                SelectedEntity = selectedEdge,
                SelectedKind = SelectionToolSystem.SelectionKind.Road,
                TitleKey = "stats.title.road",
                FallbackTitle = "Traffic Flow",
                SubtitleKey = "stats.subtitle.selected_edge",
                SubtitleArgument = selectedEdge.Index.ToString(),
                FallbackSubtitle = $"Selected edge #{selectedEdge.Index}"
            };

            m_WorkingCurrentOccupants.Clear();
            CollectCurrentEdgeTraffic(selectedEdge, ref analysis.CurrentTraffic, m_WorkingCurrentOccupants);
            CollectPlannedRoadTraffic(selectedEdge, ref analysis.PlannedTraffic, analysis);

            return analysis;
        }

        /// <summary>
        /// 分析建筑输入流量。
        /// 只认当前稳定且目的地就是该建筑的剩余路线快照。
        /// </summary>
        private SelectionAnalysis AnalyzeBuildingSelection(Entity selectedBuilding)
        {
            if (!EntityResolver.IsBuilding(EntityManager, selectedBuilding))
            {
                return null;
            }

            SelectionAnalysis analysis = new()
            {
                SelectedEntity = selectedBuilding,
                SelectedKind = SelectionToolSystem.SelectionKind.Building,
                TitleKey = "stats.title.building",
                FallbackTitle = "Building Traffic",
                SubtitleKey = "stats.subtitle.nearby_building",
                SubtitleArgument = selectedBuilding.Index.ToString(),
                FallbackSubtitle = $"Inflow traffic for building #{selectedBuilding.Index}"
            };

            
            //从剩余路线选择目的地是当前建筑的路线
            foreach (ActiveAssignedRoute route in m_ActiveRoutes.Values)
            {
                if (route.Destination != selectedBuilding)
                {
                    continue;
                }

                analysis.PlannedTraffic.Add(route.Traffic);
                analysis.RegisterRouteEdges(route.RemainingRouteEdges);
            }

            return analysis;
        }

        /// <summary>
        /// 统计当前就位于选中道路上的对象。
        /// 同时把这些对象缓存起来，避免它们在 future traffic 中把当前这条边重复算一遍。
        /// </summary>
        private void CollectCurrentEdgeTraffic(Entity selectedEdge, ref TrafficCounters currentTraffic, HashSet<Entity> occupants)
        {
            if (!EntityManager.HasBuffer<NetSubLane>(selectedEdge))
            {
                return;
            }

            DynamicBuffer<NetSubLane> lanes = EntityManager.GetBuffer<NetSubLane>(selectedEdge);
            for (int laneIndex = 0; laneIndex < lanes.Length; laneIndex++)
            {
                Entity laneEntity = lanes[laneIndex].m_SubLane;
                if (!EntityManager.Exists(laneEntity) || !EntityManager.HasBuffer<LaneObject>(laneEntity))
                {
                    continue;
                }

                DynamicBuffer<LaneObject> laneObjects = EntityManager.GetBuffer<LaneObject>(laneEntity);
                for (int objectIndex = 0; objectIndex < laneObjects.Length; objectIndex++)
                {
                    Entity trafficObject = laneObjects[objectIndex].m_LaneObject;
                    if (trafficObject == Entity.Null || !EntityManager.Exists(trafficObject))
                    {
                        continue;
                    }

                    occupants.Add(trafficObject);
                    TrafficClassifier.AddEntity(EntityManager, trafficObject, ref currentTraffic);
                }
            }
        }

        /// <summary>
        /// 从当前稳定的剩余路线快照中提取 future traffic。
        /// 如果某个对象当前就在这条道路上，而且剩余路线的第一段仍然是这条道路，
        /// 这部分只用于路线显示，不再重复算成 future traffic。
        /// </summary>
        private void CollectPlannedRoadTraffic(Entity selectedEdge, ref TrafficCounters plannedTraffic, SelectionAnalysis analysis)
        {
            foreach (ActiveAssignedRoute route in m_ActiveRoutes.Values)
            {
                int selectedEdgeIndex = FindRouteEdgeIndex(route.RemainingRouteEdges, selectedEdge);
                if (selectedEdgeIndex < 0)
                {
                    continue;
                }

                bool isCurrentOccupant = m_WorkingCurrentOccupants.Contains(route.OwnerEntity);
                bool startsOnSelectedEdge = route.RemainingRouteEdges.Count > 0 && route.RemainingRouteEdges[0] == selectedEdge;

                if (!isCurrentOccupant || !startsOnSelectedEdge || selectedEdgeIndex > 0)
                {
                    plannedTraffic.Add(route.Traffic);
                }

                analysis.RegisterRouteEdges(route.RemainingRouteEdges);
            }
        }

        /// <summary>
        /// 从剩余路径元素切片中提取未来道路边序列。
        /// 这里会做两层过滤：
        /// 1. 连续重复边去重，避免一条边被多个子元素重复登记。
        /// 2. 如果第一条边就是 agent 当前所在边，则把它从 future 路线里剔除。
        ///    因为用户要看的不是“当前位置正在走的边”，而是“还没走到的未来边”。
        /// </summary>
        private bool TryBuildRemainingRoute(Entity owner, int startIndex, Entity currentEdge, List<Entity> routeEdges)
        {
            routeEdges.Clear();

            DynamicBuffer<PathElement> pathElements = EntityManager.GetBuffer<PathElement>(owner);
            Entity lastEdge = Entity.Null;

            for (int elementIndex = startIndex; elementIndex < pathElements.Length; elementIndex++)
            {
                Entity edge = EntityResolver.ResolveRoadEdge(EntityManager, pathElements[elementIndex].m_Target);
                if (edge == Entity.Null || edge == lastEdge)
                {
                    continue;
                }

                routeEdges.Add(edge);
                lastEdge = edge;
            }

            if (routeEdges.Count == 0)
            {
                return false;
            }

            if (currentEdge != Entity.Null && routeEdges[0] == currentEdge)
            {
                routeEdges.RemoveAt(0);
            }

            return routeEdges.Count > 0;
        }

        /// <summary>
        /// 根据 agent 当前的 *CurrentLane 组件推导它当前所在的道路边。
        /// 这一步用于把“当前位置正在走的边”从 future 路线中剔除。
        /// </summary>
        private Entity ResolveCurrentEdge(Entity owner)
        {
            Entity currentLane = Entity.Null;

            if (EntityManager.HasComponent<HumanCurrentLane>(owner))
            {
                currentLane = EntityManager.GetComponentData<HumanCurrentLane>(owner).m_Lane;
            }
            else if (EntityManager.HasComponent<AnimalCurrentLane>(owner))
            {
                currentLane = EntityManager.GetComponentData<AnimalCurrentLane>(owner).m_Lane;
            }
            else if (EntityManager.HasComponent<CarCurrentLane>(owner))
            {
                currentLane = EntityManager.GetComponentData<CarCurrentLane>(owner).m_Lane;
            }
            else if (EntityManager.HasComponent<TrainCurrentLane>(owner))
            {
                TrainCurrentLane trainCurrentLane = EntityManager.GetComponentData<TrainCurrentLane>(owner);
                currentLane = trainCurrentLane.m_Front.m_Lane != Entity.Null
                    ? trainCurrentLane.m_Front.m_Lane
                    : trainCurrentLane.m_Rear.m_Lane;
            }
            else if (EntityManager.HasComponent<WatercraftCurrentLane>(owner))
            {
                currentLane = EntityManager.GetComponentData<WatercraftCurrentLane>(owner).m_Lane;
            }
            else if (EntityManager.HasComponent<AircraftCurrentLane>(owner))
            {
                currentLane = EntityManager.GetComponentData<AircraftCurrentLane>(owner).m_Lane;
            }

            return EntityResolver.ResolveRoadEdge(EntityManager, currentLane);
        }

        private static bool IsInvalidPathState(PathFlags state)
        {
            return (state & InvalidPathStateMask) != 0;
        }

        private static bool HasTransitionalState(PathFlags state)
        {
            return (state & TransitionalPathStateMask) != 0;
        }

        private static void CopyRouteEdges(List<Entity> target, List<Entity> source)
        {
            target.Clear();

            for (int index = 0; index < source.Count; index++)
            {
                target.Add(source[index]);
            }
        }

        private static uint ComputeRouteHash(List<Entity> routeEdges, Entity destination, int startIndex)
        {
            uint hash = 2166136261u;
            hash = MixHash(hash, (uint)startIndex);
            hash = MixHash(hash, (uint)routeEdges.Count);
            hash = MixHash(hash, (uint)destination.Index);
            hash = MixHash(hash, (uint)destination.Version);

            for (int index = 0; index < routeEdges.Count; index++)
            {
                Entity edge = routeEdges[index];
                hash = MixHash(hash, (uint)edge.Index);
                hash = MixHash(hash, (uint)edge.Version);
            }

            return hash;
        }

        private static uint MixHash(uint current, uint value)
        {
            return (current ^ value) * 16777619u;
        }

        private static int FindRouteEdgeIndex(List<Entity> routeEdges, Entity targetEdge)
        {
            for (int index = 0; index < routeEdges.Count; index++)
            {
                if (routeEdges[index] == targetEdge)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
