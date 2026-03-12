import { ModRegistrar } from "cs2/modding";
import { TransitScopeButton } from "./TransitScopeButton";

// 这是由 create-csii-ui-mod 模板支持的标准导出格式
const register: ModRegistrar = (moduleRegistry) => {
    // 将按钮挂载到原版左上角界面 (GameTopLeft)
    moduleRegistry.append('GameTopLeft', TransitScopeButton);
    console.log("Transit Scope UI 已成功注册到官方模块注册表！");
};

export default register;