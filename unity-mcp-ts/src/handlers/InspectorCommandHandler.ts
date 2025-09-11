import { IMcpToolDefinition } from "../core/interfaces/ICommandHandler.js";
import { JObject } from "../types/index.js";
import { z } from "zod";
import { BaseCommandHandler } from "../core/BaseCommandHandler.js";

/**
 * Command handler for Inspector operations - component manipulation on GameObjects.
 */
export class InspectorCommandHandler extends BaseCommandHandler {
    /**
     * Gets the command prefix for this handler.
     */
    public get commandPrefix(): string {
        return "inspector";
    }

    /**
     * Gets the description of this command handler.
     */
    public get description(): string {
        return "Manipulate components on GameObjects (add, remove, modify, find references, prefab operations)";
    }

    /**
     * Gets the tool definitions supported by this handler.
     * @returns A map of tool names to their definitions.
     */
    public getToolDefinitions(): Map<string, IMcpToolDefinition> {
        const tools = new Map<string, IMcpToolDefinition>();

        // inspector.addComponent - Add a component to a GameObject
        tools.set("inspector_addComponent", {
            description: "Add components to GameObjects to give them functionality like rendering, physics, or custom behaviors. Components define what GameObjects can do.",
            parameterSchema: {
                path: z.string().describe("GameObject path in scene (e.g., 'Player/Body') or prefab path (e.g., 'Assets/Prefabs/Character.prefab')"),
                componentType: z.string().describe("Component class name like 'Rigidbody2D' (physics), 'SpriteRenderer' (2D graphics), 'Animator' (animations), 'BoxCollider2D' (collision)")
            },
            annotations: {
                title: "Add Component",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: false,
                openWorldHint: false
            }
        });

        // inspector.removeComponent - Remove a component from a GameObject
        tools.set("inspector_removeComponent", {
            description: "Remove a component from a GameObject",
            parameterSchema: {
                path: z.string().describe("Path to the GameObject"),
                componentType: z.string().describe("Type name of the component to remove"),
                index: z.number().optional().describe("Index of the component if multiple exist (default: 0)")
            },
            annotations: {
                title: "Remove Component",
                readOnlyHint: false,
                destructiveHint: true,
                idempotentHint: false,
                openWorldHint: false
            }
        });

        // inspector.modifyComponent - Modify component properties
        tools.set("inspector_modifyComponent", {
            description: "Modify component properties to change GameObject behavior, appearance, or physics. Use this to configure components after adding them, such as setting Transform positions, Rigidbody mass, or Material colors.",
            parameterSchema: {
                path: z.string().describe("Path to the GameObject in scene or prefab asset path (e.g., 'Assets/Prefabs/MyPrefab.prefab')"),
                componentType: z.string().describe("Type name of the component"),
                index: z.number().optional().describe("Index of the component if multiple exist (default: 0)"),
                properties: z.record(z.any()).describe("Object containing property names and their new values. For 2D colliders, use 'autoFit: true' to auto-size/shape based on sprite transparency.")
            },
            annotations: {
                title: "Modify Component",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        // inspector.enableComponent - Enable or disable a component
        tools.set("inspector_enableComponent", {
            description: "Enable or disable a component on a GameObject",
            parameterSchema: {
                path: z.string().describe("Path to the GameObject"),
                componentType: z.string().describe("Type name of the component"),
                enabled: z.boolean().describe("Whether to enable or disable the component"),
                index: z.number().optional().describe("Index of the component if multiple exist (default: 0)")
            },
            annotations: {
                title: "Enable/Disable Component",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        // inspector.copyComponent - Copy component values
        tools.set("inspector_copyComponent", {
            description: "Copy a component's values to clipboard for later pasting",
            parameterSchema: {
                path: z.string().describe("Path to the GameObject"),
                componentType: z.string().describe("Type name of the component to copy"),
                index: z.number().optional().describe("Index of the component if multiple exist (default: 0)")
            },
            annotations: {
                title: "Copy Component",
                readOnlyHint: true,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        // inspector.pasteComponent - Paste component values
        tools.set("inspector_pasteComponent", {
            description: "Paste previously copied component values to a component",
            parameterSchema: {
                path: z.string().describe("Path to the GameObject"),
                componentType: z.string().describe("Type name of the component to paste to"),
                index: z.number().optional().describe("Index of the component if multiple exist (default: 0)")
            },
            annotations: {
                title: "Paste Component",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        // inspector.resetComponent - Reset component to defaults
        tools.set("inspector_resetComponent", {
            description: "Reset a component to its default values",
            parameterSchema: {
                path: z.string().describe("Path to the GameObject"),
                componentType: z.string().describe("Type name of the component to reset"),
                index: z.number().optional().describe("Index of the component if multiple exist (default: 0)")
            },
            annotations: {
                title: "Reset Component",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        // inspector.getComponents - Get all components on a GameObject
        tools.set("inspector_getComponents", {
            description: "Get all components attached to a GameObject",
            parameterSchema: {
                path: z.string().describe("Path to the GameObject"),
                includeProperties: z.boolean().optional().describe("Include detailed property values (default: false)")
            },
            annotations: {
                title: "Get Components",
                readOnlyHint: true,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        // inspector.getComponentProperties - Get detailed properties of a component
        tools.set("inspector_getComponentProperties", {
            description: "Get detailed properties and values of a specific component",
            parameterSchema: {
                path: z.string().describe("Path to the GameObject"),
                componentType: z.string().describe("Type name of the component"),
                index: z.number().optional().describe("Index of the component if multiple exist (default: 0)")
            },
            annotations: {
                title: "Get Component Properties",
                readOnlyHint: true,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        // inspector.findReferences - Find all GameObjects with a component
        tools.set("inspector_findReferences", {
            description: "Find all GameObjects that have a specific component type",
            parameterSchema: {
                componentType: z.string().describe("Type name of the component to search for"),
                searchInactive: z.boolean().optional().describe("Include inactive GameObjects (default: true)"),
                searchPrefabs: z.boolean().optional().describe("Include prefab assets (default: false)"),
                limit: z.number().optional().describe("Maximum number of results (default: 100)")
            },
            annotations: {
                title: "Find Component References",
                readOnlyHint: true,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        // inspector.applyToPrefab - Apply changes to prefab
        tools.set("inspector_applyToPrefab", {
            description: "Apply GameObject or component changes to the prefab source",
            parameterSchema: {
                path: z.string().describe("Path to the prefab instance GameObject"),
                applyAll: z.boolean().optional().describe("Apply all overrides or just this GameObject (default: true)")
            },
            annotations: {
                title: "Apply to Prefab",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: false,
                openWorldHint: false
            }
        });

        // inspector.revertFromPrefab - Revert changes from prefab
        tools.set("inspector_revertFromPrefab", {
            description: "Revert GameObject or component changes to match the prefab source",
            parameterSchema: {
                path: z.string().describe("Path to the prefab instance GameObject"),
                revertAll: z.boolean().optional().describe("Revert all overrides or just this GameObject (default: true)")
            },
            annotations: {
                title: "Revert from Prefab",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        // inspector.isPrefab - Check prefab status
        tools.set("inspector_isPrefab", {
            description: "Check if a GameObject is a prefab instance or asset",
            parameterSchema: {
                path: z.string().describe("Path to the GameObject")
            },
            annotations: {
                title: "Check Prefab Status",
                readOnlyHint: true,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        // inspector.createPrefab - Create prefab asset from GameObject
        tools.set("inspector_createPrefab", {
            description: "Create a prefab asset from a GameObject in the scene",
            parameterSchema: {
                path: z.string().describe("Path to the GameObject in the scene"),
                prefabPath: z.string().describe("Asset path where the prefab should be created (e.g., 'Assets/Prefabs/MyPrefab.prefab')"),
                replacePrefab: z.boolean().optional().describe("Replace existing prefab if it exists (default: true)")
            },
            annotations: {
                title: "Create Prefab",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: false,
                openWorldHint: false
            }
        });

        // inspector.instantiatePrefab - Create GameObject instance from prefab
        tools.set("inspector_instantiatePrefab", {
            description: "Create a GameObject instance from a prefab asset",
            parameterSchema: {
                prefabPath: z.string().describe("Path to the prefab asset to instantiate"),
                instanceName: z.string().optional().describe("Name for the instance (default: prefab name)"),
                parentPath: z.string().optional().describe("Path to parent GameObject (optional)")
            },
            annotations: {
                title: "Instantiate Prefab",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: false,
                openWorldHint: false
            }
        });

        return tools;
    }

    /**
     * Executes the command with the given parameters.
     * @param action The action to execute.
     * @param parameters The parameters for the command.
     * @returns A Promise that resolves to a JSON object containing the execution result.
     */
    protected async executeCommand(action: string, parameters: JObject): Promise<JObject> {
        // All inspector operations are forwarded to Unity
        try {
            // Validate action
            const validActions = [
                "addcomponent", "removecomponent", "modifycomponent", "enablecomponent",
                "copycomponent", "pastecomponent", "resetcomponent",
                "getcomponents", "getcomponentproperties", "findreferences",
                "applytoprefab", "revertfromprefab", "isprefab", "createprefab", "instantiateprefab"
            ];

            if (!validActions.includes(action.toLowerCase())) {
                return {
                    success: false,
                    error: `Unknown action: ${action}. Valid actions: ${validActions.join(", ")}`
                };
            }

            // Forward the request to Unity
            return await this.sendUnityRequest(
                `${this.commandPrefix}.${action}`,
                parameters
            );
        } catch (ex) {
            const errorMessage = ex instanceof Error ? ex.message : String(ex);
            console.error(`Error executing inspector.${action}: ${errorMessage}`);

            return {
                success: false,
                error: errorMessage
            };
        }
    }
}