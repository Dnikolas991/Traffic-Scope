using Game;
using Game.Net;
using Game.Vehicles;
using Game.Creatures;
using Unity.Collections;
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

            // 这一帧的选择事件立刻消费掉
            m_ToolSystem.ClearNewSelectionFlag();

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
                Logger.Info("当前确认对象不是道路 Edge");
                return;
            }

            Logger.Info($"当前确认道路: {selectedEdge.Index}");

            NativeArray<ComponentType> components = EntityManager.GetComponentTypes(selectedEdge);
            string compList = "";
            for (int i = 0; i < components.Length; i++)
            {
                var managedType = components[i].GetManagedType();
                compList += managedType != null ? managedType.Name : components[i].ToString();

                if (i < components.Length - 1)
                {
                    compList += ", ";
                }
            }
            components.Dispose();

            Logger.Info($"[组件列表] {compList}");

            if (!EntityManager.HasBuffer<SubLane>(selectedEdge))
            {
                Logger.Info("[警告] 该道路没有 SubLane 缓冲区");
                Logger.Info("================================================");
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

            Logger.Info($"[数据] 总流量: {total} | 私家车: {personalCarCount} | 出租车: {taxiCount} | 货运: {cargoCount}");
            Logger.Info($"[数据] 公交客运: {publicTransportCount} | 城市服务: {cityServiceCount} | 行人/自行车: {humanCount} | 其它: {otherCount}");
            Logger.Info("================================================");
        }
    }
}