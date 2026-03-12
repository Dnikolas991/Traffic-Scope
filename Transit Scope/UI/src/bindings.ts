import { bindValue, trigger } from "cs2/api";

// 1. 绑定后端的 ValueBinding
// 参数分别对应 C# 中的组名 "transitScope", 绑定名 "isActive", 以及默认值 false
export const isActiveBinding = bindValue<boolean>("transitScope", "isActive", false);

// 2. 封装发送给后端的 Trigger
export const toggleTransitScope = (active: boolean) => {
    trigger("transitScope", "toggle", active);
};