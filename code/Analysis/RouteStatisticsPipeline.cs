using System.Collections.Generic;
using Game;
using Game.Areas;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Creatures;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Routes;
using Game.Simulation;
using Game.Vehicles;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Transit_Scope.code
{
    internal sealed class RouteStatisticsPipeline
    {
        private enum MatchKind
        {
            None = 0,
            PathElementTarget = 1,
            DirectTarget = 2,
            CurrentLane = 3,
            NavigationLane = 4
        }

        private readonly EntityManager m_EntityManager;
        private readonly EntityQuery m_PathSourceQuery;
        private readonly List<Entity> m_WorkingEdges = new();
        private readonly HashSet<Entity> m_SourceSet = new();
        private readonly HashSet<Entity> m_ControllerDedupe = new();

        public RouteStatisticsPipeline(GameSystemBase system)
        {
            m_EntityManager = system.EntityManager;
            m_PathSourceQuery = m_EntityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<UpdateFrame>()
                },
                Any = new[]
                {
                    ComponentType.ReadOnly<PathOwner>(),
                    ComponentType.ReadOnly<TrainCurrentLane>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Game.Tools.Temp>()
                }
            });
        }

        public RouteStatisticsSnapshot Build(Entity selectedEntity, SelectionToolSystem.SelectionKind selectedKind, int? selectedIndex = null)
        {
            RouteStatisticsSelectionContext context = new()
            {
                SelectedEntity = selectedEntity,
                SelectedKind = selectedKind,
                SelectedIndex = selectedIndex
            };

            RouteStatisticsTargetSet targetSet = BuildTargetSet(context);
            List<Entity> matchedPathSources = CollectMatchedPathSources(targetSet);
            List<MatchedPathSourceRecord> activeSources = BuildActiveSourceRecords(context, matchedPathSources);
            List<RouteStatisticsBucket> buckets = Aggregate(activeSources);

            RouteStatisticsSnapshot snapshot = new()
            {
                Context = context,
                TargetSet = targetSet
            };

            snapshot.MatchedSources.AddRange(activeSources);
            snapshot.Buckets.AddRange(buckets);

            Logger.Info(
                $"[RouteStats] Pipeline selected={selectedKind}#{selectedEntity.Index} " +
                $"targetCount={targetSet.Count} matchedSources={activeSources.Count} buckets={buckets.Count}");

            return snapshot;
        }

        private RouteStatisticsTargetSet BuildTargetSet(RouteStatisticsSelectionContext context)
        {
            RouteStatisticsTargetSet set = new();
            Entity selectedEntity = context.SelectedEntity;
            if (selectedEntity == Entity.Null || !m_EntityManager.Exists(selectedEntity))
            {
                return set;
            }

            set.Add(selectedEntity);
            AddSubLanes(selectedEntity, set);
            AddSubNets(selectedEntity, set);
            AddSubAreas(selectedEntity, set);
            AddSubObjects(selectedEntity, set);

            if (m_EntityManager.HasBuffer<SpawnLocationElement>(selectedEntity))
            {
                DynamicBuffer<SpawnLocationElement> spawnLocations = m_EntityManager.GetBuffer<SpawnLocationElement>(selectedEntity);
                for (int i = 0; i < spawnLocations.Length; i++)
                {
                    set.Add(spawnLocations[i].m_SpawnLocation);
                }

                if (m_EntityManager.HasComponent<Attached>(selectedEntity))
                {
                    Entity parent = m_EntityManager.GetComponentData<Attached>(selectedEntity).m_Parent;
                    AddSubLanes(parent, set);
                    AddSubNets(parent, set);
                    AddSubAreas(parent, set);
                    AddSubObjects(parent, set);
                }
            }

            if (m_EntityManager.HasBuffer<Renter>(selectedEntity))
            {
                DynamicBuffer<Renter> renters = m_EntityManager.GetBuffer<Renter>(selectedEntity);
                for (int i = 0; i < renters.Length; i++)
                {
                    set.Add(renters[i].m_Renter);
                }
            }

            if (m_EntityManager.HasBuffer<AggregateElement>(selectedEntity))
            {
                DynamicBuffer<AggregateElement> aggregateElements = m_EntityManager.GetBuffer<AggregateElement>(selectedEntity);
                if (context.SelectedIndex.HasValue &&
                    context.SelectedIndex.Value >= 0 &&
                    context.SelectedIndex.Value < aggregateElements.Length)
                {
                    AddSubLanes(aggregateElements[context.SelectedIndex.Value].m_Edge, set);
                }
                else
                {
                    for (int i = 0; i < aggregateElements.Length; i++)
                    {
                        AddSubLanes(aggregateElements[i].m_Edge, set);
                    }
                }
            }

            if (m_EntityManager.HasBuffer<ConnectedRoute>(selectedEntity))
            {
                DynamicBuffer<ConnectedRoute> connectedRoutes = m_EntityManager.GetBuffer<ConnectedRoute>(selectedEntity);
                for (int i = 0; i < connectedRoutes.Length; i++)
                {
                    set.Add(connectedRoutes[i].m_Waypoint);
                }
            }

            if (m_EntityManager.HasComponent<Game.Objects.OutsideConnection>(selectedEntity) &&
                m_EntityManager.HasComponent<Owner>(selectedEntity))
            {
                AddSubLanes(m_EntityManager.GetComponentData<Owner>(selectedEntity).m_Owner, set);
            }

            return set;
        }

        private List<Entity> CollectMatchedPathSources(RouteStatisticsTargetSet targetSet)
        {
            List<Entity> result = new();
            m_ControllerDedupe.Clear();

            using NativeArray<Entity> entities = m_PathSourceQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity source = entities[i];
                MatchKind reason = MatchSourceReason(source, targetSet);
                if (reason == MatchKind.None)
                {
                    continue;
                }

                if (NeedsPublicTransportGate(source) &&
                    (!m_EntityManager.HasComponent<CurrentVehicle>(source) ||
                     !m_EntityManager.Exists(m_EntityManager.GetComponentData<CurrentVehicle>(source).m_Vehicle) ||
                     !m_EntityManager.HasComponent<Game.Vehicles.PublicTransport>(m_EntityManager.GetComponentData<CurrentVehicle>(source).m_Vehicle)))
                {
                    continue;
                }

                Entity controller = ResolveController(source);
                if (controller != Entity.Null)
                {
                    if (m_ControllerDedupe.Add(controller))
                    {
                        result.Add(controller);
                    }

                    continue;
                }

                if (m_ControllerDedupe.Add(source))
                {
                    result.Add(source);
                }
            }

            return result;
        }

        private List<MatchedPathSourceRecord> BuildActiveSourceRecords(RouteStatisticsSelectionContext context, List<Entity> matchedPathSources)
        {
            List<MatchedPathSourceRecord> result = new();
            m_SourceSet.Clear();

            Entity selectedEntity = context.SelectedEntity;
            if (m_EntityManager.HasComponent<CurrentTransport>(selectedEntity))
            {
                selectedEntity = m_EntityManager.GetComponentData<CurrentTransport>(selectedEntity).m_CurrentTransport;
            }

            Entity controller = ResolveController(selectedEntity);
            if (controller != Entity.Null)
            {
                selectedEntity = controller;
            }

            AddSourceRecord(selectedEntity, result);

            if (m_EntityManager.HasComponent<CurrentVehicle>(selectedEntity))
            {
                Entity currentVehicle = m_EntityManager.GetComponentData<CurrentVehicle>(selectedEntity).m_Vehicle;
                Entity vehicleController = ResolveController(currentVehicle);
                AddSourceRecord(vehicleController != Entity.Null ? vehicleController : currentVehicle, result);
            }

            if (m_EntityManager.HasBuffer<LayoutElement>(selectedEntity) &&
                m_EntityManager.GetBuffer<LayoutElement>(selectedEntity).Length > 0)
            {
                DynamicBuffer<LayoutElement> layouts = m_EntityManager.GetBuffer<LayoutElement>(selectedEntity);
                for (int i = 0; i < layouts.Length; i++)
                {
                    Entity vehicle = layouts[i].m_Vehicle;
                    if (!m_EntityManager.Exists(vehicle) || !m_EntityManager.HasBuffer<Passenger>(vehicle))
                    {
                        continue;
                    }

                    DynamicBuffer<Passenger> passengers = m_EntityManager.GetBuffer<Passenger>(vehicle);
                    for (int j = 0; j < passengers.Length; j++)
                    {
                        AddSourceRecord(passengers[j].m_Passenger, result);
                    }
                }
            }
            else if (m_EntityManager.HasBuffer<Passenger>(selectedEntity))
            {
                DynamicBuffer<Passenger> passengers = m_EntityManager.GetBuffer<Passenger>(selectedEntity);
                for (int i = 0; i < passengers.Length; i++)
                {
                    AddSourceRecord(passengers[i].m_Passenger, result);
                }
            }

            if (m_EntityManager.HasBuffer<HouseholdCitizen>(selectedEntity))
            {
                DynamicBuffer<HouseholdCitizen> householdCitizens = m_EntityManager.GetBuffer<HouseholdCitizen>(selectedEntity);
                for (int i = 0; i < householdCitizens.Length; i++)
                {
                    Entity citizen = householdCitizens[i].m_Citizen;
                    if (m_EntityManager.HasComponent<CurrentTransport>(citizen))
                    {
                        citizen = m_EntityManager.GetComponentData<CurrentTransport>(citizen).m_CurrentTransport;
                    }

                    AddSourceRecord(citizen, result);

                    if (m_EntityManager.HasComponent<CurrentVehicle>(citizen))
                    {
                        Entity vehicle = m_EntityManager.GetComponentData<CurrentVehicle>(citizen).m_Vehicle;
                        Entity vehicleController = ResolveController(vehicle);
                        AddSourceRecord(vehicleController != Entity.Null ? vehicleController : vehicle, result);
                    }
                }
            }

            for (int i = 0; i < matchedPathSources.Count; i++)
            {
                AddSourceRecord(matchedPathSources[i], result);
            }

            return result;
        }

        private void AddSourceRecord(Entity sourceEntity, List<MatchedPathSourceRecord> result)
        {
            if (sourceEntity == Entity.Null ||
                !m_EntityManager.Exists(sourceEntity) ||
                !m_EntityManager.HasBuffer<PathElement>(sourceEntity) ||
                !m_SourceSet.Add(sourceEntity))
            {
                return;
            }

            PathInformation info = m_EntityManager.HasComponent<PathInformation>(sourceEntity)
                ? m_EntityManager.GetComponentData<PathInformation>(sourceEntity)
                : default;

            int startIndex = 0;
            if (m_EntityManager.HasComponent<PathOwner>(sourceEntity))
            {
                DynamicBuffer<PathElement> elements = m_EntityManager.GetBuffer<PathElement>(sourceEntity);
                startIndex = math.clamp(m_EntityManager.GetComponentData<PathOwner>(sourceEntity).m_ElementIndex, 0, elements.Length);
            }

            TryBuildRemainingEdges(sourceEntity, startIndex, m_WorkingEdges);

            MatchedPathSourceRecord record = new()
            {
                SourceEntity = sourceEntity,
                ControllerEntity = ResolveController(sourceEntity),
                Destination = info.m_Destination,
                CurrentEdge = ResolveCurrentEdge(sourceEntity),
                Methods = info.m_Methods,
                VisualizationKind = ClassifyVisualizationKind(sourceEntity),
                NormalizedRouteKey = ComputeNormalizedRouteKey(m_WorkingEdges, info.m_Destination)
            };

            record.RemainingEdges.AddRange(m_WorkingEdges);
            result.Add(record);
        }

        private List<RouteStatisticsBucket> Aggregate(List<MatchedPathSourceRecord> matchedSources)
        {
            Dictionary<RouteVisualizationKind, RouteStatisticsBucket> bucketMap = new();
            Dictionary<(RouteVisualizationKind Kind, ulong RouteKey), RouteStatisticsLineItem> lineMap = new();

            for (int i = 0; i < matchedSources.Count; i++)
            {
                MatchedPathSourceRecord source = matchedSources[i];

                if (!bucketMap.TryGetValue(source.VisualizationKind, out RouteStatisticsBucket bucket))
                {
                    bucket = new RouteStatisticsBucket
                    {
                        Kind = source.VisualizationKind
                    };
                    bucketMap.Add(source.VisualizationKind, bucket);
                }

                if (bucket.SourceCount >= RouteStatisticsSnapshot.VanillaSourceLimitPerKind)
                {
                    bucket.TruncatedBySourceLimit = true;
                    continue;
                }

                bucket.SourceCount++;

                (RouteVisualizationKind, ulong) lineKey = (source.VisualizationKind, source.NormalizedRouteKey);
                if (!lineMap.TryGetValue(lineKey, out RouteStatisticsLineItem line))
                {
                    line = new RouteStatisticsLineItem
                    {
                        RouteKey = source.NormalizedRouteKey.ToString(),
                        SourceCount = 0,
                        SampleEdgeCount = source.RemainingEdges.Count,
                        SampleSourceIndex = source.SourceEntity.Index
                    };
                    lineMap.Add(lineKey, line);
                    bucket.Lines.Add(line);
                }

                line.SourceCount++;
            }

            List<RouteStatisticsBucket> result = new(bucketMap.Values);
            result.Sort((left, right) => right.SourceCount.CompareTo(left.SourceCount));

            for (int i = 0; i < result.Count; i++)
            {
                result[i].Lines.Sort((left, right) => right.SourceCount.CompareTo(left.SourceCount));
            }

            return result;
        }

        private MatchKind MatchSourceReason(Entity source, RouteStatisticsTargetSet targetSet)
        {
            if (!m_EntityManager.Exists(source))
            {
                return MatchKind.None;
            }

            if (m_EntityManager.HasComponent<PathOwner>(source) && m_EntityManager.HasBuffer<PathElement>(source))
            {
                PathOwner pathOwner = m_EntityManager.GetComponentData<PathOwner>(source);
                DynamicBuffer<PathElement> pathElements = m_EntityManager.GetBuffer<PathElement>(source);
                int startIndex = math.clamp(pathOwner.m_ElementIndex, 0, pathElements.Length);

                for (int i = startIndex; i < pathElements.Length; i++)
                {
                    PathElement pathElement = pathElements[i];
                    if ((pathElement.m_Flags & PathElementFlags.Action) == 0 &&
                        targetSet.Targets.Contains(pathElement.m_Target))
                    {
                        return MatchKind.PathElementTarget;
                    }
                }
            }

            if (m_EntityManager.HasComponent<Target>(source) &&
                targetSet.Targets.Contains(m_EntityManager.GetComponentData<Target>(source).m_Target))
            {
                return MatchKind.DirectTarget;
            }

            if (m_EntityManager.HasComponent<HumanCurrentLane>(source) &&
                targetSet.Targets.Contains(m_EntityManager.GetComponentData<HumanCurrentLane>(source).m_Lane))
            {
                return MatchKind.CurrentLane;
            }

            if (m_EntityManager.HasComponent<CarCurrentLane>(source))
            {
                if (targetSet.Targets.Contains(m_EntityManager.GetComponentData<CarCurrentLane>(source).m_Lane))
                {
                    return MatchKind.CurrentLane;
                }

                if (m_EntityManager.HasBuffer<CarNavigationLane>(source))
                {
                    DynamicBuffer<CarNavigationLane> navigationLanes = m_EntityManager.GetBuffer<CarNavigationLane>(source);
                    for (int i = 0; i < navigationLanes.Length; i++)
                    {
                        if (targetSet.Targets.Contains(navigationLanes[i].m_Lane))
                        {
                            return MatchKind.NavigationLane;
                        }
                    }
                }
            }

            if (m_EntityManager.HasComponent<WatercraftCurrentLane>(source))
            {
                if (targetSet.Targets.Contains(m_EntityManager.GetComponentData<WatercraftCurrentLane>(source).m_Lane))
                {
                    return MatchKind.CurrentLane;
                }

                if (m_EntityManager.HasBuffer<WatercraftNavigationLane>(source))
                {
                    DynamicBuffer<WatercraftNavigationLane> navigationLanes = m_EntityManager.GetBuffer<WatercraftNavigationLane>(source);
                    for (int i = 0; i < navigationLanes.Length; i++)
                    {
                        if (targetSet.Targets.Contains(navigationLanes[i].m_Lane))
                        {
                            return MatchKind.NavigationLane;
                        }
                    }
                }
            }

            if (m_EntityManager.HasComponent<AircraftCurrentLane>(source))
            {
                if (targetSet.Targets.Contains(m_EntityManager.GetComponentData<AircraftCurrentLane>(source).m_Lane))
                {
                    return MatchKind.CurrentLane;
                }

                if (m_EntityManager.HasBuffer<AircraftNavigationLane>(source))
                {
                    DynamicBuffer<AircraftNavigationLane> navigationLanes = m_EntityManager.GetBuffer<AircraftNavigationLane>(source);
                    for (int i = 0; i < navigationLanes.Length; i++)
                    {
                        if (targetSet.Targets.Contains(navigationLanes[i].m_Lane))
                        {
                            return MatchKind.NavigationLane;
                        }
                    }
                }
            }

            if (m_EntityManager.HasComponent<TrainCurrentLane>(source))
            {
                TrainCurrentLane currentLane = m_EntityManager.GetComponentData<TrainCurrentLane>(source);
                if (targetSet.Targets.Contains(currentLane.m_Front.m_Lane) ||
                    targetSet.Targets.Contains(currentLane.m_Rear.m_Lane))
                {
                    return MatchKind.CurrentLane;
                }

                if (m_EntityManager.HasBuffer<TrainNavigationLane>(source))
                {
                    DynamicBuffer<TrainNavigationLane> navigationLanes = m_EntityManager.GetBuffer<TrainNavigationLane>(source);
                    for (int i = 0; i < navigationLanes.Length; i++)
                    {
                        if (targetSet.Targets.Contains(navigationLanes[i].m_Lane))
                        {
                            return MatchKind.NavigationLane;
                        }
                    }
                }
            }

            return MatchKind.None;
        }

        private bool NeedsPublicTransportGate(Entity source)
        {
            return m_EntityManager.HasComponent<CurrentVehicle>(source) &&
                   !m_EntityManager.HasBuffer<TransformFrame>(source);
        }

        private void AddSubObjects(Entity entity, RouteStatisticsTargetSet targetSet)
        {
            if (!m_EntityManager.Exists(entity) || !m_EntityManager.HasBuffer<Game.Objects.SubObject>(entity))
            {
                return;
            }

            DynamicBuffer<Game.Objects.SubObject> subObjects = m_EntityManager.GetBuffer<Game.Objects.SubObject>(entity);
            for (int i = 0; i < subObjects.Length; i++)
            {
                Entity subObject = subObjects[i].m_SubObject;
                AddSubLanes(subObject, targetSet);
                AddSubNets(subObject, targetSet);
                AddSubAreas(subObject, targetSet);
                AddSubObjects(subObject, targetSet);
            }
        }

        private void AddSubNets(Entity entity, RouteStatisticsTargetSet targetSet)
        {
            if (!m_EntityManager.Exists(entity) || !m_EntityManager.HasBuffer<Game.Net.SubNet>(entity))
            {
                return;
            }

            DynamicBuffer<Game.Net.SubNet> subNets = m_EntityManager.GetBuffer<Game.Net.SubNet>(entity);
            for (int i = 0; i < subNets.Length; i++)
            {
                AddSubLanes(subNets[i].m_SubNet, targetSet);
            }
        }

        private void AddSubAreas(Entity entity, RouteStatisticsTargetSet targetSet)
        {
            if (!m_EntityManager.Exists(entity) || !m_EntityManager.HasBuffer<Game.Areas.SubArea>(entity))
            {
                return;
            }

            DynamicBuffer<Game.Areas.SubArea> subAreas = m_EntityManager.GetBuffer<Game.Areas.SubArea>(entity);
            for (int i = 0; i < subAreas.Length; i++)
            {
                Entity area = subAreas[i].m_Area;
                AddSubLanes(area, targetSet);
                AddSubAreas(area, targetSet);
            }
        }

        private void AddSubLanes(Entity entity, RouteStatisticsTargetSet targetSet)
        {
            if (!m_EntityManager.Exists(entity) || !m_EntityManager.HasBuffer<Game.Net.SubLane>(entity))
            {
                return;
            }

            DynamicBuffer<Game.Net.SubLane> subLanes = m_EntityManager.GetBuffer<Game.Net.SubLane>(entity);
            for (int i = 0; i < subLanes.Length; i++)
            {
                if (subLanes[i].m_PathMethods != 0)
                {
                    targetSet.Add(subLanes[i].m_SubLane);
                }
            }
        }

        private bool TryBuildRemainingEdges(Entity source, int startIndex, List<Entity> edges)
        {
            edges.Clear();

            if (!m_EntityManager.HasBuffer<PathElement>(source))
            {
                return false;
            }

            DynamicBuffer<PathElement> elements = m_EntityManager.GetBuffer<PathElement>(source);
            Entity currentEdge = ResolveCurrentEdge(source);
            Entity lastEdge = Entity.Null;

            for (int i = startIndex; i < elements.Length; i++)
            {
                Entity edge = EntityResolver.ResolveRoadEdge(m_EntityManager, elements[i].m_Target);
                if (edge == Entity.Null || edge == lastEdge)
                {
                    continue;
                }

                if (edges.Count == 0 && currentEdge != Entity.Null && edge == currentEdge)
                {
                    lastEdge = edge;
                    continue;
                }

                edges.Add(edge);
                lastEdge = edge;
            }

            return edges.Count > 0;
        }

        private Entity ResolveController(Entity source)
        {
            if (source == Entity.Null || !m_EntityManager.Exists(source) || !m_EntityManager.HasComponent<Controller>(source))
            {
                return Entity.Null;
            }

            Entity controller = m_EntityManager.GetComponentData<Controller>(source).m_Controller;
            return controller != Entity.Null && m_EntityManager.Exists(controller) ? controller : Entity.Null;
        }

        private Entity ResolveCurrentEdge(Entity source)
        {
            if (source == Entity.Null || !m_EntityManager.Exists(source))
            {
                return Entity.Null;
            }

            if (m_EntityManager.HasComponent<HumanCurrentLane>(source))
            {
                return EntityResolver.ResolveRoadEdge(m_EntityManager, m_EntityManager.GetComponentData<HumanCurrentLane>(source).m_Lane);
            }

            if (m_EntityManager.HasComponent<CarCurrentLane>(source))
            {
                return EntityResolver.ResolveRoadEdge(m_EntityManager, m_EntityManager.GetComponentData<CarCurrentLane>(source).m_Lane);
            }

            if (m_EntityManager.HasComponent<TrainCurrentLane>(source))
            {
                TrainCurrentLane currentLane = m_EntityManager.GetComponentData<TrainCurrentLane>(source);
                Entity lane = currentLane.m_Front.m_Lane != Entity.Null ? currentLane.m_Front.m_Lane : currentLane.m_Rear.m_Lane;
                return EntityResolver.ResolveRoadEdge(m_EntityManager, lane);
            }

            if (m_EntityManager.HasComponent<WatercraftCurrentLane>(source))
            {
                return EntityResolver.ResolveRoadEdge(m_EntityManager, m_EntityManager.GetComponentData<WatercraftCurrentLane>(source).m_Lane);
            }

            if (m_EntityManager.HasComponent<AircraftCurrentLane>(source))
            {
                return EntityResolver.ResolveRoadEdge(m_EntityManager, m_EntityManager.GetComponentData<AircraftCurrentLane>(source).m_Lane);
            }

            return Entity.Null;
        }

        private static ulong ComputeNormalizedRouteKey(List<Entity> edges, Entity destination)
        {
            unchecked
            {
                ulong hash = 1469598103934665603UL;
                hash = (hash ^ (uint)destination.Index) * 1099511628211UL;
                hash = (hash ^ (uint)destination.Version) * 1099511628211UL;

                for (int i = 0; i < edges.Count; i++)
                {
                    hash = (hash ^ (uint)edges[i].Index) * 1099511628211UL;
                    hash = (hash ^ (uint)edges[i].Version) * 1099511628211UL;
                }

                return hash;
            }
        }

        private RouteVisualizationKind ClassifyVisualizationKind(Entity sourceEntity)
        {
            if (m_EntityManager.HasComponent<Human>(sourceEntity))
            {
                return RouteVisualizationKind.Human;
            }

            if (m_EntityManager.HasComponent<Watercraft>(sourceEntity))
            {
                return RouteVisualizationKind.Watercraft;
            }

            if (m_EntityManager.HasComponent<Aircraft>(sourceEntity))
            {
                return RouteVisualizationKind.Aircraft;
            }

            if (m_EntityManager.HasComponent<Train>(sourceEntity))
            {
                return RouteVisualizationKind.Train;
            }

            if (m_EntityManager.HasComponent<Bicycle>(sourceEntity))
            {
                return RouteVisualizationKind.Bicycle;
            }

            return RouteVisualizationKind.Car;
        }

    }
}
