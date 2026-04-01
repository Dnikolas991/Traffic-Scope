import React from "react";
import { useValue } from "cs2/api";
import { isActiveBinding, toggleTransitScope } from "./bindings";
import { TransitScopeIcon } from "./TransitScopeIcon";
import { TransitScopeStatsPanel } from "./TransitScopeStatsPanel";

/**
 * 左上角入口按钮。
 * 这次按参考模组 Traffic 的风格回收视觉：
 * 1. 按钮尺寸恢复成更接近原版浮动工具按钮的体量。
 * 2. 去掉当前这层偏“玻璃卡片”的自定义外观。
 * 3. 只保留图标主体和淡蓝色底图。
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
                    margin: "8px 10px 0",
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

            <TransitScopeStatsPanel />
        </div>
    );
};
