import React, { useLayoutEffect, useRef, useState } from "react";
import { useValue } from "cs2/api";
import { Button } from "cs2/ui";
import { hasStatsBinding, isActiveBinding, toggleTransitScope } from "./bindings";
import { SelectionIcon } from "./SelectionIcon";
import { StatsPanel } from "./StatsPanel";

interface AnchorPosition {
    x: number;
    y: number;
}

export const SelectionButton = () => {
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
            <div ref={containerRef}>
                <Button
                    variant="floating"
                    selected={isActive}
                    onSelect={handleToggle}
                    tooltipLabel="Transit Scope"
                >
                    <SelectionIcon active={isActive} />
                </Button>
            </div>

            <StatsPanel anchor={anchor} />
        </>
    );
};
