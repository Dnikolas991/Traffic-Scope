import React from 'react';
import { useValue } from "cs2/api";
import {
    isActiveBinding,
    hoveredEdgeIdBinding,
    selectedEdgeIdBinding,
    statusTextBinding,
    toggleTransitScope
} from "./bindings";

export const TransitScopeButton = () => {
    const isActive = useValue(isActiveBinding);
    const hoveredEdgeId = useValue(hoveredEdgeIdBinding);
    const selectedEdgeId = useValue(selectedEdgeIdBinding);
    const statusText = useValue(statusTextBinding);

    const handleToggle = () => {
        toggleTransitScope(!isActive);
    };

    const hasHovered = hoveredEdgeId >= 0;
    const hasSelected = selectedEdgeId >= 0;

    return (
        <div
            style={{
                pointerEvents: 'auto',
                display: 'flex',
                margin: '4px 8px',
                alignItems: 'center',
                gap: '8px'
            }}
        >
            <div
                onClick={handleToggle}
                title={`交通观测镜 (Transit Scope)\n${statusText}`}
                style={{
                    width: '42px',
                    height: '42px',
                    backgroundColor: isActive ? 'rgba(52, 152, 219, 0.4)' : 'rgba(0, 0, 0, 0.6)',
                    backdropFilter: 'blur(5px)',
                    border: isActive ? '1px solid rgba(82, 186, 255, 0.8)' : '1px solid rgba(255, 255, 255, 0.15)',
                    borderRadius: '6px',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    cursor: 'pointer',
                    color: isActive ? '#ffffff' : '#b0b0b0',
                    transition: 'all 0.15s ease-in-out',
                    position: 'relative'
                }}
            >
                <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" strokeWidth="2">
                    <circle cx="12" cy="12" r="3" />
                    <path d="M2 12c0 0 4-8 10-8s10 8 10 8-4 8-10 8-10-8-10-8z" />
                </svg>

                {/* 右下角小状态点：
                   蓝色 = 已锁定
                   白色 = 仅悬停
                */}
                {isActive && (
                    <div
                        style={{
                            position: 'absolute',
                            right: '4px',
                            bottom: '4px',
                            width: '8px',
                            height: '8px',
                            borderRadius: '50%',
                            backgroundColor: hasSelected
                                ? 'rgba(82, 186, 255, 1)'
                                : hasHovered
                                    ? 'rgba(255, 255, 255, 0.9)'
                                    : 'rgba(255, 255, 255, 0.2)',
                            boxShadow: hasSelected
                                ? '0 0 6px rgba(82, 186, 255, 0.9)'
                                : 'none'
                        }}
                    />
                )}
            </div>

            {isActive && (
                <div
                    style={{
                        minWidth: '156px',
                        maxWidth: '240px',
                        padding: '6px 10px',
                        backgroundColor: 'rgba(0, 0, 0, 0.55)',
                        border: '1px solid rgba(255, 255, 255, 0.12)',
                        borderRadius: '6px',
                        color: '#e8f3ff',
                        fontSize: '12px',
                        lineHeight: 1.35,
                        backdropFilter: 'blur(5px)',
                        whiteSpace: 'nowrap',
                        overflow: 'hidden',
                        textOverflow: 'ellipsis'
                    }}
                    title={statusText}
                >
                    {statusText}
                </div>
            )}
        </div>
    );
};