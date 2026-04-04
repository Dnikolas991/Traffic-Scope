import React from "react";
import { Portal } from "cs2/ui";
import { useValue } from "cs2/api";
import { hasStatsBinding, isActiveBinding, statsJsonBinding } from "./bindings";
import type { RouteStatisticsBucket, RouteStatisticsLineItem, RouteStatisticsPanelPayload, RouteVisualizationKind } from "./routeStatsContracts";

interface AnchorPosition {
    x: number;
    y: number;
}

interface Props {
    anchor: AnchorPosition | null;
}

interface NormalizedRouteStatisticsBucket extends Omit<RouteStatisticsBucket, "kind"> {
    kind: RouteVisualizationKind;
}

interface NormalizedRouteStatisticsPanelPayload extends Omit<RouteStatisticsPanelPayload, "buckets"> {
    buckets: NormalizedRouteStatisticsBucket[];
}

const orderedKinds: RouteVisualizationKind[] = ["Car", "Watercraft", "Aircraft", "Train", "Human", "Bicycle"];

const kindLabelMap: Record<RouteVisualizationKind, string> = {
    Car: "Car",
    Watercraft: "Watercraft",
    Aircraft: "Aircraft",
    Train: "Train",
    Human: "Human",
    Bicycle: "Bicycle"
};

const kindColorMap: Record<RouteVisualizationKind, string> = {
    Car: "#5DB7FF",
    Watercraft: "#4FC6F0",
    Aircraft: "#F28DDA",
    Train: "#C38BFF",
    Human: "#D6E2F0",
    Bicycle: "#60D5C0"
};

export const StatsPanel = ({ anchor }: Props) => {
    const isActive = useValue(isActiveBinding);
    const hasStats = useValue(hasStatsBinding);
    const statsJson = useValue(statsJsonBinding);

    if (!isActive || !hasStats || !statsJson || !anchor) {
        return null;
    }

    let payload: NormalizedRouteStatisticsPanelPayload | null = null;
    try {
        payload = normalizePayload(JSON.parse(statsJson) as RouteStatisticsPanelPayload);
    } catch (error) {
        console.error("Transit Scope route stats parse failed:", error);
        return null;
    }

    if (!payload) {
        return null;
    }

    const topBuckets = payload.buckets.slice(0, 6);
    const totalSources = Math.max(1, payload.matchedSourceCount || 0);
    const pieBackground = buildPieGradient(topBuckets, totalSources);

    return (
        <Portal>
            <div
                style={{
                    position: "absolute",
                    top: `${anchor.y}px`,
                    left: `${anchor.x}px`,
                    pointerEvents: "auto",
                    width: "720px",
                    borderRadius: "12px",
                    overflow: "hidden",
                    background: "linear-gradient(145deg, rgba(32, 38, 45, 0.98) 0%, rgba(20, 24, 28, 0.98) 100%)",
                    border: "1px solid rgba(255, 255, 255, 0.08)",
                    boxShadow: "0 12px 32px rgba(0, 0, 0, 0.45)"
                }}
            >
                <div
                    style={{
                        padding: "14px 20px",
                        background: "rgba(0, 0, 0, 0.25)",
                        borderBottom: "1px solid rgba(255, 255, 255, 0.06)",
                        display: "flex",
                        justifyContent: "space-between",
                        alignItems: "baseline",
                        gap: "16px"
                    }}
                >
                    <div>
                        <div
                            style={{
                                fontSize: "16px",
                                fontWeight: 800,
                                color: "var(--accentColorNormal, #8fd5ff)",
                                textTransform: "uppercase",
                                letterSpacing: "0.8px"
                            }}
                        >
                            Route Statistics
                        </div>
                        <div
                            style={{
                                fontSize: "12px",
                                color: "rgba(255,255,255,0.55)"
                            }}
                        >
                            Selected {payload.selectedKind} #{payload.selectedEntity}
                        </div>
                    </div>

                    <div
                        style={{
                            display: "grid",
                            gridTemplateColumns: "repeat(2, auto)",
                            gap: "4px 16px",
                            fontSize: "12px",
                            color: "rgba(255,255,255,0.72)"
                        }}
                    >
                        <div>Targets: {payload.targetCount}</div>
                        <div>Matched Sources: {payload.matchedSourceCount}</div>
                        <div>Buckets: {payload.buckets.length}</div>
                    </div>
                </div>

                <div
                    style={{
                        padding: "18px 20px 20px",
                        display: "grid",
                        gap: "16px"
                    }}
                >
                    {topBuckets.length > 0 ? (
                        <>
                            <div
                                style={{
                                    display: "grid",
                                    gridTemplateColumns: "220px 1fr",
                                    gap: "18px",
                                    alignItems: "center",
                                    padding: "14px",
                                    borderRadius: "10px",
                                    background: "rgba(255,255,255,0.04)",
                                    border: "1px solid rgba(255,255,255,0.06)"
                                }}
                            >
                                <div
                                    style={{
                                        display: "grid",
                                        justifyItems: "center",
                                        gap: "10px"
                                    }}
                                >
                                    <div
                                        style={{
                                            width: "164px",
                                            height: "164px",
                                            borderRadius: "50%",
                                            background: pieBackground,
                                            position: "relative",
                                            boxShadow: "inset 0 0 0 1px rgba(255,255,255,0.07)"
                                        }}
                                    >
                                        <div
                                            style={{
                                                position: "absolute",
                                                inset: "26px",
                                                borderRadius: "50%",
                                                background: "rgba(20, 24, 28, 0.96)",
                                                display: "grid",
                                                placeItems: "center",
                                                textAlign: "center",
                                                boxShadow: "0 0 0 1px rgba(255,255,255,0.05)"
                                            }}
                                        >
                                            <div>
                                                <div style={{ fontSize: "11px", color: "rgba(255,255,255,0.6)", textTransform: "uppercase", letterSpacing: "0.7px" }}>
                                                    Total
                                                </div>
                                                <div style={{ fontSize: "28px", fontWeight: 800, color: "#FFFFFF", lineHeight: 1.1 }}>
                                                    {payload.matchedSourceCount}
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                    <div style={{ fontSize: "12px", color: "rgba(255,255,255,0.62)" }}>
                                        Pie composition by matched path sources
                                    </div>
                                </div>

                                <div
                                    style={{
                                        display: "grid",
                                        gap: "10px"
                                    }}
                                >
                                    {topBuckets.map((bucket) => renderLegendRow(bucket, totalSources))}
                                </div>
                            </div>

                            <div
                                style={{
                                    display: "grid",
                                    gap: "12px"
                                }}
                            >
                                {topBuckets.map((bucket) => renderBucket(bucket, totalSources))}
                            </div>
                        </>
                    ) : (
                        <div
                            style={{
                                padding: "20px 16px",
                                borderRadius: "10px",
                                background: "rgba(255,255,255,0.04)",
                                border: "1px solid rgba(255,255,255,0.06)",
                                color: "rgba(255,255,255,0.78)"
                            }}
                        >
                            <div style={{ fontSize: "14px", fontWeight: 700, color: "#FFFFFF" }}>
                                No matched path sources
                            </div>
                            <div style={{ marginTop: "8px", fontSize: "12px", lineHeight: 1.6 }}>
                                The panel is active, but the current matching chain did not find any path sources for this selection.
                            </div>
                        </div>
                    )}
                </div>
            </div>
        </Portal>
    );
};

function renderLegendRow(bucket: NormalizedRouteStatisticsBucket, totalSources: number) {
    const ratio = ((bucket.sourceCount / totalSources) * 100).toFixed(1);

    return (
        <div
            key={`legend-${bucket.kind}`}
            style={{
                display: "grid",
                gridTemplateColumns: "14px 1fr auto",
                alignItems: "center",
                columnGap: "10px",
                fontSize: "12px",
                color: "rgba(255,255,255,0.78)"
            }}
        >
            <div
                style={{
                    width: "10px",
                    height: "10px",
                    borderRadius: "50%",
                    background: kindColorMap[bucket.kind]
                }}
            />
            <div>{kindLabelMap[bucket.kind]}</div>
            <div style={{ color: kindColorMap[bucket.kind] }}>{ratio}%</div>
        </div>
    );
}

function renderBucket(bucket: NormalizedRouteStatisticsBucket, totalSources: number) {
    const bucketColor = kindColorMap[bucket.kind];
    const ratio = ((bucket.sourceCount / totalSources) * 100).toFixed(1);

    return (
        <div
            key={bucket.kind}
            style={{
                padding: "12px 14px",
                borderRadius: "10px",
                background: "rgba(255, 255, 255, 0.04)",
                border: "1px solid rgba(255,255,255,0.06)"
            }}
        >
            <div
                style={{
                    display: "grid",
                    gridTemplateColumns: "14px 1fr auto auto",
                    alignItems: "center",
                    columnGap: "12px"
                }}
            >
                <div
                    style={{
                        width: "10px",
                        height: "10px",
                        borderRadius: "50%",
                        background: bucketColor
                    }}
                />
                <div style={{ fontSize: "14px", fontWeight: 700, color: "#FFFFFF" }}>
                    {kindLabelMap[bucket.kind]}
                </div>
                <div style={{ fontSize: "13px", color: "rgba(255,255,255,0.68)" }}>
                    {bucket.sourceCount} sources
                </div>
                <div style={{ fontSize: "13px", color: bucketColor }}>
                    {ratio}%
                </div>
            </div>

            <div
                style={{
                    marginTop: "8px",
                    height: "6px",
                    borderRadius: "999px",
                    background: "rgba(255,255,255,0.06)",
                    overflow: "hidden"
                }}
            >
                <div
                    style={{
                        width: `${Math.max(4, (bucket.sourceCount / totalSources) * 100)}%`,
                        height: "100%",
                        background: bucketColor
                    }}
                />
            </div>

            <div
                style={{
                    marginTop: "10px",
                    display: "grid",
                    gap: "6px"
                }}
            >
                {bucket.lines.slice(0, 5).map((line) => renderLine(line))}
            </div>

            {bucket.truncated && (
                <div
                    style={{
                        marginTop: "8px",
                        fontSize: "11px",
                        color: "rgba(255, 180, 120, 0.92)"
                    }}
                >
                    Truncated by vanilla 200-source limit
                </div>
            )}
        </div>
    );
}

function renderLine(line: RouteStatisticsLineItem) {
    return (
        <div
            key={line.routeKey}
            style={{
                display: "grid",
                gridTemplateColumns: "1fr auto auto",
                columnGap: "12px",
                fontSize: "12px",
                color: "rgba(255,255,255,0.78)"
            }}
        >
            <div
                style={{
                    whiteSpace: "nowrap",
                    overflow: "hidden",
                    textOverflow: "ellipsis"
                }}
            >
                Route #{line.routeKey}
            </div>
            <div>{line.sourceCount} hits</div>
            <div>{line.sampleEdgeCount} edges</div>
        </div>
    );
}

function normalizePayload(payload: RouteStatisticsPanelPayload): NormalizedRouteStatisticsPanelPayload {
    const buckets: NormalizedRouteStatisticsBucket[] = [];
    for (const bucket of payload.buckets ?? []) {
        const normalizedBucket = normalizeBucket(bucket);
        if (normalizedBucket) {
            buckets.push(normalizedBucket);
        }
    }

    buckets.sort((left, right) => right.sourceCount - left.sourceCount);

    return {
        ...payload,
        buckets
    };
}

function normalizeBucket(bucket: RouteStatisticsBucket): NormalizedRouteStatisticsBucket | null {
    const kind = normalizeKind(bucket.kind);
    if (!kind) {
        return null;
    }

    return {
        ...bucket,
        kind,
        lines: bucket.lines ?? []
    };
}

function normalizeKind(kind: RouteStatisticsBucket["kind"]): RouteVisualizationKind | null {
    if (typeof kind === "string" && orderedKinds.includes(kind as RouteVisualizationKind)) {
        return kind as RouteVisualizationKind;
    }

    if (typeof kind === "number" && kind >= 0 && kind < orderedKinds.length) {
        return orderedKinds[kind];
    }

    return null;
}

function buildPieGradient(buckets: NormalizedRouteStatisticsBucket[], totalSources: number) {
    let offset = 0;
    const stops = buckets.map((bucket) => {
        const start = offset;
        offset += (bucket.sourceCount / totalSources) * 360;
        return `${kindColorMap[bucket.kind]} ${start}deg ${offset}deg`;
    });

    if (stops.length === 0) {
        return "rgba(255,255,255,0.08)";
    }

    return `conic-gradient(${stops.join(", ")})`;
}
