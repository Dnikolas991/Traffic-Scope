import { bindValue, trigger } from "cs2/api";

export const isActiveBinding = bindValue<boolean>("transitScope", "isActive");
export const hasStatsBinding = bindValue<boolean>("transitScope", "hasStats");
export const statsJsonBinding = bindValue<string>("transitScope", "statsJson");

export const toggleTransitScope = (active: boolean) => {
    trigger("transitScope", "toggle", active);
};

export const confirmTransitScope = () => {
    trigger("transitScope", "confirm");
};
