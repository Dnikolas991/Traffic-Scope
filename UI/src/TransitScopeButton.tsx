import React, { useLayoutEffect, useRef, useState } from "react";
import { useValue } from "cs2/api";
import { Button } from "cs2/ui";
import { hasStatsBinding, isActiveBinding, toggleTransitScope } from "./bindings";
import { TransitScopeIcon } from "./TransitScopeIcon";
import { TransitScopeStatsPanel } from "./TransitScopeStatsPanel";

/** 锚点位置接口，用于定位浮动面板 */
interface AnchorPosition {
    x: number;
    y: number;
}

/**
 * Transit Scope 的主入口按钮组件。
 * 
 * 设计参考: 
 * 1. 挂载于 GameTopLeft，与原版及 Traffic 模组对齐。
 * 2. 使用 cs2/ui 的 Button 组件，variant="floating" 以获得原版悬浮质感。
 * 3. 统计面板通过 Portal 渲染，并根据按钮位置实时计算锚点，避免 UI 遮挡。
 */
export const TransitScopeButton = () => {
    // 绑定游戏状态：是否激活，以及是否有统计数据需要展示
    const isActive = useValue(isActiveBinding);
    const hasStats = useValue(hasStatsBinding);
    
    // 引用容器以计算位置
    const containerRef = useRef<HTMLDivElement | null>(null);
    const [anchor, setAnchor] = useState<AnchorPosition | null>(null);

    // 切换模组激活状态
    const handleToggle = () => {
        toggleTransitScope(!isActive);
    };

    // 动态更新面板锚点位置
    useLayoutEffect(() => {
        const updateAnchor = () => {
            if (!containerRef.current) {
                setAnchor(null);
                return;
            }

            const rect = containerRef.current.getBoundingClientRect();
            // 将面板定位在按钮下方，并留出 8px 间距
            setAnchor({
                x: rect.left,
                y: rect.bottom + 8
            });
        };

        updateAnchor();
        window.addEventListener("resize", updateAnchor);
        return () => window.removeEventListener("resize", updateAnchor);
    }, [isActive, hasStats]);

    return (
        <>
            {/* 按钮容器 */}
            <div
                ref={containerRef}
                style={{
                    pointerEvents: "auto",
                    margin: "4px 8px",
                    position: "relative"
                }}
            >
                {/* 
                  * 使用原版 UI 组件 Button。
                  * variant="floating" 提供标准的阴影和背景。
                  * selected 属性对应选中时的高亮样式（与 Traffic 一致）。
                  */}
                <Button
                    variant="floating"
                    selected={isActive}
                    onSelect={handleToggle}
                    tooltipLabel="Transit Scope"
                    style={{
                        width: "56px",
                        height: "56px"
                    }}
                >
                    <TransitScopeIcon active={isActive} />
                </Button>
            </div>

            {/* 统计数据浮层 */}
            <TransitScopeStatsPanel anchor={anchor} />
        </>
    );
};
