import React, { useLayoutEffect, useRef, useState } from "react";
import { useValue } from "cs2/api";
import { hasStatsBinding, isActiveBinding, toggleTransitScope } from "./bindings";
import { TransitScopeIcon } from "./TransitScopeIcon";
import { TransitScopeStatsPanel } from "./TransitScopeStatsPanel";

interface AnchorPosition {
    x: number;
    y: number;
}

/**
 * 左上角入口按钮。
 * 这里参考 Traffic 的挂载方式，保留入口按钮挂在 GameTopLeft，
 * 但把统计面板改成基于按钮位置的浮层锚定，而不是继续放在普通文档流里。
 * 这样按钮不会被面板顶歪，整体位置也更接近原版工具入口。
 */
export const TransitScopeButton = () => {
    const isActive = useValue(isActiveBinding);
    const hasStats = useValue(hasStatsBinding);
    const containerRef = useRef<HTMLDivElement | null>(null);
    const [anchor, setAnchor] = useState<AnchorPosition | null>(null);

    const handleToggle = () => {
        toggleTransitScope(!isActive);
    };

    useLayoutEffect(() => {
        const updateAnchor = () => {
            if (!containerRef.current) {
                setAnchor(null);
                return;
            }

            const rect = containerRef.current.getBoundingClientRect();
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
            <div
                ref={containerRef}
                style={{
                    pointerEvents: "auto",
                    margin: "6px 8px 0",
                    position: "relative"
                }}
            >
                <button
                    onClick={handleToggle}
                    title="Transit Scope"
                    style={{
                        width: "56px",
                        height: "56px",
                        borderRadius: "14px",
                        border: isActive
                            ? "1px solid rgba(235,248,255,0.88)"
                            : "1px solid rgba(122,155,175,0.82)",
                        background: isActive
                            ? "linear-gradient(180deg, rgba(116,194,231,0.98) 0%, rgba(78,149,184,0.98) 100%)"
                            : "linear-gradient(180deg, rgba(98,170,204,0.94) 0%, rgba(67,122,150,0.96) 100%)",
                        boxShadow: isActive
                            ? "inset 0 1px 0 rgba(255,255,255,0.32), 0 4px 12px rgba(0,0,0,0.28)"
                            : "inset 0 1px 0 rgba(255,255,255,0.18), 0 4px 10px rgba(0,0,0,0.24)",
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        cursor: "pointer",
                        padding: 0
                    }}
                >
                    <TransitScopeIcon active={isActive} />
                </button>
            </div>

            <TransitScopeStatsPanel anchor={anchor} />
        </>
    );
};
