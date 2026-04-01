import React from "react";
import { Portal } from "cs2/ui";
import { useValue } from "cs2/api";
import { hasStatsBinding, isActiveBinding, statsJsonBinding } from "./bindings";

interface StatItem {
    label: string;
    value: number;
    color: string;
}

interface StatsPayload {
    title: string;
    subtitle: string;
    total: number;
    items: StatItem[];
}

interface AnchorPosition {
    x: number;
    y: number;
}

interface Props {
    anchor: AnchorPosition | null;
}

/**
 * 统计面板使用 Portal 浮层挂载。
 * 这样它的位置由入口按钮锚点决定，不会再把 GameTopLeft 的布局撑歪。
 *
 * 排版上改成：
 * 1. 左侧更大的饼图。
 * 2. 右侧双列图例卡片。
 * 3. 字号显著抬高，不再是一列小字从上往下排。
 */
export const TransitScopeStatsPanel = ({ anchor }: Props) => {
    const isActive = useValue(isActiveBinding);
    const hasStats = useValue(hasStatsBinding);
    const statsJson = useValue(statsJsonBinding);

    if (!isActive || !hasStats || !statsJson || !anchor) {
        return null;
    }

    let payload: StatsPayload | null = null;

    try {
        payload = JSON.parse(statsJson) as StatsPayload;
    } catch (error) {
        console.error("Transit Scope stats parse failed:", error);
        return null;
    }

    if (!payload || !payload.items || payload.items.length === 0) {
        return null;
    }

    const total = Math.max(1, payload.total || 0);
    let currentAngle = 0;

    const segments = payload.items.map((item) => {
        const slice = (item.value / total) * 360;
        const start = currentAngle;
        const end = currentAngle + slice;
        currentAngle = end;
        return `${item.color} ${start.toFixed(2)}deg ${end.toFixed(2)}deg`;
    });

    const pieStyle = {
        background: `conic-gradient(${segments.join(", ")})`
    } as React.CSSProperties;

    return (
        <Portal>
            <div
                style={{
                    position: "absolute",
                    top: `${anchor.y}px`,
                    left: `${anchor.x}px`,
                    pointerEvents: "none",
                    width: "560px",
                    borderRadius: "16px",
                    overflow: "hidden",
                    backgroundColor: "rgba(80,80,80,0.94)",
                    boxShadow: "0 8px 20px rgba(0,0,0,0.28)"
                }}
            >
                <div
                    style={{
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "space-between",
                        gap: "12px",
                        padding: "12px 16px",
                        backgroundColor: "rgba(36,36,36,0.98)",
                        color: "var(--accentColorNormal, #8fd5ff)"
                    }}
                >
                    <div
                        style={{
                            fontSize: "15px",
                            fontWeight: 700,
                            letterSpacing: "0.6px",
                            textTransform: "uppercase"
                        }}
                    >
                        {payload.title}
                    </div>
                    <div
                        style={{
                            fontSize: "13px",
                            color: "rgba(255,255,255,0.74)",
                            whiteSpace: "nowrap",
                            overflow: "hidden",
                            textOverflow: "ellipsis"
                        }}
                    >
                        {payload.subtitle}
                    </div>
                </div>

                <div
                    style={{
                        padding: "16px",
                        display: "grid",
                        gridTemplateColumns: "190px minmax(0, 1fr)",
                        columnGap: "18px",
                        alignItems: "center"
                    }}
                >
                    <div
                        style={{
                            position: "relative",
                            width: "176px",
                            height: "176px",
                            borderRadius: "999px",
                            justifySelf: "center",
                            ...pieStyle
                        }}
                    >
                        <div
                            style={{
                                position: "absolute",
                                inset: "30px",
                                borderRadius: "999px",
                                background: "rgba(15,16,19,0.98)",
                                border: "1px solid rgba(255,255,255,0.08)",
                                display: "flex",
                                flexDirection: "column",
                                alignItems: "center",
                                justifyContent: "center"
                            }}
                        >
                            <div style={{ fontSize: "13px", color: "rgba(255,255,255,0.68)" }}>Total</div>
                            <div style={{ marginTop: "4px", fontSize: "30px", fontWeight: 700, color: "#FFFFFF" }}>{payload.total}</div>
                        </div>
                    </div>

                    <div
                        style={{
                            display: "grid",
                            gridTemplateColumns: "repeat(2, minmax(0, 1fr))",
                            gap: "10px 12px",
                            minWidth: 0
                        }}
                    >
                        {payload.items.map((item) => {
                            const percent = ((item.value / total) * 100).toFixed(1);

                            return (
                                <div
                                    key={item.label}
                                    style={{
                                        display: "grid",
                                        gridTemplateColumns: "12px minmax(0, 1fr) auto",
                                        alignItems: "center",
                                        columnGap: "8px",
                                        minWidth: 0,
                                        padding: "8px 10px",
                                        borderRadius: "10px",
                                        backgroundColor: "rgba(15,16,19,0.60)"
                                    }}
                                >
                                    <div
                                        style={{
                                            width: "10px",
                                            height: "10px",
                                            borderRadius: "999px",
                                            background: item.color
                                        }}
                                    />
                                    <div
                                        style={{
                                            minWidth: 0,
                                            fontSize: "15px",
                                            color: "#FFFFFF",
                                            whiteSpace: "nowrap",
                                            overflow: "hidden",
                                            textOverflow: "ellipsis"
                                        }}
                                    >
                                        {item.label}
                                    </div>
                                    <div style={{ fontSize: "15px", fontWeight: 700, color: "#FFFFFF" }}>{percent}%</div>
                                </div>
                            );
                        })}
                    </div>
                </div>
            </div>
        </Portal>
    );
};
