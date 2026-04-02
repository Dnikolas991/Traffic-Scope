import { ModRegistrar } from "cs2/modding";
import { ScopeButton } from "./ScopeButton";

const register: ModRegistrar = (moduleRegistry) => {
    moduleRegistry.append("GameTopLeft", ScopeButton);
    console.log("Transit Scope UI mounted at GameTopLeft.");
};

export default register;
