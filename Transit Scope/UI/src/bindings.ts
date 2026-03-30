import { bindValue, trigger } from "cs2/api";

/**
 * 后端 ValueBinding
 */
export const isActiveBinding = bindValue<boolean>("transitScope", "isActive");
export const hoveredEdgeIdBinding = bindValue<number>("transitScope", "hoveredEdgeId");
export const selectedEdgeIdBinding = bindValue<number>("transitScope", "selectedEdgeId");
export const statusTextBinding = bindValue<string>("transitScope", "statusText");

/**
 * 后端 TriggerBinding
 */
export const toggleTransitScope = (active: boolean) => {
    trigger("transitScope", "toggle", active);
};

export const confirmTransitScope = () => {
    trigger("transitScope", "confirm");
};