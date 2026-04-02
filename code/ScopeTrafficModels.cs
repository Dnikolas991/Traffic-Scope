using System.Collections.Generic;
using Unity.Entities;

namespace Transit_Scope.code
{
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
    /// 当前选中对象的一次完整分析结果。
    /// </summary>
    internal sealed class ScopeSelectionAnalysis
    {
        public Entity SelectedEntity { get; set; }
        public ScopeToolSystem.SelectionKind SelectedKind { get; set; }

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
