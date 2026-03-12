import React from 'react';
import { useValue } from "cs2/api"; // 引入官方的 Hook
import { isActiveBinding, toggleTransitScope } from "./bindings";

export const TransitScopeButton = () => {
    // 【核心变化】使用 useValue 订阅后端的绑定。
    // 只要 C# 端调用了 m_ActiveBinding.Update()，这个变量就会自动更新，触发按钮重绘！
    const isActive = useValue(isActiveBinding);

    const handleToggle = () => {
        // 通知后端切换状态，不在这里自己改 isActive
        toggleTransitScope(!isActive);
    };

    return (
        <div style={{ pointerEvents: 'auto', display: 'flex', margin: '4px 8px' }}>
            <div
                onClick={handleToggle}
                title="交通观测镜 (Transit Scope)"
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
                    transition: 'all 0.15s ease-in-out'
                }}
            >
                <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" strokeWidth="2">
                    <circle cx="12" cy="12" r="3" />
                    <path d="M2 12c0 0 4-8 10-8s10 8 10 8-4 8-10 8-10-8-10-8z" />
                </svg>
            </div>
        </div>
    );
};