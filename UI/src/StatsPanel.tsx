import React from "react";
import { Portal } from "cs2/ui";
import { useValue } from "cs2/api";
import { hasStatsBinding, isActiveBinding, statsJsonBinding } from "./bindings";
import { translate } from "./localization";
import type { RouteStatisticsBucket, RouteStatisticsPanelPayload, RouteVisualizationKind } from "./routeStatsContracts";

/** 锚点位置接口 */
interface AnchorPosition {
    x: number;
    y: number;
}

interface Props {
    anchor: AnchorPosition | null;
}

/** 归一化后的数据桶接口 */
interface NormalizedRouteStatisticsBucket extends Omit<RouteStatisticsBucket, "kind"> {
    kind: RouteVisualizationKind;
}

/** 归一化后的面板数据接口 */
interface NormalizedRouteStatisticsPanelPayload extends Omit<RouteStatisticsPanelPayload, "buckets"> {
    buckets: NormalizedRouteStatisticsBucket[];
}

/** 定义统计类别的固定顺序 */
const orderedKinds: RouteVisualizationKind[] = [
    "CargoFreight",
    "PrivateCar",
    "PublicTransport",
    "PublicService",
    "Watercraft",
    "Aircraft",
    "Train",
    "Human",
    "Bicycle"
];

/** 类别与本地化 Key 的映射 */
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

/** 
 * 活泼的颜色方案 
 * 行人：绿色 (#4CAF50)
 * 自行车：淡黄色 (#FFF176)
 */
const kindColorMap: Record<RouteVisualizationKind, string> = {
    CargoFreight: "#FF9800",    // 橙色
    PrivateCar: "#2196F3",     // 蓝色
    PublicTransport: "#00BCD4", // 青色
    PublicService: "#F44336",   // 红色
    Watercraft: "#3F51B5",     // 靛蓝色
    Aircraft: "#E91E63",       // 玫红色
    Train: "#9C27B0",          // 紫色
    Human: "#4CAF50",          // 绿色
    Bicycle: "#FFF176"         // 淡黄色
};

export const StatsPanel = ({ anchor }: Props) => {
    const isActive = useValue(isActiveBinding);
    const hasStats = useValue(hasStatsBinding);
    const statsJson = useValue(statsJsonBinding);

    // 如果面板未激活或没有数据，则不渲染
    if (!isActive || !hasStats || !statsJson || !anchor) {
        return null;
    }

    let payload: NormalizedRouteStatisticsPanelPayload | null = null;
    try {
        // 解析后端传来的 JSON 数据并进行归一化处理
        payload = normalizePayload(JSON.parse(statsJson) as RouteStatisticsPanelPayload);
    } catch (error) {
        console.error("Transit Scope 统计解析失败:", error);
        return null;
    }

    if (!payload) {
        return null;
    }

    // 筛选出有数据的类别
    const topBuckets = payload.buckets.filter(b => b.sourceCount > 0);
    const hasTraffic = payload.matchedSourceCount > 0;
    const totalSources = payload.matchedSourceCount;
    
    // 构建环形图的 CSS 渐变背景
    const pieBackground = buildPieGradient(payload.buckets, totalSources);

    return (
        <Portal>
            <div
                style={{
                    position: "absolute",
                    top: `${anchor.y}px`,
                    left: `${anchor.x}px`,
                    pointerEvents: "auto",
                    width: "560px",             // 略微增加宽度
                    borderRadius: "28px",        
                    overflow: "hidden",
                    background: "rgba(10, 12, 16, 0.98)", // 极深色背景，提升对比度
                    backdropFilter: "blur(30px)", 
                    border: "1px solid rgba(255, 255, 255, 0.18)",
                    boxShadow: "0 35px 70px rgba(0, 0, 0, 0.9)",
                    fontFamily: "Inter, system-ui, sans-serif"
                }}
            >
                {/* 顶部标题栏 */}
                <div
                    style={{
                        padding: "24px 32px",
                        background: "rgba(255, 255, 255, 0.04)",
                        borderBottom: "1px solid rgba(255, 255, 255, 0.1)",
                    }}
                >
                    <div style={{
                        fontSize: "22px",        
                        fontWeight: 900,
                        color: "#FFFFFF",
                        letterSpacing: "1.5px",
                        textTransform: "uppercase"
                    }}>
                        Transit Statistics
                    </div>
                </div>

                {/* 内容区域 */}
                <div style={{ padding: "44px 32px" }}>
                    <div style={{ display: "flex", alignItems: "center", gap: "48px" }}>
                        
                        {/* 环形图区域 */}
                        <div style={{ position: "relative", width: "180px", height: "180px", flexShrink: 0 }}>
                            {/* 外部色彩圆环层 */}
                            <div style={{
                                width: "100%",
                                height: "100%",
                                borderRadius: "50%",
                                background: pieBackground,
                                boxShadow: "0 0 45px rgba(0,0,0,0.6)",
                                zIndex: 1
                            }} />
                            
                            {/* 内部空心层 */}
                            <div style={{
                                position: "absolute",
                                inset: "32px",
                                borderRadius: "50%",
                                background: "#121519", 
                                display: "flex",
                                flexDirection: "column",
                                justifyContent: "center",
                                alignItems: "center",
                                boxShadow: "inset 0 0 25px rgba(0,0,0,0.8)",
                                zIndex: 10               // 确保在最上层
                            }}>
                                {hasTraffic ? (
                                    <>
                                        {/* 有交通流量时显示总数 */}
                                        <div style={{ fontSize: "52px", fontWeight: 950, color: "#FFFFFF", lineHeight: 1 }}>
                                            {totalSources}
                                        </div>
                                        {/* Total 标签 - 改为纯白色不透明文字 */}
                                        <div style={{ 
                                            fontSize: "14px",      
                                            fontWeight: 700,
                                            color: "#FFFFFF",    // 纯白色
                                            textTransform: "uppercase", 
                                            marginTop: "10px",
                                            letterSpacing: "2px"
                                        }}>
                                            Total
                                        </div>
                                    </>
                                ) : (
                                    // 无流量时中心显示淡色 0
                                    <div style={{ fontSize: "52px", fontWeight: 950, color: "rgba(255,255,255,0.1)" }}>
                                        0
                                    </div>
                                )}
                            </div>
                        </div>

                        {/* 右侧详情 / 无流量提示 */}
                        <div style={{ flexGrow: 1, display: "flex", flexDirection: "column", gap: "18px" }}>
                            {hasTraffic ? (
                                topBuckets.map((bucket) => {
                                    const ratio = ((bucket.sourceCount / totalSources) * 100).toFixed(1);
                                    const color = kindColorMap[bucket.kind];
                                    return (
                                        <div key={bucket.kind} style={{ display: "flex", alignItems: "center", gap: "16px" }}>
                                            <div style={{ width: "12px", height: "12px", borderRadius: "50%", backgroundColor: color, flexShrink: 0 }} />
                                            <div style={{ fontSize: "17px", color: "#FFFFFF", flexGrow: 1, fontWeight: 600 }}>
                                                {translate(kindLabelKeyMap[bucket.kind], bucket.kind)}
                                            </div>
                                            <div style={{ fontSize: "17px", fontWeight: 800, color: "#FFFFFF", width: "55px", textAlign: "right" }}>
                                                {bucket.sourceCount}
                                            </div>
                                            <div style={{ fontSize: "14px", color: "rgba(255, 255, 255, 0.4)", width: "65px", textAlign: "right" }}>
                                                {ratio}%
                                            </div>
                                        </div>
                                    );
                                })
                            ) : (
                                // 无流量时，No Traffic 字样挪到右侧放大显示
                                <div style={{ display: "flex", flexDirection: "column", gap: "12px" }}>
                                    <div style={{ 
                                        fontSize: "44px", 
                                        fontWeight: 950, 
                                        color: "#FFFFFF", 
                                        textTransform: "uppercase",
                                        letterSpacing: "1px"
                                    }}>
                                        No Traffic
                                    </div>
                                    {/* 等待字样使用双行显示 */}
                                    <div style={{ 
                                        fontSize: "18px", 
                                        color: "rgba(255, 255, 255, 0.4)", 
                                        lineHeight: 1.4,
                                        fontWeight: 500
                                    }}>
                                        Waiting for transit data<br />
                                        to be collected...
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

/**
 * 格式化后端原始数据
 */
function normalizePayload(payload: RouteStatisticsPanelPayload): NormalizedRouteStatisticsPanelPayload {
    const bucketMap = new Map<RouteVisualizationKind, NormalizedRouteStatisticsBucket>();
    for (const bucket of payload.buckets ?? []) {
        const normalizedBucket = normalizeBucket(bucket);
        if (normalizedBucket) {
            bucketMap.set(normalizedBucket.kind, normalizedBucket);
        }
    }

    const buckets = orderedKinds.map((kind) => {
        return bucketMap.get(kind) ?? {
            kind,
            sourceCount: 0,
            truncated: false,
            lines: []
        };
    });

    return { ...payload, buckets };
}

function normalizeBucket(bucket: RouteStatisticsBucket): NormalizedRouteStatisticsBucket | null {
    const kind = normalizeKind(bucket.kind);
    return kind ? { ...bucket, kind, lines: bucket.lines ?? [] } : null;
}

function normalizeKind(kind: RouteStatisticsBucket["kind"]): RouteVisualizationKind | null {
    if (typeof kind === "string" && orderedKinds.includes(kind as RouteVisualizationKind)) return kind as RouteVisualizationKind;
    if (typeof kind === "number" && kind >= 0 && kind < orderedKinds.length) return orderedKinds[kind];
    return null;
}

/**
 * 构建环形图背景
 */
function buildPieGradient(buckets: NormalizedRouteStatisticsBucket[], totalSources: number) {
    // 无流量时显示白色圆环
    if (totalSources <= 0) return "rgba(255, 255, 255, 0.85)";

    let offset = 0;
    const stops = buckets
        .filter((bucket) => bucket.sourceCount > 0)
        .map((bucket) => {
            const start = offset;
            offset += (bucket.sourceCount / totalSources) * 360;
            return `${kindColorMap[bucket.kind]} ${start}deg ${offset}deg`;
        });

    return stops.length === 0 ? "rgba(255, 255, 255, 0.85)" : `conic-gradient(${stops.join(", ")})`;
}
