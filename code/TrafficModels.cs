using System.Collections.Generic;
using Game.Pathfind;
using Unity.Entities;

namespace Transit_Scope.code
{
    /// <summary>
    /// 统一表示一类流量计数。
    /// 当前项目里无论是“当前就在道路上的对象”，还是“未来仍会经过该道路的对象”，
    /// 最终都会归并到这组分类字段里，保证 UI 统计口径一致。
    /// </summary>
    public struct TrafficCounters
    {
        public int PersonalCars;
        public int Taxis;
        public int Cargo;
        public int PublicTransport;
        public int CityService;
        public int Bicycles;
        public int Trains;
        public int Watercraft;
        public int Aircraft;
        public int Humans;
        public int OtherVehicles;
        public int Others;

        public int Total =>
            PersonalCars + Taxis + Cargo + PublicTransport + CityService +
            Bicycles + Trains + Watercraft + Aircraft + Humans + OtherVehicles + Others;

        public void Add(TrafficCounters other)
        {
            PersonalCars += other.PersonalCars;
            Taxis += other.Taxis;
            Cargo += other.Cargo;
            PublicTransport += other.PublicTransport;
            CityService += other.CityService;
            Bicycles += other.Bicycles;
            Trains += other.Trains;
            Watercraft += other.Watercraft;
            Aircraft += other.Aircraft;
            Humans += other.Humans;
            OtherVehicles += other.OtherVehicles;
            Others += other.Others;
        }
    }

    /// <summary>
    /// 某个 agent 当前“已经分配，但尚未消费完”的剩余导航路线快照。
    /// 这里不再描述“新路线生成事件”，而是描述此刻仍然有效的未来路线切片。
    /// </summary>
    internal sealed class ActiveAssignedRoute
    {
        public Entity OwnerEntity;
        public Entity Destination;
        public Entity CurrentEdge;
        public PathFlags OwnerState;
        public PathFlags InfoState;
        public PathMethod Methods;
        public int ElementIndex;
        public int TotalElements;
        public int RemainingElements;
        public uint RemainingRouteHash;
        public uint LastSeenFrame;
        public int StableFrameCount;
        public bool IsStable;
        public TrafficCounters Traffic;
        public List<Entity> RemainingRouteEdges { get; } = new();
    }

    /// <summary>
    /// 一帧内剩余路线快照的摘要。
    /// 它描述的是“当前仍有效的未来路线存量”，而不是本帧新产生了多少路线。
    /// </summary>
    internal struct RouteFrameStats
    {
        public uint FrameIndex;
        public int ObservedOwners;
        public int StableOwners;
        public int RemainingSegments;
    }

    /// <summary>
    /// 当前选中对象的一次完整分析结果。
    /// 这里承载的是给 UI 和 Overlay 直接消费的数据视图。
    /// </summary>
    internal sealed class SelectionAnalysis
    {
        public Entity SelectedEntity { get; set; }
        public SelectionToolSystem.SelectionKind SelectedKind { get; set; }

        public string TitleKey { get; set; }
        public string FallbackTitle { get; set; }
        public string SubtitleKey { get; set; }
        public string SubtitleArgument { get; set; }
        public string FallbackSubtitle { get; set; }

        public TrafficCounters CurrentTraffic;
        public TrafficCounters PlannedTraffic;

        public Dictionary<Entity, int> RouteEdgeWeights { get; } = new();

        public TrafficCounters CombinedTraffic
        {
            get
            {
                TrafficCounters total = CurrentTraffic;
                total.Add(PlannedTraffic);
                return total;
            }
        }

        public void RegisterRouteEdges(IEnumerable<Entity> routeEdges)
        {
            foreach (Entity edge in routeEdges)
            {
                if (edge == Entity.Null)
                {
                    continue;
                }

                if (RouteEdgeWeights.TryGetValue(edge, out int weight))
                {
                    RouteEdgeWeights[edge] = weight + 1;
                }
                else
                {
                    RouteEdgeWeights.Add(edge, 1);
                }
            }
        }
    }
}
