using Colossal.Mathematics;
using Game;
using Game.Creatures;
using Game.Net;
using Game.Objects;
using Game.Prefabs;
using Game.Rendering;
using Game.Vehicles;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using NetSubLane = Game.Net.SubLane;
using PrefabRefData = Game.Prefabs.PrefabRef;

namespace Transit_Scope.code
{
    /// <summary>
    /// 负责在玩家确认选中对象后做统计分析，并把结果推给前端图形化展示。
    /// 这里统一输出“交通流量构成”：
    /// 1. 选中道路/轨道时，统计该边上的 traffic objects。
    /// 2. 选中建筑时，统计建筑周边一定范围内道路/轨道上的 traffic objects。
    /// 这样建筑面板不再出现与交通无关的建筑尺寸信息。
    /// </summary>
    public partial class TransitScopeSystem : GameSystemBase
    {
        private struct TrafficCounters
        {
            public int PersonalCars;
            public int Taxis;
            public int Cargo;
            public int PublicTransport;
            public int CityService;
            public int Bicycles;
            public int Trains;
            public int Watercraft;
            public int Aircraft;
            public int Humans;
            public int OtherVehicles;
            public int Others;

            public int Total =>
                PersonalCars +
                Taxis +
                Cargo +
                PublicTransport +
                CityService +
                Bicycles +
                Trains +
                Watercraft +
                Aircraft +
                Humans +
                OtherVehicles +
                Others;
        }

        private TransitScopeToolSystem m_ToolSystem;
        private TransitScopeUISystem m_UISystem;
        private EntityQuery m_TrafficEdgeQuery;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_ToolSystem = World.GetOrCreateSystemManaged<TransitScopeToolSystem>();
            m_UISystem = World.GetOrCreateSystemManaged<TransitScopeUISystem>();

            // 该查询只在“确认选中建筑”后使用一次，用于扫描建筑周边的道路/轨道边。
            m_TrafficEdgeQuery = GetEntityQuery(
                ComponentType.ReadOnly<Edge>(),
                ComponentType.ReadOnly<Curve>(),
                ComponentType.ReadOnly<NetSubLane>());

            Logger.Info("TransitScopeSystem 启动");
        }

        protected override void OnUpdate()
        {
            if (!m_ToolSystem.HasNewSelection)
            {
                return;
            }

            Entity selected = m_ToolSystem.SelectedEntity;
            TransitScopeToolSystem.SelectionKind kind = m_ToolSystem.SelectedKind;

            // 每次确认选中后只消费一次事件，避免每帧重复统计。
            m_ToolSystem.ClearNewSelectionFlag();

            if (selected == Entity.Null || !EntityManager.Exists(selected))
            {
                m_UISystem.ClearStats();
                return;
            }

            if (kind == TransitScopeToolSystem.SelectionKind.Road)
            {
                AnalyzeRoad(selected);
                return;
            }

            if (kind == TransitScopeToolSystem.SelectionKind.Building)
            {
                AnalyzeBuildingTraffic(selected);
            }
        }

        /// <summary>
        /// 统计单条道路/轨道上的 traffic objects。
        /// </summary>
        private void AnalyzeRoad(Entity selectedEdge)
        {
            if (!EntityManager.HasComponent<Edge>(selectedEdge))
            {
                m_UISystem.ClearStats();
                return;
            }

            TrafficCounters counters = default;
            AccumulateTrafficFromEdge(selectedEdge, ref counters);

            TransitScopeSelectionStats stats = BuildTrafficStats(
                title: "Traffic Flow",
                subtitle: $"Selected edge #{selectedEdge.Index}",
                counters);

            m_UISystem.PresentStats(stats);
        }

        /// <summary>
        /// 统计建筑周边的道路/轨道流量。
        /// 核心思路：
        /// 1. 读取建筑中心点。
        /// 2. 估算一个搜索半径。
        /// 3. 遍历所有交通边，找到位于半径内的道路/轨道。
        /// 4. 汇总这些边上的 traffic objects。
        ///
        /// 这虽然是近似方案，但语义正确：建筑面板展示的是“建筑周边交通流量”，
        /// 而不是建筑尺寸或其它无关信息。
        /// </summary>
        private void AnalyzeBuildingTraffic(Entity building)
        {
            if (!TryGetBuildingCenter(building, out float3 buildingCenter))
            {
                m_UISystem.ClearStats();
                return;
            }

            float searchRadius = EstimateBuildingTrafficSearchRadius(building);
            TrafficCounters counters = default;

            using NativeArray<Entity> edges = m_TrafficEdgeQuery.ToEntityArray(World.UpdateAllocator.ToAllocator);
            for (int i = 0; i < edges.Length; i++)
            {
                Entity edge = edges[i];

                if (!EntityManager.HasComponent<Curve>(edge))
                {
                    continue;
                }

                Curve curve = EntityManager.GetComponentData<Curve>(edge);
                float3 midpoint = EvaluateBezier(curve.m_Bezier, 0.5f);

                // 只统计建筑附近的边，避免把整张地图的交通都混进来。
                if (math.distance(midpoint, buildingCenter) > searchRadius)
                {
                    continue;
                }

                AccumulateTrafficFromEdge(edge, ref counters);
            }

            TransitScopeSelectionStats stats = BuildTrafficStats(
                title: "Building Traffic",
                subtitle: $"Nearby traffic around building #{building.Index}",
                counters);

            m_UISystem.PresentStats(stats);
        }

        /// <summary>
        /// 将某一条道路/轨道边上的 lane objects 累加到统一的交通分类统计中。
        /// </summary>
        private void AccumulateTrafficFromEdge(Entity edge, ref TrafficCounters counters)
        {
            if (!EntityManager.Exists(edge) || !EntityManager.HasBuffer<NetSubLane>(edge))
            {
                return;
            }

            DynamicBuffer<NetSubLane> lanesBuffer = EntityManager.GetBuffer<NetSubLane>(edge);
            for (int i = 0; i < lanesBuffer.Length; i++)
            {
                Entity laneEntity = lanesBuffer[i].m_SubLane;

                if (!EntityManager.Exists(laneEntity) || !EntityManager.HasBuffer<LaneObject>(laneEntity))
                {
                    continue;
                }

                DynamicBuffer<LaneObject> laneObjects = EntityManager.GetBuffer<LaneObject>(laneEntity);
                for (int j = 0; j < laneObjects.Length; j++)
                {
                    ClassifyTrafficObject(laneObjects[j].m_LaneObject, ref counters);
                }
            }
        }

        /// <summary>
        /// 按对象组件类型把 traffic object 归到对应分类。
        /// 这个分类逻辑被道路统计和建筑周边统计共同复用。
        /// </summary>
        private void ClassifyTrafficObject(Entity obj, ref TrafficCounters counters)
        {
            if (!EntityManager.Exists(obj))
            {
                return;
            }

            if (EntityManager.HasComponent<Human>(obj))
            {
                counters.Humans++;
                return;
            }

            if (EntityManager.HasComponent<Game.Vehicles.Bicycle>(obj))
            {
                counters.Bicycles++;
                return;
            }

            if (EntityManager.HasComponent<Game.Vehicles.Train>(obj))
            {
                counters.Trains++;
                return;
            }

            if (EntityManager.HasComponent<Game.Vehicles.Watercraft>(obj))
            {
                counters.Watercraft++;
                return;
            }

            if (EntityManager.HasComponent<Game.Vehicles.Aircraft>(obj) ||
                EntityManager.HasComponent<Game.Vehicles.Airplane>(obj) ||
                EntityManager.HasComponent<Game.Vehicles.Helicopter>(obj))
            {
                counters.Aircraft++;
                return;
            }

            if (EntityManager.HasComponent<Game.Vehicles.Car>(obj))
            {
                if (EntityManager.HasComponent<Game.Vehicles.PersonalCar>(obj))
                {
                    counters.PersonalCars++;
                }
                else if (EntityManager.HasComponent<Game.Vehicles.Taxi>(obj))
                {
                    counters.Taxis++;
                }
                else if (EntityManager.HasComponent<Game.Vehicles.CargoTransport>(obj) ||
                         EntityManager.HasComponent<Game.Vehicles.DeliveryTruck>(obj) ||
                         EntityManager.HasComponent<Game.Vehicles.GoodsDeliveryVehicle>(obj) ||
                         EntityManager.HasComponent<Game.Vehicles.PostVan>(obj))
                {
                    counters.Cargo++;
                }
                else if (EntityManager.HasComponent<Game.Vehicles.PublicTransport>(obj) ||
                         EntityManager.HasComponent<Game.Vehicles.PassengerTransport>(obj))
                {
                    counters.PublicTransport++;
                }
                else if (EntityManager.HasComponent<Game.Vehicles.Ambulance>(obj) ||
                         EntityManager.HasComponent<Game.Vehicles.FireEngine>(obj) ||
                         EntityManager.HasComponent<Game.Vehicles.GarbageTruck>(obj) ||
                         EntityManager.HasComponent<Game.Vehicles.Hearse>(obj) ||
                         EntityManager.HasComponent<Game.Vehicles.PoliceCar>(obj) ||
                         EntityManager.HasComponent<Game.Vehicles.RoadMaintenanceVehicle>(obj) ||
                         EntityManager.HasComponent<Game.Vehicles.MaintenanceVehicle>(obj) ||
                         EntityManager.HasComponent<Game.Vehicles.ParkMaintenanceVehicle>(obj) ||
                         EntityManager.HasComponent<Game.Vehicles.PrisonerTransport>(obj) ||
                         EntityManager.HasComponent<Game.Vehicles.EvacuatingTransport>(obj))
                {
                    counters.CityService++;
                }
                else
                {
                    counters.OtherVehicles++;
                }

                return;
            }

            if (EntityManager.HasComponent<Game.Vehicles.Vehicle>(obj))
            {
                counters.OtherVehicles++;
                return;
            }

            counters.Others++;
        }

        /// <summary>
        /// 将后端统计结果转换成前端饼图数据。
        /// 当总量为 0 时，也给一个“暂无流量”占位，避免面板完全消失导致误判为故障。
        /// </summary>
        private static TransitScopeSelectionStats BuildTrafficStats(string title, string subtitle, TrafficCounters counters)
        {
            TransitScopeSelectionStats stats = new()
            {
                Title = title,
                Subtitle = subtitle,
                Total = counters.Total
            };

            AddStat(stats, "Private Cars", counters.PersonalCars, "#5DB7FF");
            AddStat(stats, "Taxis", counters.Taxis, "#6ECF88");
            AddStat(stats, "Cargo", counters.Cargo, "#F2B35E");
            AddStat(stats, "Public Transit", counters.PublicTransport, "#8B9BFF");
            AddStat(stats, "City Service", counters.CityService, "#FF8A7A");
            AddStat(stats, "Bicycles", counters.Bicycles, "#60D5C0");
            AddStat(stats, "Rail", counters.Trains, "#C38BFF");
            AddStat(stats, "Water", counters.Watercraft, "#4FC6F0");
            AddStat(stats, "Air", counters.Aircraft, "#F28DDA");
            AddStat(stats, "Pedestrians", counters.Humans, "#D6E2F0");
            AddStat(stats, "Other Vehicles", counters.OtherVehicles, "#9AA7B6");
            AddStat(stats, "Others", counters.Others, "#738195");

            if (stats.Items.Count == 0)
            {
                AddStat(stats, "No Traffic", 1, "#7F8EA3");
                stats.Total = 1;
            }

            return stats;
        }

        /// <summary>
        /// 读取建筑中心位置。
        /// 优先使用 Transform；这是判断“建筑附近交通”的基础。
        /// </summary>
        private bool TryGetBuildingCenter(Entity building, out float3 center)
        {
            center = float3.zero;

            if (EntityManager.HasComponent<Transform>(building))
            {
                center = EntityManager.GetComponentData<Transform>(building).m_Position;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 估算建筑周边交通搜索半径。
        /// 优先基于 prefab 的地块尺寸，再叠加一个基础缓冲值。
        /// 这样小建筑不会扫太远，大建筑也不会只扫到门口那一小段路。
        /// </summary>
        private float EstimateBuildingTrafficSearchRadius(Entity building)
        {
            float radius = 32f;

            if (EntityManager.HasComponent<PrefabRefData>(building))
            {
                PrefabRefData prefabRef = EntityManager.GetComponentData<PrefabRefData>(building);
                Entity prefab = prefabRef.m_Prefab;

                if (prefab != Entity.Null && EntityManager.Exists(prefab))
                {
                    if (EntityManager.HasComponent<BuildingExtensionData>(prefab))
                    {
                        BuildingExtensionData ext = EntityManager.GetComponentData<BuildingExtensionData>(prefab);
                        radius = math.max(radius, math.max(ext.m_LotSize.x, ext.m_LotSize.y) * 10f);
                    }
                    else if (EntityManager.HasComponent<BuildingData>(prefab))
                    {
                        BuildingData data = EntityManager.GetComponentData<BuildingData>(prefab);
                        radius = math.max(radius, math.max(data.m_LotSize.x, data.m_LotSize.y) * 10f);
                    }
                }
            }

            if (EntityManager.HasComponent<CullingInfo>(building))
            {
                CullingInfo cullingInfo = EntityManager.GetComponentData<CullingInfo>(building);
                float3 boundsSize = cullingInfo.m_Bounds.max - cullingInfo.m_Bounds.min;
                radius = math.max(radius, math.max(boundsSize.x, boundsSize.z) * 1.5f);
            }

            return radius;
        }

        /// <summary>
        /// 计算 Bezier 曲线在给定 t 位置的采样点。
        /// 这里用 t=0.5 的中点近似判断一条边是否位于建筑附近，足够稳定且计算量低。
        /// </summary>
        private static float3 EvaluateBezier(Bezier4x3 bezier, float t)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            return uuu * bezier.a +
                   3f * uu * t * bezier.b +
                   3f * u * tt * bezier.c +
                   ttt * bezier.d;
        }

        /// <summary>
        /// 只把正数统计项加入饼图，避免图例出现大量 0 值。
        /// </summary>
        private static void AddStat(TransitScopeSelectionStats stats, string label, int value, string color)
        {
            if (value <= 0)
            {
                return;
            }

            stats.Items.Add(new TransitScopeStatItem
            {
                Label = label,
                Value = value,
                Color = color
            });
        }
    }
}
