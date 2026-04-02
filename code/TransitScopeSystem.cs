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
    /// 
    /// 本次重构严格对齐原版交通线路口径：
    /// 1. 道路统计 = 当前在该路段上的流量 (OnRouteNow) + 路径规划中即将经过的流量 (PlannedToPass)。
    /// 2. 建筑统计 = 目标设定为该建筑的流入量 (Destination Inflow)。
    /// </summary>
    public partial class TransitScopeSystem : GameSystemBase
    {
        private TransitScopeToolSystem m_ToolSystem;
        private TransitScopeUISystem m_UISystem;
        private TransitScopeTrafficFlowSystem m_FlowSystem;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_ToolSystem = World.GetOrCreateSystemManaged<TransitScopeToolSystem>();
            m_UISystem = World.GetOrCreateSystemManaged<TransitScopeUISystem>();
            m_FlowSystem = World.GetOrCreateSystemManaged<TransitScopeTrafficFlowSystem>();

            Logger.Info("TransitScopeSystem 启动，已对接全图流量缓存系统");
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
            
            // 1. OnRouteNow: 当前物理位置正在该道路上的实体
            AccumulateTrafficFromEdge(selectedEdge, ref counters);

            // 2. PlannedToPass: 路径规划中未来将要经过该道路的实体（从缓存读取）
            if (m_FlowSystem.PlannedFlowCache.IsCreated && 
                m_FlowSystem.PlannedFlowCache.TryGetValue(selectedEdge, out TrafficCounters plannedFlow))
            {
                counters.Add(plannedFlow);
            }

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
            TrafficCounters counters = default;

            // 原版口径：建筑流量仅统计“正在前往该建筑”的目标流入量。
            // 抛弃了过去粗暴的“按半径扫描周围道路”的错误做法。
            if (m_FlowSystem.PlannedFlowCache.IsCreated && 
                m_FlowSystem.PlannedFlowCache.TryGetValue(building, out TrafficCounters inflow))
            {
                counters.Add(inflow);
            }

            TransitScopeSelectionStats stats = BuildTrafficStats(
                titleKey: "stats.title.building",
                fallbackTitle: "Building Traffic",
                subtitleKey: "stats.subtitle.nearby_building",
                subtitleArg: building.Index.ToString(),
                fallbackSubtitle: $"Inflow traffic for building #{building.Index}",
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
