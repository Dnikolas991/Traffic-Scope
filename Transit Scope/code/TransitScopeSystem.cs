using Game;
using Game.Creatures;
using Game.Net;
using Game.Prefabs;
using Game.Rendering;
using Game.Vehicles;
using Unity.Entities;
using Unity.Mathematics;
using NetSubLane = Game.Net.SubLane;
using PrefabRefData = Game.Prefabs.PrefabRef;

namespace Transit_Scope.code
{
    /// <summary>
    /// 负责在玩家确认选中对象后做统计分析，并把结果推给前端图形化展示。
    /// 道路输出交通流量分类，建筑输出建筑本体信息，二者彻底分开。
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

            // 每次确认选中后只统计一次，避免每帧重复分析。
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
        /// 统计道路/轨道上的对象构成，并转换成饼图数据。
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
        /// 建筑不再复用车流统计。
        /// 这里改成输出真实的建筑信息构成，包括地块尺寸、模型体量和子对象数量。
        /// 这些数据都来自当前建筑实体或其 prefab，而不是道路上的交通对象。
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

            int footprintArea = math.max(0, lotWidth * lotDepth);
            int modelWidth = 0;
            int modelDepth = 0;
            int modelHeight = 0;

            // 使用渲染包围盒近似读取建筑模型尺寸。
            // 这是建筑本体几何信息，不是交通流量。
            if (EntityManager.HasComponent<CullingInfo>(building))
            {
                CullingInfo cullingInfo = EntityManager.GetComponentData<CullingInfo>(building);
                float3 size = cullingInfo.m_Bounds.max - cullingInfo.m_Bounds.min;
                modelWidth = math.max(0, (int)math.round(size.x));
                modelHeight = math.max(0, (int)math.round(size.y));
                modelDepth = math.max(0, (int)math.round(size.z));
            }

            TransitScopeSelectionStats stats = new()
            {
                Title = "建筑信息概览",
                Subtitle = $"实体 #{building.Index}" + (prefabIndex > 0 ? $" · Prefab #{prefabIndex}" : string.Empty),
                Total = 0
            };

            AddStat(stats, "占地面积", footprintArea, "#5DB7FF");
            AddStat(stats, "模型高度", modelHeight, "#6ECF88");
            AddStat(stats, "模型宽度", modelWidth, "#F2B35E");
            AddStat(stats, "模型进深", modelDepth, "#8B9BFF");
            AddStat(stats, "子对象", subObjectCount, "#FF8A7A");
            AddStat(stats, "地块宽度", lotWidth, "#60D5C0");
            AddStat(stats, "地块深度", lotDepth, "#C38BFF");

            if (stats.Items.Count == 0)
            {
                AddStat(stats, "基础信息", 1, "#7F8EA3");
            }

            // 建筑总量按当前展示项目之和计算，方便前端直接计算占比。
            int total = 0;
            for (int i = 0; i < stats.Items.Count; i++)
            {
                total += stats.Items[i].Value;
            }

            stats.Total = math.max(1, total);
            m_UISystem.PresentStats(stats);
        }

        /// <summary>
        /// 只把正数统计项加入饼图，避免图例里出现大量 0 值噪音。
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
