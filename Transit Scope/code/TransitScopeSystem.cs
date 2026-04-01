using Game;
using Game.Creatures;
using Game.Net;
using Game.Vehicles;
using Unity.Entities;
using NetSubLane = Game.Net.SubLane;
using PrefabRefData = Game.Prefabs.PrefabRef;

namespace Transit_Scope.code
{
    public partial class TransitScopeSystem : GameSystemBase
    {
        private TransitScopeToolSystem m_ToolSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolSystem = World.GetOrCreateSystemManaged<TransitScopeToolSystem>();
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

            // 消费本帧新选择事件
            m_ToolSystem.ClearNewSelectionFlag();

            if (selected == Entity.Null || !EntityManager.Exists(selected))
            {
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

        private void AnalyzeRoad(Entity selectedEdge)
        {
            if (!EntityManager.HasComponent<Edge>(selectedEdge))
            {
                return;
            }

            if (!EntityManager.HasBuffer<NetSubLane>(selectedEdge))
            {
                Logger.Info($"[TransitScope] 道路 #{selectedEdge.Index} 没有 SubLane 数据");
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

                if (!EntityManager.Exists(laneEntity))
                {
                    continue;
                }

                if (!EntityManager.HasBuffer<LaneObject>(laneEntity))
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

                    // 1) 行人
                    if (EntityManager.HasComponent<Human>(obj))
                    {
                        humanCount++;
                        continue;
                    }

                    // 2) 自行车
                    if (EntityManager.HasComponent<Game.Vehicles.Bicycle>(obj))
                    {
                        bicycleCount++;
                        continue;
                    }

                    // 3) 轨道交通
                    if (EntityManager.HasComponent<Game.Vehicles.Train>(obj))
                    {
                        trainCount++;
                        continue;
                    }

                    // 4) 水上交通
                    if (EntityManager.HasComponent<Game.Vehicles.Watercraft>(obj))
                    {
                        watercraftCount++;
                        continue;
                    }

                    // 5) 航空交通
                    if (EntityManager.HasComponent<Game.Vehicles.Aircraft>(obj) ||
                        EntityManager.HasComponent<Game.Vehicles.Airplane>(obj) ||
                        EntityManager.HasComponent<Game.Vehicles.Helicopter>(obj))
                    {
                        aircraftCount++;
                        continue;
                    }

                    // 6) 道路车辆体系
                    if (EntityManager.HasComponent<Game.Vehicles.Car>(obj))
                    {
                        // 私家车
                        if (EntityManager.HasComponent<Game.Vehicles.PersonalCar>(obj))
                        {
                            personalCarCount++;
                        }
                        // 出租车
                        else if (EntityManager.HasComponent<Game.Vehicles.Taxi>(obj))
                        {
                            taxiCount++;
                        }
                        // 货运 / 物流
                        else if (EntityManager.HasComponent<Game.Vehicles.CargoTransport>(obj) ||
                                 EntityManager.HasComponent<Game.Vehicles.DeliveryTruck>(obj) ||
                                 EntityManager.HasComponent<Game.Vehicles.GoodsDeliveryVehicle>(obj) ||
                                 EntityManager.HasComponent<Game.Vehicles.PostVan>(obj))
                        {
                            cargoCount++;
                        }
                        // 公共交通
                        else if (EntityManager.HasComponent<Game.Vehicles.PublicTransport>(obj) ||
                                 EntityManager.HasComponent<Game.Vehicles.PassengerTransport>(obj))
                        {
                            publicTransportCount++;
                        }
                        // 城市服务车辆
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
                        // 其它汽车类
                        else
                        {
                            otherVehicleCount++;
                        }

                        continue;
                    }

                    // 7) 兜底：有 Vehicle 但不属于上面明确分类
                    if (EntityManager.HasComponent<Game.Vehicles.Vehicle>(obj))
                    {
                        otherVehicleCount++;
                        continue;
                    }

                    // 8) 其它未知对象
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

            Logger.Info(
                $"[TransitScope][Road #{selectedEdge.Index}] " +
                $"总流量:{totalTraffic} | " +
                $"私家车:{personalCarCount} | " +
                $"出租车:{taxiCount} | " +
                $"货运物流:{cargoCount} | " +
                $"公共交通:{publicTransportCount} | " +
                $"城市服务:{cityServiceCount} | " +
                $"自行车:{bicycleCount} | " +
                $"轨道交通:{trainCount} | " +
                $"水上交通:{watercraftCount} | " +
                $"航空交通:{aircraftCount} | " +
                $"行人:{humanCount} | " +
                $"其它车辆:{otherVehicleCount} | " +
                $"其它对象:{otherCount}"
            );
        }

        private void AnalyzeBuilding(Entity building)
        {
            if (EntityManager.HasComponent<PrefabRefData>(building))
            {
                PrefabRefData prefabRef = EntityManager.GetComponentData<PrefabRefData>(building);
                Logger.Info($"[TransitScope][Building #{building.Index}] 已选中建筑，Prefab 实体 #{prefabRef.m_Prefab.Index}");
            }
            else
            {
                Logger.Info($"[TransitScope][Building #{building.Index}] 已选中建筑");
            }
        }
    }
}