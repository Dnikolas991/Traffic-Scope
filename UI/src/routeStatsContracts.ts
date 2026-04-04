export type RouteVisualizationKind =
    | "CargoFreight"
    | "PrivateCar"
    | "PublicTransport"
    | "PublicService"
    | "Watercraft"
    | "Aircraft"
    | "Train"
    | "Human"
    | "Bicycle";

export interface RouteStatisticsLineItem {
    routeKey: string;
    sourceCount: number;
    sampleEdgeCount: number;
    sampleSource: number;
}

export interface RouteStatisticsBucket {
    kind: RouteVisualizationKind | number;
    sourceCount: number;
    truncated: boolean;
    lines: RouteStatisticsLineItem[];
}

export interface RouteStatisticsPanelPayload {
    selectedEntity: number;
    selectedKind: string;
    targetCount: number;
    matchedSourceCount: number;
    buckets: RouteStatisticsBucket[];
}
