import React, { useState } from "react";
import { Portal } from "cs2/ui";
import { useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { hasStatsBinding, isActiveBinding, statsJsonBinding } from "./bindings";

/** 统计项数据接口 */
interface StatItem {
    labelKey?: string;
    label: string;
    value: number;
    color: string;
}

/** 统计面板负载数据接口 */
interface StatsPayload {
    titleKey?: string;
    title: string;
    subtitleKey?: string;
    subtitleArg?: string;
    subtitle: string;
    total: number;
    displayTotal?: number;
    items: StatItem[];
}

/** 锚点位置接口 */
interface AnchorPosition {
    x: number;
    y: number;
}

interface Props {
    anchor: AnchorPosition | null;
}

/**
 * 重构后的统计面板组件。
 * 
 * 视觉优化点:
 * 1. 结构: 使用 Header + Body 的经典分层布局，增强专业感。
 * 2. 颜色: 深色背景 (rgba(24, 28, 33, 0.96)) 配合淡蓝色强调色 (accentColor)。
 * 3. 交互: 列表项支持 Hover 高亮，增强操作反馈。
 * 4. 图表: 优化 Conic-Gradient 计算，并在圆环中心增加立体感装饰。
 */
export const TransitScopeStatsPanel = ({ anchor }: Props) => {
    const isActive = useValue(isActiveBinding);
    const hasStats = useValue(hasStatsBinding);
    const statsJson = useValue(statsJsonBinding);
    const { translate } = useLocalization();
    
    // 当前悬停的统计项索引
    const [hoveredIndex, setHoveredIndex] = useState<number | null>(null);

    // 本地化辅助函数
    const localize = (key: string | undefined, fallback: string, arg?: string): string => {
        const template = key ? (translate(key, fallback) ?? fallback) : fallback;
        return arg !== undefined ? template.replace("{0}", arg) : template;
    };

    // 状态检查
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

    // 计算图表扇区
    const chartTotal = Math.max(1, payload.total || 0);
    const displayTotal = payload.displayTotal ?? payload.total ?? 0;
    let currentAngle = 0;

    const segments = payload.items.map((item) => {
        const slice = (item.value / chartTotal) * 360;
        const start = currentAngle;
        const end = currentAngle + slice;
        currentAngle = end;
        return `${item.color} ${start.toFixed(2)}deg ${end.toFixed(2)}deg`;
    });

    const pieStyle = {
        background: `conic-gradient(${segments.join(", ")})`
    } as React.CSSProperties;

    const localizedTitle = localize(payload.titleKey, payload.title);
    const localizedSubtitle = localize(payload.subtitleKey, payload.subtitle, payload.subtitleArg);

    return (
        <Portal>
            <div
                style={{
                    position: "absolute",
                    top: `${anchor.y}px`,
                    left: `${anchor.x}px`,
                    pointerEvents: "auto",
                    width: "580px",
                    borderRadius: "12px",
                    overflow: "hidden",
                    // 渐变深色背景，模仿原版面板质感
                    background: "linear-gradient(145deg, rgba(32, 38, 45, 0.98) 0%, rgba(20, 24, 28, 0.98) 100%)",
                    border: "1px solid rgba(255, 255, 255, 0.08)",
                    boxShadow: "0 12px 32px rgba(0, 0, 0, 0.45), inset 0 1px 1px rgba(255, 255, 255, 0.05)",
                    transition: "all 0.2s ease-in-out"
                }}
            >
                {/* 面板页眉 */}
                <div
                    style={{
                        padding: "14px 20px",
                        background: "rgba(0, 0, 0, 0.25)",
                        borderBottom: "1px solid rgba(255, 255, 255, 0.06)",
                        display: "flex",
                        alignItems: "baseline",
                        justifyContent: "space-between",
                        gap: "16px"
                    }}
                >
                    <div
                        style={{
                            fontSize: "16px",
                            fontWeight: 800,
                            color: "var(--accentColorNormal, #8fd5ff)",
                            textTransform: "uppercase",
                            letterSpacing: "0.8px"
                        }}
                    >
                        {localizedTitle}
                    </div>
                    <div
                        style={{
                            fontSize: "13px",
                            fontWeight: 500,
                            color: "rgba(255, 255, 255, 0.55)",
                            fontStyle: "italic",
                            whiteSpace: "nowrap",
                            overflow: "hidden",
                            textOverflow: "ellipsis"
                        }}
                    >
                        {localizedSubtitle}
                    </div>
                </div>

                {/* 内容区域: 图表 + 列表 */}
                <div
                    style={{
                        padding: "24px 20px",
                        display: "grid",
                        gridTemplateColumns: "200px 1fr",
                        columnGap: "24px",
                        alignItems: "center"
                    }}
                >
                    {/* 左侧圆环图 */}
                    <div
                        style={{
                            position: "relative",
                            width: "180px",
                            height: "180px",
                            borderRadius: "50%",
                            padding: "4px",
                            background: "rgba(0, 0, 0, 0.3)",
                            boxShadow: "inset 0 2px 4px rgba(0, 0, 0, 0.4)"
                        }}
                    >
                        {/* 彩色圆环 */}
                        <div
                            style={{
                                width: "100%",
                                height: "100%",
                                borderRadius: "50%",
                                ...pieStyle,
                                transform: hoveredIndex !== null ? "scale(1.02)" : "scale(1)",
                                transition: "transform 0.3s cubic-bezier(0.175, 0.885, 0.32, 1.275)"
                            }}
                        />
                        {/* 中心数值区 */}
                        <div
                            style={{
                                position: "absolute",
                                inset: "34px",
                                borderRadius: "50%",
                                background: "linear-gradient(135deg, #1a1f26 0%, #0d1014 100%)",
                                border: "1px solid rgba(255, 255, 255, 0.08)",
                                boxShadow: "0 4px 10px rgba(0,0,0,0.3)",
                                display: "flex",
                                flexDirection: "column",
                                alignItems: "center",
                                justifyContent: "center",
                                zIndex: 2
                            }}
                        >
                            <div style={{ fontSize: "12px", color: "rgba(255, 255, 255, 0.45)", fontWeight: 600 }}>
                                {localize("stats.total", "TOTAL")}
                            </div>
                            <div style={{ fontSize: "32px", fontWeight: 800, color: "#FFFFFF", textShadow: "0 0 12px rgba(255,255,255,0.1)" }}>
                                {displayTotal}
                            </div>
                        </div>
                    </div>

                    {/* 右侧详细列表 */}
                    <div
                        style={{
                            display: "flex",
                            flexDirection: "column",
                            gap: "8px"
                        }}
                    >
                        {payload.items.map((item, index) => {
                            const percent = ((item.value / chartTotal) * 100).toFixed(1);
                            const localizedLabel = localize(item.labelKey, item.label);
                            const isHovered = hoveredIndex === index;

                            return (
                                <div
                                    key={`${item.labelKey || item.label}-${item.color}`}
                                    onMouseEnter={() => setHoveredIndex(index)}
                                    onMouseLeave={() => setHoveredIndex(null)}
                                    style={{
                                        display: "grid",
                                        gridTemplateColumns: "12px 1fr auto",
                                        alignItems: "center",
                                        columnGap: "12px",
                                        padding: "10px 14px",
                                        borderRadius: "8px",
                                        background: isHovered ? "rgba(255, 255, 255, 0.08)" : "rgba(255, 255, 255, 0.03)",
                                        border: "1px solid",
                                        borderColor: isHovered ? "rgba(255, 255, 255, 0.12)" : "transparent",
                                        transition: "all 0.15s ease",
                                        cursor: "default"
                                    }}
                                >
                                    {/* 颜色指示器 */}
                                    <div
                                        style={{
                                            width: "8px",
                                            height: "8px",
                                            borderRadius: "50%",
                                            background: item.color,
                                            boxShadow: isHovered ? `0 0 8px ${item.color}` : "none",
                                            transition: "box-shadow 0.2s ease"
                                        }}
                                    />
                                    {/* 标签名 */}
                                    <div
                                        style={{
                                            fontSize: "14px",
                                            fontWeight: 500,
                                            color: isHovered ? "#FFFFFF" : "rgba(255, 255, 255, 0.85)",
                                            whiteSpace: "nowrap",
                                            overflow: "hidden",
                                            textOverflow: "ellipsis"
                                        }}
                                    >
                                        {localizedLabel}
                                    </div>
                                    {/* 百分比数值 */}
                                    <div
                                        style={{
                                            fontSize: "14px",
                                            fontWeight: 700,
                                            color: isHovered ? "var(--accentColorNormal, #8fd5ff)" : "#FFFFFF",
                                            minWidth: "45px",
                                            textAlign: "right"
                                        }}
                                    >
                                        {percent}%
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                </div>
            </div>
        </Portal>
    );
};
