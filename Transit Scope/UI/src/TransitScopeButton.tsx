import React from "react";
import { useValue } from "cs2/api";
import { isActiveBinding, toggleTransitScope } from "./bindings";
import { TransitScopeIcon } from "./TransitScopeIcon";
import { TransitScopeStatsPanel } from "./TransitScopeStatsPanel";

/**
 * 左上角入口按钮 + 选中后的统计卡片。
 * 保持整体布局紧凑，不做额外复杂面板。
 */
export const TransitScopeButton = () => {
    const isActive = useValue(isActiveBinding);

    const handleToggle = () => {
        toggleTransitScope(!isActive);
    };

    return (
        <div style={{ display: "flex", flexDirection: "column", alignItems: "flex-start" }}>
            <div
                style={{
                    pointerEvents: "auto",
                    display: "flex",
                    alignItems: "center",
                    gap: "6px",
                    margin: "4px 8px",
                    padding: "2px"
                }}
            >
                <button
                    onClick={handleToggle}
                    title="Transit Scope"
                    style={{
                        width: "38px",
                        height: "38px",
                        borderRadius: "9px",
                        border: isActive
                            ? "1px solid rgba(116,196,255,0.96)"
                            : "1px solid rgba(255,255,255,0.16)",
                        background: isActive
                            ? "linear-gradient(180deg, rgba(67,97,128,0.96) 0%, rgba(34,47,63,0.98) 100%)"
                            : "linear-gradient(180deg, rgba(62,69,79,0.94) 0%, rgba(35,40,47,0.98) 100%)",
                        boxShadow: isActive
                            ? "inset 0 0 0 1px rgba(255,255,255,0.10), 0 1px 3px rgba(0,0,0,0.32)"
                            : "inset 0 0 0 1px rgba(255,255,255,0.04), 0 1px 3px rgba(0,0,0,0.32)",
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        cursor: "pointer",
                        padding: 0,
                        transition: "background 120ms ease, border-color 120ms ease, box-shadow 120ms ease"
                    }}
                >
                    <TransitScopeIcon active={isActive} />
                </button>
            </div>

            <TransitScopeStatsPanel />
        </div>
    );
};
