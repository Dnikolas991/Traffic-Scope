using Game;
using Game.Net;
using Game.Vehicles;
using Game.Creatures;
using Unity.Collections;
using Unity.Entities;

namespace Transit_Scope
{
    public partial class TransitScopeSystem : GameSystemBase
    {
        private TransitScopeToolSystem m_TransitScopeToolSystem;
        private Entity m_LastSelected = Entity.Null;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_TransitScopeToolSystem = World.GetOrCreateSystemManaged<TransitScopeToolSystem>();
            Logger.Info("TransitScopeSystem 启动");
        }

        protected override void OnUpdate()
        {
            Entity selectedEntity = m_TransitScopeToolSystem.SelectedEntity;

            if (selectedEntity == Entity.Null || selectedEntity == m_LastSelected)
                return;

            if (!EntityManager.Exists(selectedEntity))
                return;

            m_LastSelected = selectedEntity;
            Logger.Info($"当前选择实体: {selectedEntity.Index}");

            NativeArray<ComponentType> components = EntityManager.GetComponentTypes(selectedEntity);
            string compList = "";
            for (int i = 0; i < components.Length; i++)
            {
                var managedType = components[i].GetManagedType();
                compList += managedType != null ? managedType.Name : components[i].ToString();
                if (i < components.Length - 1)
                    compList += ", ";
            }
            components.Dispose();

            Logger.Info($"[组件列表] {compList}");

            if (!EntityManager.HasComponent<Edge>(selectedEntity))
            {
                Logger.Info("[分析] 当前选中实体不是 Edge");
                Logger.Info($"================================================\n");
                return;
            }

            Logger.Info("[分析] 确认为路段 (Edge)！开始统计流量...");

            if (!EntityManager.HasBuffer<SubLane>(selectedEntity))
            {
                Logger.Info("[警告] 该路段没有 SubLane 缓冲区！");
                Logger.Info($"================================================\n");
                return;
            }

            DynamicBuffer<SubLane> lanesBuffer = EntityManager.GetBuffer<SubLane>(selectedEntity);

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

                if (!EntityManager.Exists(laneEntity) || !EntityManager.HasBuffer<LaneObject>(laneEntity))
                    continue;

                DynamicBuffer<LaneObject> laneObjects = EntityManager.GetBuffer<LaneObject>(laneEntity);

                for (int j = 0; j < laneObjects.Length; j++)
                {
                    Entity obj = laneObjects[j].m_LaneObject;

                    if (!EntityManager.Exists(obj))
                        continue;

                    if (EntityManager.HasComponent<Human>(obj))
                    {
                        humanCount++;
                    }
                    else if (EntityManager.HasComponent<Car>(obj))
                    {
                        if (EntityManager.HasComponent<PersonalCar>(obj)) personalCarCount++;
                        else if (EntityManager.HasComponent<Taxi>(obj)) taxiCount++;
                        else if (EntityManager.HasComponent<CargoTransport>(obj) ||
                                 EntityManager.HasComponent<DeliveryTruck>(obj) ||
                                 EntityManager.HasComponent<GoodsDeliveryVehicle>(obj) ||
                                 EntityManager.HasComponent<PostVan>(obj)) cargoCount++;
                        else if (EntityManager.HasComponent<PublicTransport>(obj) ||
                                 EntityManager.HasComponent<PassengerTransport>(obj)) publicTransportCount++;
                        else if (EntityManager.HasComponent<Ambulance>(obj) ||
                                 EntityManager.HasComponent<PoliceCar>(obj) ||
                                 EntityManager.HasComponent<FireEngine>(obj) ||
                                 EntityManager.HasComponent<GarbageTruck>(obj) ||
                                 EntityManager.HasComponent<Hearse>(obj) ||
                                 EntityManager.HasComponent<RoadMaintenanceVehicle>(obj)) cityServiceCount++;
                        else otherCount++;
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
            Logger.Info($"================================================\n");
        }
    }
}