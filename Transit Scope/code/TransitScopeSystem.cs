using Game;
using Game.Creatures;
using Game.Net;
using Game.Prefabs;
using Game.Vehicles;
using Unity.Entities;
using NetSubLane = Game.Net.SubLane;
using PrefabRefData = Game.Prefabs.PrefabRef;

namespace Transit_Scope.code
{
    /// <summary>
    /// 负责在玩家确认选中对象后做统计分析，并把结果推给前端图形化展示。
    /// </summary>
    public partial class TransitScopeSystem : GameSystemBase
    {
        private TransitScopeToolSystem m_ToolSystem;
        private TransitScopeUISystem m_UISystem;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_ToolSystem = World.GetOrCreateSystemManaged<TransitScopeToolSystem>();
            m_UISystem = World.GetOrCreateSystemManaged<TransitScopeUISystem>();
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

            // 只消费一次，避免每帧重复计算和重复刷新 UI。
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
                AnalyzeBuilding(selected);
            }
        }

        /// <summary>
        /// 统计道路/轨道上的对象构成，并转成饼图数据。
        /// 用户强调“占比很重要”，所以这里会把各类交通对象拆得比较细。
        /// </summary>
        private void AnalyzeRoad(Entity selectedEdge)
        {
            if (!EntityManager.HasComponent<Edge>(selectedEdge))
            {
                m_UISystem.ClearStats();
                return;
            }

            if (!EntityManager.HasBuffer<NetSubLane>(selectedEdge))
            {
                m_UISystem.PresentStats(new TransitScopeSelectionStats
                {
                    Title = "道路流量统计",
                    Subtitle = $"实体 #{selectedEdge.Index}",
                    Total = 0
                });
                return;
            }

            DynamicBuffer<NetSubLane> lanesBuffer = EntityManager.GetBuffer<NetSubLane>(selectedEdge);

            int personalCarCount = 0;
            int taxiCount = 0;
            int cargoCount = 0;
            int publicTransportCount = 0;
            int cityServiceCount = 0;
            int bicycleCount = 0;
            int trainCount = 0;
            int watercraftCount = 0;
            int aircraftCount = 0;
            int humanCount = 0;
            int otherVehicleCount = 0;
            int otherCount = 0;

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
                    Entity obj = laneObjects[j].m_LaneObject;

                    if (!EntityManager.Exists(obj))
                    {
                        continue;
                    }

                    if (EntityManager.HasComponent<Human>(obj))
                    {
                        humanCount++;
                        continue;
                    }

                    if (EntityManager.HasComponent<Game.Vehicles.Bicycle>(obj))
                    {
                        bicycleCount++;
                        continue;
                    }

                    if (EntityManager.HasComponent<Game.Vehicles.Train>(obj))
                    {
                        trainCount++;
                        continue;
                    }

                    if (EntityManager.HasComponent<Game.Vehicles.Watercraft>(obj))
                    {
                        watercraftCount++;
                        continue;
                    }

                    if (EntityManager.HasComponent<Game.Vehicles.Aircraft>(obj) ||
                        EntityManager.HasComponent<Game.Vehicles.Airplane>(obj) ||
                        EntityManager.HasComponent<Game.Vehicles.Helicopter>(obj))
                    {
                        aircraftCount++;
                        continue;
                    }

                    if (EntityManager.HasComponent<Game.Vehicles.Car>(obj))
                    {
                        if (EntityManager.HasComponent<Game.Vehicles.PersonalCar>(obj))
                        {
                            personalCarCount++;
                        }
                        else if (EntityManager.HasComponent<Game.Vehicles.Taxi>(obj))
                        {
                            taxiCount++;
                        }
                        else if (EntityManager.HasComponent<Game.Vehicles.CargoTransport>(obj) ||
                                 EntityManager.HasComponent<Game.Vehicles.DeliveryTruck>(obj) ||
                                 EntityManager.HasComponent<Game.Vehicles.GoodsDeliveryVehicle>(obj) ||
                                 EntityManager.HasComponent<Game.Vehicles.PostVan>(obj))
                        {
                            cargoCount++;
                        }
                        else if (EntityManager.HasComponent<Game.Vehicles.PublicTransport>(obj) ||
                                 EntityManager.HasComponent<Game.Vehicles.PassengerTransport>(obj))
                        {
                            publicTransportCount++;
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
                            cityServiceCount++;
                        }
                        else
                        {
                            otherVehicleCount++;
                        }

                        continue;
                    }

                    if (EntityManager.HasComponent<Game.Vehicles.Vehicle>(obj))
                    {
                        otherVehicleCount++;
                        continue;
                    }

                    otherCount++;
                }
            }

            int totalTraffic =
                personalCarCount +
                taxiCount +
                cargoCount +
                publicTransportCount +
                cityServiceCount +
                bicycleCount +
                trainCount +
                watercraftCount +
                aircraftCount +
                humanCount +
                otherVehicleCount +
                otherCount;

            TransitScopeSelectionStats stats = new()
            {
                Title = "道路流量统计",
                Subtitle = $"实体 #{selectedEdge.Index}",
                Total = totalTraffic
            };

            AddStat(stats, "私家车", personalCarCount, "#5DB7FF");
            AddStat(stats, "出租车", taxiCount, "#6ECF88");
            AddStat(stats, "货运物流", cargoCount, "#F2B35E");
            AddStat(stats, "公共交通", publicTransportCount, "#8B9BFF");
            AddStat(stats, "城市服务", cityServiceCount, "#FF8A7A");
            AddStat(stats, "自行车", bicycleCount, "#60D5C0");
            AddStat(stats, "轨道交通", trainCount, "#C38BFF");
            AddStat(stats, "水上交通", watercraftCount, "#4FC6F0");
            AddStat(stats, "航空交通", aircraftCount, "#F28DDA");
            AddStat(stats, "行人", humanCount, "#D6E2F0");
            AddStat(stats, "其它车辆", otherVehicleCount, "#9AA7B6");
            AddStat(stats, "其它对象", otherCount, "#738195");

            m_UISystem.PresentStats(stats);
        }

        /// <summary>
        /// 建筑当前先展示基础概览信息。
        /// 虽然不是交通流量，但依然以“占比”方式组织成饼图，方便后续继续扩展。
        /// </summary>
        private void AnalyzeBuilding(Entity building)
        {
            int lotWidth = 0;
            int lotDepth = 0;
            int subObjectCount = EntityManager.HasBuffer<Game.Objects.SubObject>(building)
                ? EntityManager.GetBuffer<Game.Objects.SubObject>(building).Length
                : 0;
            int prefabIndex = 0;

            if (EntityManager.HasComponent<PrefabRefData>(building))
            {
                PrefabRefData prefabRef = EntityManager.GetComponentData<PrefabRefData>(building);
                prefabIndex = prefabRef.m_Prefab.Index;

                if (prefabRef.m_Prefab != Entity.Null && EntityManager.Exists(prefabRef.m_Prefab))
                {
                    if (EntityManager.HasComponent<BuildingExtensionData>(prefabRef.m_Prefab))
                    {
                        BuildingExtensionData ext = EntityManager.GetComponentData<BuildingExtensionData>(prefabRef.m_Prefab);
                        lotWidth = ext.m_LotSize.x;
                        lotDepth = ext.m_LotSize.y;
                    }
                    else if (EntityManager.HasComponent<BuildingData>(prefabRef.m_Prefab))
                    {
                        BuildingData data = EntityManager.GetComponentData<BuildingData>(prefabRef.m_Prefab);
                        lotWidth = data.m_LotSize.x;
                        lotDepth = data.m_LotSize.y;
                    }
                }
            }

            TransitScopeSelectionStats stats = new()
            {
                Title = "建筑概览",
                Subtitle = $"实体 #{building.Index}" + (prefabIndex > 0 ? $" · Prefab #{prefabIndex}" : string.Empty),
                Total = lotWidth + lotDepth + subObjectCount
            };

            AddStat(stats, "地块宽度", lotWidth, "#5DB7FF");
            AddStat(stats, "地块深度", lotDepth, "#6ECF88");
            AddStat(stats, "子对象", subObjectCount, "#F2B35E");

            if (stats.Items.Count == 0)
            {
                AddStat(stats, "基础信息", 1, "#7F8EA3");
                stats.Total = 1;
            }

            m_UISystem.PresentStats(stats);
        }

        /// <summary>
        /// 只把正数统计项加入饼图，避免图例里堆满 0 值噪音。
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
