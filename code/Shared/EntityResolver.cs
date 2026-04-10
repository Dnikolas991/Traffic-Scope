using System.Collections.Generic;
using Unity.Entities;

namespace Transit_Scope.code
{
    /// <summary>
    /// 统一处理命中实体到业务实体的解析逻辑。
    /// 
    /// 选择工具、路径分析和 Overlay 都会碰到子实体、Temp 实体和 Owner 链，
    /// 这里负责把它们还原成真正的道路边或建筑实体。
    /// </summary>
    internal static class EntityResolver
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

            Entity resolved = ResolveBuildingCandidate(entityManager, entity);
            if (resolved != Entity.Null)
            {
                return resolved;
            }

            if (entityManager.HasComponent<Game.Tools.Temp>(entity))
            {
                Game.Tools.Temp temp = entityManager.GetComponentData<Game.Tools.Temp>(entity);
                resolved = ResolveBuildingCandidate(entityManager, temp.m_Original);
                if (resolved != Entity.Null)
                {
                    return resolved;
                }
            }

            return Entity.Null;
        }

        public static Entity ResolveOwnerRootEntity(EntityManager entityManager, Entity entity)
        {
            if (entity == Entity.Null || !entityManager.Exists(entity))
            {
                return Entity.Null;
            }

            return ResolveOwnerRoot(entityManager, entity);
        }

        public static List<Entity> CollectBuildingHighlightEntities(EntityManager entityManager, Entity buildingEntity)
        {
            List<Entity> result = new();
            if (buildingEntity == Entity.Null || !entityManager.Exists(buildingEntity))
            {
                return result;
            }

            Entity aggregateRoot = ResolveBuildingAggregateRoot(entityManager, buildingEntity);
            HashSet<Entity> visited = new();
            AddBuildingHighlightEntityRecursive(entityManager, aggregateRoot, visited, result);
            return result;
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

        private static Entity ResolveTargetRoot(EntityManager entityManager, Entity entity)
        {
            Entity current = entity;

            for (int depth = 0; depth < MaxOwnerTraversalDepth; depth++)
            {
                if (current == Entity.Null ||
                    !entityManager.Exists(current) ||
                    !entityManager.HasComponent<Game.Common.Target>(current))
                {
                    break;
                }

                Entity target = entityManager.GetComponentData<Game.Common.Target>(current).m_Target;
                if (target == Entity.Null || !entityManager.Exists(target))
                {
                    break;
                }

                current = target;
            }

            return current;
        }

        private static Entity ResolveAttachedRoot(EntityManager entityManager, Entity entity)
        {
            Entity current = entity;

            for (int depth = 0; depth < MaxOwnerTraversalDepth; depth++)
            {
                if (current == Entity.Null ||
                    !entityManager.Exists(current) ||
                    !entityManager.HasComponent<Game.Objects.Attached>(current))
                {
                    break;
                }

                Entity parent = entityManager.GetComponentData<Game.Objects.Attached>(current).m_Parent;
                if (parent == Entity.Null || !entityManager.Exists(parent))
                {
                    break;
                }

                current = parent;
            }

            return current;
        }

        private static Entity ResolveBuildingCandidate(EntityManager entityManager, Entity entity)
        {
            if (IsBuilding(entityManager, entity))
            {
                return entity;
            }

            Entity attachedRoot = ResolveAttachedRoot(entityManager, entity);
            if (IsBuilding(entityManager, attachedRoot))
            {
                return attachedRoot;
            }

            Entity ownerRoot = ResolveOwnerRoot(entityManager, entity);
            if (IsBuilding(entityManager, ownerRoot))
            {
                return ownerRoot;
            }

            Entity targetRoot = ResolveTargetRoot(entityManager, entity);
            if (IsBuilding(entityManager, targetRoot))
            {
                return targetRoot;
            }

            Entity ownerOfAttachedRoot = ResolveOwnerRoot(entityManager, attachedRoot);
            if (IsBuilding(entityManager, ownerOfAttachedRoot))
            {
                return ownerOfAttachedRoot;
            }

            Entity ownerOfTargetRoot = ResolveOwnerRoot(entityManager, targetRoot);
            if (IsBuilding(entityManager, ownerOfTargetRoot))
            {
                return ownerOfTargetRoot;
            }

            Entity attachedOfOwnerRoot = ResolveAttachedRoot(entityManager, ownerRoot);
            if (IsBuilding(entityManager, attachedOfOwnerRoot))
            {
                return attachedOfOwnerRoot;
            }

            Entity attachedOfTargetRoot = ResolveAttachedRoot(entityManager, targetRoot);
            if (IsBuilding(entityManager, attachedOfTargetRoot))
            {
                return attachedOfTargetRoot;
            }

            return Entity.Null;
        }

        private static Entity ResolveBuildingAggregateRoot(EntityManager entityManager, Entity entity)
        {
            Entity best = ResolveBuildingCandidate(entityManager, entity);
            if (best == Entity.Null)
            {
                return entity;
            }

            Entity current = best;
            for (int depth = 0; depth < MaxOwnerTraversalDepth; depth++)
            {
                Entity next = ResolveOwnerRoot(entityManager, current);
                if (next != Entity.Null && next != current)
                {
                    Entity ownerBuilding = ResolveBuildingCandidate(entityManager, next);
                    if (ownerBuilding != Entity.Null && ownerBuilding != current)
                    {
                        current = ownerBuilding;
                        continue;
                    }
                }

                Entity attached = ResolveAttachedRoot(entityManager, current);
                if (attached != Entity.Null && attached != current)
                {
                    Entity attachedBuilding = ResolveBuildingCandidate(entityManager, attached);
                    if (attachedBuilding != Entity.Null && attachedBuilding != current)
                    {
                        current = attachedBuilding;
                        continue;
                    }
                }

                break;
            }

            return current;
        }

        private static void AddBuildingHighlightEntityRecursive(
            EntityManager entityManager,
            Entity entity,
            HashSet<Entity> visited,
            List<Entity> result)
        {
            if (entity == Entity.Null || !entityManager.Exists(entity) || !visited.Add(entity))
            {
                return;
            }

            if (entityManager.HasComponent<Game.Prefabs.PrefabRef>(entity) &&
                !entityManager.HasComponent<Game.Net.Edge>(entity))
            {
                result.Add(entity);
            }

            if (entityManager.HasBuffer<Game.Objects.SubObject>(entity))
            {
                DynamicBuffer<Game.Objects.SubObject> subObjects = entityManager.GetBuffer<Game.Objects.SubObject>(entity);
                for (int i = 0; i < subObjects.Length; i++)
                {
                    AddBuildingHighlightEntityRecursive(entityManager, subObjects[i].m_SubObject, visited, result);
                }
            }

            if (entityManager.HasBuffer<Game.Buildings.InstalledUpgrade>(entity))
            {
                DynamicBuffer<Game.Buildings.InstalledUpgrade> upgrades = entityManager.GetBuffer<Game.Buildings.InstalledUpgrade>(entity);
                for (int i = 0; i < upgrades.Length; i++)
                {
                    AddBuildingHighlightEntityRecursive(entityManager, upgrades[i].m_Upgrade, visited, result);
                }
            }

            if (entityManager.HasComponent<Game.Objects.Attachment>(entity))
            {
                Entity attached = entityManager.GetComponentData<Game.Objects.Attachment>(entity).m_Attached;
                AddBuildingHighlightEntityRecursive(entityManager, attached, visited, result);
            }
        }
    }
}
