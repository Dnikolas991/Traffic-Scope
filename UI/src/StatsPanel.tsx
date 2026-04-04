import React, { useMemo } from "react";
import { Portal } from "cs2/ui";
import { useValue } from "cs2/api";
import { hasStatsBinding, isActiveBinding, statsJsonBinding } from "./bindings";
import { useTranslate } from "./localization";
import type { RouteStatisticsBucket, RouteStatisticsPanelPayload, RouteVisualizationKind } from "./routeStatsContracts";

interface AnchorPosition { x: number; y: number; }
interface Props { anchor: AnchorPosition | null; }

interface NormalizedRouteStatisticsBucket extends Omit<RouteStatisticsBucket, "kind"> {
    kind: RouteVisualizationKind;
    percentage: number; // 存储计算后的精确百分比
}

interface NormalizedRouteStatisticsPanelPayload extends Omit<RouteStatisticsPanelPayload, "buckets"> {
    buckets: NormalizedRouteStatisticsBucket[];
    calculatedTotal: number; // 重新计算的总数
}

const orderedKinds: RouteVisualizationKind[] = [
    "CargoFreight", "PrivateCar", "PublicTransport", "PublicService",
    "Watercraft", "Aircraft", "Train", "Human", "Bicycle"
];

const kindLabelKeyMap: Record<RouteVisualizationKind, string> = {
    CargoFreight: "stats.item.cargo",
    PrivateCar: "stats.item.private_cars",
    PublicTransport: "stats.item.public_transit",
    PublicService: "stats.item.city_service",
    Watercraft: "stats.item.water",
    Aircraft: "stats.item.air",
    Train: "stats.item.rail",
    Human: "stats.item.pedestrians",
    Bicycle: "stats.item.bicycles"
};

const kindColorMap: Record<RouteVisualizationKind, string> = {
    CargoFreight: "#FF9800", PrivateCar: "#2196F3", PublicTransport: "#00BCD4",
    PublicService: "#F44336", Watercraft: "#3F51B5", Aircraft: "#E91E63",
    Train: "#9C27B0", Human: "#4CAF50", Bicycle: "#FFF176"
};

export const StatsPanel = ({ anchor }: Props) => {
    const isActive = useValue(isActiveBinding);
    const hasStats = useValue(hasStatsBinding);
    const statsJson = useValue(statsJsonBinding);
    const translate = useTranslate();

    // 使用 useMemo 优化计算性能，防止重复解析 JSON 和重算比例
    const payload = useMemo(() => {
        if (!statsJson) return null;
        try {
            const raw = JSON.parse(statsJson) as RouteStatisticsPanelPayload;
            return calculateProportions(normalizePayload(raw));
        } catch (e) {
            console.error("Transit Scope 解析失败:", e);
            return null;
        }
    }, [statsJson]);

    if (!isActive || !hasStats || !payload || !anchor) return null;

    const topBuckets = payload.buckets.filter(b => b.sourceCount > 0);
    const hasTraffic = payload.calculatedTotal > 0;
    
    // 这里的 pieBackground 现在基于 recalculate 后的总数，不会有缺口
    const pieBackground = buildPieGradient(payload.buckets, payload.calculatedTotal);

    return (
        <Portal>
            <div style={{
                position: "absolute", top: `${anchor.y}px`, left: `${anchor.x}px`,
                pointerEvents: "auto", width: "560px", borderRadius: "28px",
                overflow: "hidden", background: "rgba(10, 12, 16, 0.98)",
                backdropFilter: "blur(30px)", border: "1px solid rgba(255, 255, 255, 0.18)",
                boxShadow: "0 35px 70px rgba(0, 0, 0, 0.9)", color: "#FFFFFF"
            }}>
                <div style={{
                    padding: "24px 32px", background: "rgba(255, 255, 255, 0.04)",
                    borderBottom: "1px solid rgba(255, 255, 255, 0.1)",
                }}>
                    <div style={{ fontSize: "22px", fontWeight: 900, letterSpacing: "1.5px", textTransform: "uppercase" }}>
                        {translate("stats.title.main", "Transit Statistics")}
                    </div>
                </div>

                <div style={{ padding: "44px 32px" }}>
                    <div style={{ display: "flex", alignItems: "center", gap: "48px" }}>
                        <div style={{ position: "relative", width: "180px", height: "180px", flexShrink: 0 }}>
                            <div style={{ width: "100%", height: "100%", borderRadius: "50%", background: pieBackground, zIndex: 1 }} />
                            <div style={{
                                position: "absolute", inset: "32px", borderRadius: "50%", background: "#121519",
                                display: "flex", flexDirection: "column", justifyContent: "center", alignItems: "center",
                                zIndex: 10
                            }}>
                                {hasTraffic ? (
                                    <>
                                        <div style={{ fontSize: "52px", fontWeight: 950, lineHeight: 1 }}>{payload.calculatedTotal}</div>
                                        <div style={{ fontSize: "14px", fontWeight: 700, textTransform: "uppercase", marginTop: "10px", letterSpacing: "2px" }}>
                                            {translate("stats.total", "Total")}
                                        </div>
                                    </>
                                ) : (
                                    <div style={{ fontSize: "52px", fontWeight: 950, color: "rgba(255,255,255,0.1)" }}>0</div>
                                )}
                            </div>
                        </div>

                        <div style={{ flexGrow: 1, display: "flex", flexDirection: "column", gap: "18px" }}>
                            {hasTraffic ? (
                                topBuckets.map((bucket) => (
                                    <div key={bucket.kind} style={{ display: "flex", alignItems: "center", gap: "16px" }}>
                                        <div style={{ width: "12px", height: "12px", borderRadius: "50%", backgroundColor: kindColorMap[bucket.kind] }} />
                                        <div style={{ fontSize: "17px", flexGrow: 1, fontWeight: 600 }}>{translate(kindLabelKeyMap[bucket.kind], bucket.kind)}</div>
                                        <div style={{ fontSize: "17px", fontWeight: 800, width: "55px", textAlign: "right" }}>{bucket.sourceCount}</div>
                                        <div style={{ fontSize: "14px", color: "rgba(255, 255, 255, 0.4)", width: "65px", textAlign: "right" }}>
                                            {bucket.percentage.toFixed(1)}%
                                        </div>
                                    </div>
                                ))
                            ) : (
                                <div style={{ display: "flex", flexDirection: "column", gap: "12px" }}>
                                    <div style={{ fontSize: "44px", fontWeight: 950, textTransform: "uppercase" }}>{translate("stats.item.no_traffic", "No Traffic")}</div>
                                    <div style={{ fontSize: "18px", color: "rgba(255, 255, 255, 0.4)", lineHeight: 1.4, fontWeight: 500 }}>
                                        {translate("stats.message.waiting", "Waiting for transit data to be collected...")}
                                    </div>
                                </div>
                            )}
                        </div>
                    </div>
                </div>
            </div>
        </Portal>
    );
};

/** 规范化原始数据，并重新计算总数以对齐分母 */
function normalizePayload(payload: RouteStatisticsPanelPayload): NormalizedRouteStatisticsPanelPayload {
    const bucketMap = new Map<string, RouteStatisticsBucket>();
    payload.buckets.forEach(b => {
        const kind = normalizeKind(b.kind);
        if (kind) bucketMap.set(kind, b);
    });

    let calculatedTotal = 0;
    const buckets = orderedKinds.map(kind => {
        const b = bucketMap.get(kind);
        const count = b?.sourceCount ?? 0;
        calculatedTotal += count;
        return {
            kind,
            sourceCount: count,
            truncated: b?.truncated ?? false,
            lines: b?.lines ?? [],
            percentage: 0
        };
    });

    return { ...payload, buckets, calculatedTotal };
}

/** 
 * 核心修复逻辑：计算占比
 * 即使 totalSources > 0，也要确保各部分比例完全填满 360 度，且显示百分比之和为 100%
 */
function calculateProportions(payload: NormalizedRouteStatisticsPanelPayload): NormalizedRouteStatisticsPanelPayload {
    if (payload.calculatedTotal <= 0) return payload;

    // 我们这里采用精确浮点计算，确保饼图无间隙
    payload.buckets.forEach(b => {
        b.percentage = (b.sourceCount / payload.calculatedTotal) * 100;
    });

    return payload;
}

function normalizeKind(kind: RouteStatisticsBucket["kind"]): RouteVisualizationKind | null {
    if (typeof kind === "string" && orderedKinds.includes(kind as RouteVisualizationKind)) return kind as RouteVisualizationKind;
    if (typeof kind === "number" && kind >= 0 && kind < orderedKinds.length) return orderedKinds[kind];
    return null;
}

/** 构建环形图渐变 - 现在保证 360 度完全覆盖 */
function buildPieGradient(buckets: NormalizedRouteStatisticsBucket[], total: number) {
    if (total <= 0) return "rgba(255, 255, 255, 0.85)";

    let offset = 0;
    const stops: string[] = [];
    
    // 过滤出有数据的桶进行绘制
    const activeBuckets = buckets.filter(b => b.sourceCount > 0);
    
    activeBuckets.forEach((bucket, idx) => {
        const start = offset;
        // 计算当前项结束角度
        const delta = (bucket.sourceCount / total) * 360;
        
        // 最后一项强制锁定到 360，消除浮点数精度导致的微小缝隙
        const end = (idx === activeBuckets.length - 1) ? 360 : start + delta;
        
        stops.push(`${kindColorMap[bucket.kind]} ${start.toFixed(2)}deg ${end.toFixed(2)}deg`);
        offset = end;
    });

    return stops.length === 0 ? "rgba(255, 255, 255, 0.85)" : `conic-gradient(${stops.join(", ")})`;
}
