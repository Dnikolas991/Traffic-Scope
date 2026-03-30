using Game;
using Game.Creatures;
using Game.Net;
using Game.Vehicles;
using Unity.Entities;

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

            Entity selectedEdge = m_ToolSystem.SelectedEdge;

            // 这一帧的“新选择事件”消费掉
            m_ToolSystem.ClearNewSelectionFlag();

            // 取消选择时，这里直接返回，不做流量分析
            if (selectedEdge == Entity.Null)
            {
                return;
            }

            if (!EntityManager.Exists(selectedEdge))
            {
                return;
            }

            if (!EntityManager.HasComponent<Edge>(selectedEdge))
            {
                return;
            }

            if (!EntityManager.HasBuffer<SubLane>(selectedEdge))
            {
                Logger.Info($"道路 #{selectedEdge.Index} 没有 SubLane 数据");
                return;
            }

            DynamicBuffer<SubLane> lanesBuffer = EntityManager.GetBuffer<SubLane>(selectedEdge);

            int personalCarCount = 0;
            int taxiCount = 0;
            int cargoCount = 0;
            int publicTransportCount = 0;
            int cityServiceCount = 0;
            int humanCount = 0;
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

                    if (EntityManager.HasComponent<Human>(obj))
                    {
                        humanCount++;
                    }
                    else if (EntityManager.HasComponent<Car>(obj))
                    {
                        if (EntityManager.HasComponent<PersonalCar>(obj))
                        {
                            personalCarCount++;
                        }
                        else if (EntityManager.HasComponent<Taxi>(obj))
                        {
                            taxiCount++;
                        }
                        else if (EntityManager.HasComponent<CargoTransport>(obj) ||
                                 EntityManager.HasComponent<DeliveryTruck>(obj) ||
                                 EntityManager.HasComponent<GoodsDeliveryVehicle>(obj) ||
                                 EntityManager.HasComponent<PostVan>(obj))
                        {
                            cargoCount++;
                        }
                        else if (EntityManager.HasComponent<PublicTransport>(obj) ||
                                 EntityManager.HasComponent<PassengerTransport>(obj))
                        {
                            publicTransportCount++;
                        }
                        else if (EntityManager.HasComponent<Ambulance>(obj) ||
                                 EntityManager.HasComponent<PoliceCar>(obj) ||
                                 EntityManager.HasComponent<FireEngine>(obj) ||
                                 EntityManager.HasComponent<GarbageTruck>(obj) ||
                                 EntityManager.HasComponent<Hearse>(obj) ||
                                 EntityManager.HasComponent<RoadMaintenanceVehicle>(obj))
                        {
                            cityServiceCount++;
                        }
                        else
                        {
                            otherCount++;
                        }
                    }
                    else if (EntityManager.HasComponent<Bicycle>(obj))
                    {
                        humanCount++;
                    }
                    else
                    {
                        otherCount++;
                    }
                }
            }

            int total = personalCarCount + taxiCount + cargoCount + publicTransportCount + cityServiceCount + humanCount + otherCount;

            Logger.Info(
                $"[TransitScope] 道路 #{selectedEdge.Index} | 总流量:{total} | 私家车:{personalCarCount} | 出租车:{taxiCount} | 货运:{cargoCount} | 公交客运:{publicTransportCount} | 城市服务:{cityServiceCount} | 行人/自行车:{humanCount} | 其它:{otherCount}"
            );
        }
    }
}