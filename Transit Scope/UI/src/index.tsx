import { ModRegistrar } from "cs2/modding";
import { TransitScopeButton } from "./TransitScopeButton";

const register: ModRegistrar = (moduleRegistry) => {
    moduleRegistry.append("GameTopLeft", TransitScopeButton);
    console.log("Transit Scope UI mounted at GameTopLeft.");
};

export default register;
