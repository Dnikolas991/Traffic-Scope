using System.Collections.Generic;
using Game;
using Unity.Entities;

namespace Transit_Scope.code
{
    /// <summary>
    /// Facade for the route statistics pipeline.
    /// It stores the current snapshot and exposes edge weights for world-space rendering.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class TrafficFlowSystem : GameSystemBase
    {
        private RouteStatisticsPipeline m_Pipeline;

        internal RouteStatisticsSnapshot CurrentSnapshot { get; private set; }

        internal Dictionary<Entity, int> CurrentRouteEdgeWeights { get; } = new();

        protected override void OnCreate()
        {
            base.OnCreate();
            m_Pipeline = new RouteStatisticsPipeline(this);
            Logger.Info("TrafficFlowSystem 已切换为原版式线路流量统计管线。");
        }

        protected override void OnUpdate()
        {
            // Refreshing is driven by SelectionSystem.
        }

        internal RouteStatisticsSnapshot RefreshRouteStatistics(
            Entity selectedEntity,
            SelectionToolSystem.SelectionKind selectedKind,
            int? selectedIndex = null)
        {
            if (selectedEntity == Entity.Null || !EntityManager.Exists(selectedEntity))
            {
                ClearRouteStatistics();
                return null;
            }

            CurrentSnapshot = m_Pipeline.Build(selectedEntity, selectedKind, selectedIndex);
            RebuildEdgeWeights(CurrentSnapshot);

            Logger.Info(
                $"[RouteStats] Snapshot selected={selectedKind}#{selectedEntity.Index} " +
                $"targets={CurrentSnapshot?.TargetSet?.Count ?? 0} " +
                $"matchedSources={CurrentSnapshot?.MatchedSources?.Count ?? 0} " +
                $"buckets={CurrentSnapshot?.Buckets?.Count ?? 0} " +
                $"overlayEdges={CurrentRouteEdgeWeights.Count}");

            return CurrentSnapshot;
        }

        internal void ClearRouteStatistics()
        {
            CurrentSnapshot = null;
            CurrentRouteEdgeWeights.Clear();
        }

        private void RebuildEdgeWeights(RouteStatisticsSnapshot snapshot)
        {
            CurrentRouteEdgeWeights.Clear();

            if (snapshot == null)
            {
                return;
            }

            for (int sourceIndex = 0; sourceIndex < snapshot.MatchedSources.Count; sourceIndex++)
            {
                MatchedPathSourceRecord source = snapshot.MatchedSources[sourceIndex];
                for (int edgeIndex = 0; edgeIndex < source.RemainingEdges.Count; edgeIndex++)
                {
                    Entity edge = source.RemainingEdges[edgeIndex];
                    if (edge == Entity.Null)
                    {
                        continue;
                    }

                    if (CurrentRouteEdgeWeights.TryGetValue(edge, out int weight))
                    {
                        CurrentRouteEdgeWeights[edge] = weight + 1;
                    }
                    else
                    {
                        CurrentRouteEdgeWeights.Add(edge, 1);
                    }
                }
            }
        }
    }
}
