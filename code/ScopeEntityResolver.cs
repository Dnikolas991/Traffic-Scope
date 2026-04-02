using Unity.Entities;

namespace Transit_Scope.code
{
    /// <summary>
    /// 统一处理命中实体到业务实体的解析逻辑。
    /// 
    /// 选择工具、路径分析和 Overlay 都会碰到子实体、Temp 实体和 Owner 链，
    /// 这里负责把它们还原成真正的道路边或建筑实体。
    /// </summary>
    internal static class ScopeEntityResolver
    {
        private const int MaxOwnerTraversalDepth = 8;

        public static bool IsRoad(EntityManager entityManager, Entity entity)
        {
            return entity != Entity.Null &&
                   entityManager.Exists(entity) &&
                   entityManager.HasComponent<Game.Net.Edge>(entity);
        }

        public static bool IsBuilding(EntityManager entityManager, Entity entity)
        {
            if (entity == Entity.Null || !entityManager.Exists(entity))
            {
                return false;
            }

            if (entityManager.HasComponent<Game.Net.Edge>(entity))
            {
                return false;
            }

            return entityManager.HasComponent<Game.Prefabs.PrefabRef>(entity) &&
                   (entityManager.HasComponent<Game.Buildings.Building>(entity) ||
                    entityManager.HasComponent<Game.Objects.Transform>(entity));
        }

        public static Entity ResolveRoadEdge(EntityManager entityManager, Entity entity)
        {
            if (entity == Entity.Null || !entityManager.Exists(entity))
            {
                return Entity.Null;
            }

            if (entityManager.HasComponent<Game.Net.Edge>(entity))
            {
                return entity;
            }

            Entity ownerRoot = ResolveOwnerRoot(entityManager, entity);
            if (ownerRoot != Entity.Null && entityManager.HasComponent<Game.Net.Edge>(ownerRoot))
            {
                return ownerRoot;
            }

            if (entityManager.HasComponent<Game.Tools.Temp>(entity))
            {
                Game.Tools.Temp temp = entityManager.GetComponentData<Game.Tools.Temp>(entity);
                if (temp.m_Original != Entity.Null)
                {
                    Entity originalOwnerRoot = ResolveOwnerRoot(entityManager, temp.m_Original);
                    if (originalOwnerRoot != Entity.Null && entityManager.HasComponent<Game.Net.Edge>(originalOwnerRoot))
                    {
                        return originalOwnerRoot;
                    }
                }
            }

            return Entity.Null;
        }

        public static Entity ResolveBuildingEntity(EntityManager entityManager, Entity entity)
        {
            if (entity == Entity.Null || !entityManager.Exists(entity))
            {
                return Entity.Null;
            }

            Entity ownerRoot = ResolveOwnerRoot(entityManager, entity);
            if (IsBuilding(entityManager, ownerRoot))
            {
                return ownerRoot;
            }

            if (IsBuilding(entityManager, entity))
            {
                return entity;
            }

            if (entityManager.HasComponent<Game.Tools.Temp>(entity))
            {
                Game.Tools.Temp temp = entityManager.GetComponentData<Game.Tools.Temp>(entity);
                if (IsBuilding(entityManager, temp.m_Original))
                {
                    return temp.m_Original;
                }
            }

            return Entity.Null;
        }

        private static Entity ResolveOwnerRoot(EntityManager entityManager, Entity entity)
        {
            Entity current = entity;

            for (int depth = 0; depth < MaxOwnerTraversalDepth; depth++)
            {
                if (current == Entity.Null ||
                    !entityManager.Exists(current) ||
                    !entityManager.HasComponent<Game.Common.Owner>(current))
                {
                    break;
                }

                Game.Common.Owner owner = entityManager.GetComponentData<Game.Common.Owner>(current);
                if (owner.m_Owner == Entity.Null || !entityManager.Exists(owner.m_Owner))
                {
                    break;
                }

                current = owner.m_Owner;
            }

            return current;
        }
    }
}
