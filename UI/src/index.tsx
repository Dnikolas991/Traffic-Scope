import { ModRegistrar } from "cs2/modding";
import { SelectionButton } from "./SelectionButton";

const register: ModRegistrar = (moduleRegistry) => {
    moduleRegistry.append("GameTopLeft", SelectionButton);
    console.log("Transit Scope UI mounted at GameTopLeft.");
};

export default register;
