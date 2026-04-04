using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using Game.Pathfind;
using Unity.Entities;

namespace Transit_Scope.code
{
    /// <summary>
    /// The six visualization buckets mirrored from the vanilla traffic route view.
    /// </summary>
    internal enum RouteVisualizationKind
    {
        Car = 0,
        Watercraft = 1,
        Aircraft = 2,
        Train = 3,
        Human = 4,
        Bicycle = 5
    }

    /// <summary>
    /// Input context for a single statistics refresh.
    /// </summary>
    internal sealed class RouteStatisticsSelectionContext
    {
        public Entity SelectedEntity { get; set; }
        public SelectionToolSystem.SelectionKind SelectedKind { get; set; }
        public int? SelectedIndex { get; set; }
    }

    /// <summary>
    /// Expanded target set used for path source matching.
    /// </summary>
    internal sealed class RouteStatisticsTargetSet
    {
        public HashSet<Entity> Targets { get; } = new();

        public int Count => Targets.Count;

        public void Add(Entity entity)
        {
            if (entity != Entity.Null)
            {
                Targets.Add(entity);
            }
        }
    }

    /// <summary>
    /// One matched source plus the data needed for panel aggregation.
    /// </summary>
    internal sealed class MatchedPathSourceRecord
    {
        public Entity SourceEntity { get; set; }
        public Entity ControllerEntity { get; set; }
        public Entity Destination { get; set; }
        public Entity CurrentEdge { get; set; }
        public PathMethod Methods { get; set; }
        public RouteVisualizationKind VisualizationKind { get; set; }
        public List<Entity> RemainingEdges { get; } = new();

        /// <summary>
        /// Stable key for grouping similar remaining routes inside one bucket.
        /// </summary>
        public ulong NormalizedRouteKey { get; set; }
    }

    /// <summary>
    /// Aggregated line item shown in the statistics panel.
    /// </summary>
    [DataContract]
    internal sealed class RouteStatisticsLineItem
    {
        [DataMember(Name = "routeKey")]
        public string RouteKey { get; set; }

        [DataMember(Name = "sourceCount")]
        public int SourceCount { get; set; }

        [DataMember(Name = "sampleEdgeCount")]
        public int SampleEdgeCount { get; set; }

        [DataMember(Name = "sampleSource")]
        public int SampleSourceIndex { get; set; }
    }

    /// <summary>
    /// One bucket in the final panel payload.
    /// </summary>
    [DataContract]
    internal sealed class RouteStatisticsBucket
    {
        [DataMember(Name = "kind")]
        public RouteVisualizationKind Kind { get; set; }

        [DataMember(Name = "sourceCount")]
        public int SourceCount { get; set; }

        [DataMember(Name = "truncated")]
        public bool TruncatedBySourceLimit { get; set; }

        [DataMember(Name = "lines")]
        public List<RouteStatisticsLineItem> Lines { get; } = new();
    }

    /// <summary>
    /// Backend snapshot for the current selection.
    /// </summary>
    internal sealed class RouteStatisticsSnapshot
    {
        public const int VanillaSourceLimitPerKind = 200;

        public RouteStatisticsSelectionContext Context { get; set; }
        public RouteStatisticsTargetSet TargetSet { get; set; }
        public List<MatchedPathSourceRecord> MatchedSources { get; } = new();
        public List<RouteStatisticsBucket> Buckets { get; } = new();
    }

    /// <summary>
    /// Frontend DTO for the route statistics panel.
    /// </summary>
    [DataContract]
    internal sealed class RouteStatisticsPanelPayload
    {
        [DataMember(Name = "selectedEntity")]
        public int SelectedEntityIndex { get; set; }

        [DataMember(Name = "selectedKind")]
        public string SelectedKind { get; set; }

        [DataMember(Name = "targetCount")]
        public int TargetCount { get; set; }

        [DataMember(Name = "matchedSourceCount")]
        public int MatchedSourceCount { get; set; }

        [DataMember(Name = "buckets")]
        public List<RouteStatisticsBucket> Buckets { get; } = new();

        public static RouteStatisticsPanelPayload FromSnapshot(RouteStatisticsSnapshot snapshot)
        {
            RouteStatisticsPanelPayload payload = new()
            {
                SelectedEntityIndex = snapshot?.Context?.SelectedEntity.Index ?? 0,
                SelectedKind = snapshot?.Context?.SelectedKind.ToString() ?? SelectionToolSystem.SelectionKind.None.ToString(),
                TargetCount = snapshot?.TargetSet?.Count ?? 0,
                MatchedSourceCount = snapshot?.MatchedSources?.Count ?? 0
            };

            if (snapshot != null)
            {
                payload.Buckets.AddRange(snapshot.Buckets);
            }

            return payload;
        }

        public string ToJson()
        {
            DataContractJsonSerializer serializer = new(typeof(RouteStatisticsPanelPayload));

            using MemoryStream stream = new();
            serializer.WriteObject(stream, this);
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
