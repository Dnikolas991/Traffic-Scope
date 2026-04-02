using Game.Creatures;
using Unity.Entities;

namespace Transit_Scope.code
{
    /// <summary>
    /// 统一交通分类口径。
    /// </summary>
    internal static class ScopeTrafficClassifier
    {
        public static TrafficCounters ClassifySingle(EntityManager entityManager, Entity entity)
        {
            TrafficCounters counters = default;
            AddEntity(entityManager, entity, ref counters);
            return counters;
        }

        public static void AddEntity(EntityManager entityManager, Entity entity, ref TrafficCounters counters)
        {
            if (entity == Entity.Null || !entityManager.Exists(entity))
            {
                return;
            }

            if (entityManager.HasComponent<Human>(entity))
            {
                counters.Humans++;
                return;
            }

            if (entityManager.HasComponent<Game.Vehicles.Bicycle>(entity))
            {
                counters.Bicycles++;
                return;
            }

            if (entityManager.HasComponent<Game.Vehicles.Train>(entity))
            {
                counters.Trains++;
                return;
            }

            if (entityManager.HasComponent<Game.Vehicles.Watercraft>(entity))
            {
                counters.Watercraft++;
                return;
            }

            if (entityManager.HasComponent<Game.Vehicles.Aircraft>(entity) ||
                entityManager.HasComponent<Game.Vehicles.Airplane>(entity) ||
                entityManager.HasComponent<Game.Vehicles.Helicopter>(entity))
            {
                counters.Aircraft++;
                return;
            }

            if (entityManager.HasComponent<Game.Vehicles.Car>(entity))
            {
                if (entityManager.HasComponent<Game.Vehicles.PersonalCar>(entity))
                {
                    counters.PersonalCars++;
                }
                else if (entityManager.HasComponent<Game.Vehicles.Taxi>(entity))
                {
                    counters.Taxis++;
                }
                else if (entityManager.HasComponent<Game.Vehicles.CargoTransport>(entity) ||
                         entityManager.HasComponent<Game.Vehicles.DeliveryTruck>(entity) ||
                         entityManager.HasComponent<Game.Vehicles.GoodsDeliveryVehicle>(entity) ||
                         entityManager.HasComponent<Game.Vehicles.PostVan>(entity))
                {
                    counters.Cargo++;
                }
                else if (entityManager.HasComponent<Game.Vehicles.PublicTransport>(entity) ||
                         entityManager.HasComponent<Game.Vehicles.PassengerTransport>(entity))
                {
                    counters.PublicTransport++;
                }
                else if (entityManager.HasComponent<Game.Vehicles.Ambulance>(entity) ||
                         entityManager.HasComponent<Game.Vehicles.FireEngine>(entity) ||
                         entityManager.HasComponent<Game.Vehicles.GarbageTruck>(entity) ||
                         entityManager.HasComponent<Game.Vehicles.Hearse>(entity) ||
                         entityManager.HasComponent<Game.Vehicles.PoliceCar>(entity) ||
                         entityManager.HasComponent<Game.Vehicles.RoadMaintenanceVehicle>(entity) ||
                         entityManager.HasComponent<Game.Vehicles.MaintenanceVehicle>(entity) ||
                         entityManager.HasComponent<Game.Vehicles.ParkMaintenanceVehicle>(entity) ||
                         entityManager.HasComponent<Game.Vehicles.PrisonerTransport>(entity) ||
                         entityManager.HasComponent<Game.Vehicles.EvacuatingTransport>(entity))
                {
                    counters.CityService++;
                }
                else
                {
                    counters.OtherVehicles++;
                }

                return;
            }

            if (entityManager.HasComponent<Game.Vehicles.Vehicle>(entity))
            {
                counters.OtherVehicles++;
                return;
            }

            counters.Others++;
        }
    }
}
