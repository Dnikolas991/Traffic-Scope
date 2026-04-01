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
    /// 同时只发送本地化键和少量参数给前端，真正的翻译由前端按当前语言完成。
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

            // 只在确认选中建筑后使用，用于扫描建筑周边道路/轨道边。
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
                titleKey: "stats.title.road",
                fallbackTitle: "Traffic Flow",
                subtitleKey: "stats.subtitle.selected_edge",
                subtitleArg: selectedEdge.Index.ToString(),
                fallbackSubtitle: $"Selected edge #{selectedEdge.Index}",
                counters);

            m_UISystem.PresentStats(stats);
        }

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

                if (math.distance(midpoint, buildingCenter) > searchRadius)
                {
                    continue;
                }

                AccumulateTrafficFromEdge(edge, ref counters);
            }

            TransitScopeSelectionStats stats = BuildTrafficStats(
                titleKey: "stats.title.building",
                fallbackTitle: "Building Traffic",
                subtitleKey: "stats.subtitle.nearby_building",
                subtitleArg: building.Index.ToString(),
                fallbackSubtitle: $"Nearby traffic around building #{building.Index}",
                counters);

            m_UISystem.PresentStats(stats);
        }

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
        /// 把后端统计结果转换成前端饼图数据。
        /// 没有流量时保留一个占位扇区保证图表可见，但 displayTotal 仍然显示 0。
        /// </summary>
        private static TransitScopeSelectionStats BuildTrafficStats(
            string titleKey,
            string fallbackTitle,
            string subtitleKey,
            string subtitleArg,
            string fallbackSubtitle,
            TrafficCounters counters)
        {
            TransitScopeSelectionStats stats = new()
            {
                TitleKey = titleKey,
                Title = fallbackTitle,
                SubtitleKey = subtitleKey,
                SubtitleArg = subtitleArg,
                Subtitle = fallbackSubtitle,
                Total = counters.Total,
                DisplayTotal = counters.Total
            };

            AddStat(stats, "stats.item.private_cars", "Private Cars", counters.PersonalCars, "#5DB7FF");
            AddStat(stats, "stats.item.taxis", "Taxis", counters.Taxis, "#6ECF88");
            AddStat(stats, "stats.item.cargo", "Cargo", counters.Cargo, "#F2B35E");
            AddStat(stats, "stats.item.public_transit", "Public Transit", counters.PublicTransport, "#8B9BFF");
            AddStat(stats, "stats.item.city_service", "City Service", counters.CityService, "#FF8A7A");
            AddStat(stats, "stats.item.bicycles", "Bicycles", counters.Bicycles, "#60D5C0");
            AddStat(stats, "stats.item.rail", "Rail", counters.Trains, "#C38BFF");
            AddStat(stats, "stats.item.water", "Water", counters.Watercraft, "#4FC6F0");
            AddStat(stats, "stats.item.air", "Air", counters.Aircraft, "#F28DDA");
            AddStat(stats, "stats.item.pedestrians", "Pedestrians", counters.Humans, "#D6E2F0");
            AddStat(stats, "stats.item.other_vehicles", "Other Vehicles", counters.OtherVehicles, "#9AA7B6");
            AddStat(stats, "stats.item.others", "Others", counters.Others, "#738195");

            if (stats.Items.Count == 0)
            {
                AddStat(stats, "stats.item.no_traffic", "No Traffic", 1, "#7F8EA3");
                stats.Total = 1;
                stats.DisplayTotal = 0;
            }

            return stats;
        }

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

        private static void AddStat(TransitScopeSelectionStats stats, string labelKey, string fallbackLabel, int value, string color)
        {
            if (value <= 0)
            {
                return;
            }

            stats.Items.Add(new TransitScopeStatItem
            {
                LabelKey = labelKey,
                Label = fallbackLabel,
                Value = value,
                Color = color
            });
        }
    }
}
