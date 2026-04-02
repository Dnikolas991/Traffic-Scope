using System.Collections.Generic;
using Game;
using Game.Common;
using Game.Net;
using Game.Pathfind;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using NetSubLane = Game.Net.SubLane;

namespace Transit_Scope.code
{
    /// <summary>
    /// 负责计算当前选中对象的交通分析结果。
    /// 
    /// 与旧实现相比，这个系统做了两件关键调整：
    /// 1. 不再维护全图级的大缓存，而是只为当前选中的对象做针对性分析。
    ///    这样可以避免无意义的全量聚合，也更容易保证统计口径正确。
    /// 2. 除了返回统计数字，还会同步产出导航路径所经过的边集合，
    ///    供 Overlay 系统绘制“未来也会经过这里”的路线高亮。
    /// 
    /// 统计口径：
    /// - 道路：当前就在该边上的对象 + 未来路径会经过该边的对象。
    /// - 建筑：未来目的地为该建筑的输入流量。
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class ScopeTrafficFlowSystem : GameSystemBase
    {
        private EntityQuery m_PathQuery;

        /// <summary>
        /// 复用的工作缓冲区。
        /// 为了降低 GC 压力，这些集合会在每次分析开始前清空，而不是重复 new。
        /// </summary>
        private readonly List<Entity> m_WorkingRouteEdges = new();
        private readonly HashSet<Entity> m_WorkingCurrentOccupants = new();

        /// <summary>
        /// 当前已经完成的分析结果。
        /// Overlay 系统会读取这里的导航路径缓存来绘制选中对象的路线高亮。
        /// </summary>
        internal ScopeSelectionAnalysis CurrentSelectionAnalysis { get; private set; }

        protected override void OnCreate()
        {
            base.OnCreate();

            m_PathQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<PathOwner>(),
                    ComponentType.ReadOnly<PathElement>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Game.Tools.Temp>()
                }
            });

            Logger.Info("ScopeTrafficFlowSystem 已启动，准备按选中对象实时分析交通路径。");
        }

        protected override void OnUpdate()
        {
            // 该系统作为分析服务存在，不在自己的 Update 中主动做全图扫描。
            // 只有外部系统在玩家选中对象后才会触发分析。
        }

        /// <summary>
        /// 清空当前选中的分析结果。
        /// 当玩家取消选择时，路线高亮和统计面板都应该同步消失。
        /// </summary>
        public void ClearSelectionAnalysis()
        {
            CurrentSelectionAnalysis = null;
        }

        /// <summary>
        /// 根据当前选中的对象刷新分析结果。
        /// </summary>
        internal ScopeSelectionAnalysis RefreshSelectionAnalysis(Entity selectedEntity, ScopeToolSystem.SelectionKind selectionKind)
        {
            if (selectedEntity == Entity.Null || !EntityManager.Exists(selectedEntity))
            {
                CurrentSelectionAnalysis = null;
                return null;
            }

            CurrentSelectionAnalysis = selectionKind switch
            {
                ScopeToolSystem.SelectionKind.Road => AnalyzeRoadSelection(selectedEntity),
                ScopeToolSystem.SelectionKind.Building => AnalyzeBuildingSelection(selectedEntity),
                _ => null
            };

            return CurrentSelectionAnalysis;
        }

        /// <summary>
        /// 分析道路流量。
        /// 当前流量通过道路上的 LaneObject 统计，未来流量通过 PathOwner 路径统计。
        /// </summary>
        private ScopeSelectionAnalysis AnalyzeRoadSelection(Entity selectedEdge)
        {
            if (!ScopeEntityResolver.IsRoad(EntityManager, selectedEdge))
            {
                return null;
            }

            ScopeSelectionAnalysis analysis = new()
            {
                SelectedEntity = selectedEdge,
                SelectedKind = ScopeToolSystem.SelectionKind.Road,
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
        /// 只统计“未来目的地就是该建筑”的对象，不再混入周边道路上的即时流量。
        /// </summary>
        private ScopeSelectionAnalysis AnalyzeBuildingSelection(Entity selectedBuilding)
        {
            if (!ScopeEntityResolver.IsBuilding(EntityManager, selectedBuilding))
            {
                return null;
            }

            ScopeSelectionAnalysis analysis = new()
            {
                SelectedEntity = selectedBuilding,
                SelectedKind = ScopeToolSystem.SelectionKind.Building,
                TitleKey = "stats.title.building",
                FallbackTitle = "Building Traffic",
                SubtitleKey = "stats.subtitle.nearby_building",
                SubtitleArgument = selectedBuilding.Index.ToString(),
                FallbackSubtitle = $"Inflow traffic for building #{selectedBuilding.Index}"
            };

            using NativeArray<Entity> pathOwners = m_PathQuery.ToEntityArray(Allocator.Temp);
            for (int index = 0; index < pathOwners.Length; index++)
            {
                Entity pathOwnerEntity = pathOwners[index];
                if (!EntityManager.HasComponent<PathInformation>(pathOwnerEntity))
                {
                    continue;
                }

                PathInformation pathInformation = EntityManager.GetComponentData<PathInformation>(pathOwnerEntity);
                if (pathInformation.m_Destination != selectedBuilding)
                {
                    continue;
                }

                TrafficCounters agentTraffic = ScopeTrafficClassifier.ClassifySingle(EntityManager, pathOwnerEntity);
                if (agentTraffic.Total == 0)
                {
                    continue;
                }

                analysis.PlannedTraffic.Add(agentTraffic);

                if (TryBuildPlannedRoute(pathOwnerEntity, m_WorkingRouteEdges))
                {
                    analysis.RegisterRouteEdges(m_WorkingRouteEdges);
                }
            }

            return analysis;
        }

        /// <summary>
        /// 统计当前就在选中道路上的对象。
        /// 同时把这些对象写入 occupant 集合，后续可以避免与“未来路径流量”重复计数。
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
                    ScopeTrafficClassifier.AddEntity(EntityManager, trafficObject, ref currentTraffic);
                }
            }
        }

        /// <summary>
        /// 统计未来会经过选中道路的对象，并同步提取这些对象的剩余导航路径。
        /// </summary>
        private void CollectPlannedRoadTraffic(Entity selectedEdge, ref TrafficCounters plannedTraffic, ScopeSelectionAnalysis analysis)
        {
            using NativeArray<Entity> pathOwners = m_PathQuery.ToEntityArray(Allocator.Temp);
            for (int index = 0; index < pathOwners.Length; index++)
            {
                Entity pathOwnerEntity = pathOwners[index];
                TrafficCounters agentTraffic = ScopeTrafficClassifier.ClassifySingle(EntityManager, pathOwnerEntity);
                if (agentTraffic.Total == 0)
                {
                    continue;
                }

                if (!TryBuildPlannedRoute(pathOwnerEntity, m_WorkingRouteEdges))
                {
                    continue;
                }

                int selectedEdgeIndex = FindRouteEdgeIndex(m_WorkingRouteEdges, selectedEdge);
                if (selectedEdgeIndex < 0)
                {
                    continue;
                }

                bool isCurrentOccupant = m_WorkingCurrentOccupants.Contains(pathOwnerEntity);

                // 如果对象当前就已经在这条边上，且路径中的第一个边也是该边，
                // 那么这一次命中应该算作“当前流量”，而不是“未来将到达”。
                if (!isCurrentOccupant || selectedEdgeIndex > 0)
                {
                    plannedTraffic.Add(agentTraffic);
                }

                // 路线展示希望保留这类对象的完整剩余导航路径，
                // 因为玩家不仅关心“会不会到这里”，也关心“从哪里来、往哪里去”。
                analysis.RegisterRouteEdges(m_WorkingRouteEdges);
            }
        }

        /// <summary>
        /// 从 PathOwner 和 PathElement 中提取该对象未来剩余的导航路径，
        /// 并将路径上的子实体解析为真正的道路边实体。
        /// </summary>
        private bool TryBuildPlannedRoute(Entity pathOwnerEntity, List<Entity> routeEdges)
        {
            routeEdges.Clear();

            if (!EntityManager.HasComponent<PathOwner>(pathOwnerEntity) || !EntityManager.HasBuffer<PathElement>(pathOwnerEntity))
            {
                return false;
            }

            PathOwner pathOwner = EntityManager.GetComponentData<PathOwner>(pathOwnerEntity);
            DynamicBuffer<PathElement> pathElements = EntityManager.GetBuffer<PathElement>(pathOwnerEntity);

            int startIndex = math.clamp(pathOwner.m_ElementIndex, 0, pathElements.Length);
            Entity lastEdge = Entity.Null;

            for (int elementIndex = startIndex; elementIndex < pathElements.Length; elementIndex++)
            {
                Entity pathTarget = pathElements[elementIndex].m_Target;
                Entity edge = ScopeEntityResolver.ResolveRoadEdge(EntityManager, pathTarget);

                // 路径中连续的子元素经常会指向同一条边。
                // 这里做一次顺序去重，避免一辆车在同一条边上被重复登记多次。
                if (edge == Entity.Null || edge == lastEdge)
                {
                    continue;
                }

                routeEdges.Add(edge);
                lastEdge = edge;
            }

            return routeEdges.Count > 0;
        }

        /// <summary>
        /// 在路径中查找指定边第一次出现的位置。
        /// </summary>
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
