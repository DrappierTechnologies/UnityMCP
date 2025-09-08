import { IMcpToolDefinition } from "../core/interfaces/ICommandHandler.js";
import { JObject } from "../types/index.js";
import { z } from "zod";
import { BaseCommandHandler } from "../core/BaseCommandHandler.js";

/**
 * Command handler for manipulating Unity scene hierarchy.
 */
export class HierarchyCommandHandler extends BaseCommandHandler {
    /**
     * Gets the command prefix for this handler.
     */
    public get commandPrefix(): string {
        return "hierarchy";
    }

    /**
     * Gets the description of this command handler.
     */
    public get description(): string {
        return "Manipulate Unity scene hierarchy (create, read, update, delete GameObjects)";
    }

    /**
     * Gets the tool definitions supported by this handler.
     * @returns A map of tool names to their definitions.
     */
    public getToolDefinitions(): Map<string, IMcpToolDefinition> {
        const tools = new Map<string, IMcpToolDefinition>();

        // hierarchy.get - Read hierarchy structure
        tools.set("hierarchy_get", {
            description: "Get the Unity scene hierarchy structure",
            parameterSchema: {
                rootPath: z.string().optional().describe("Root GameObject path to start from (optional)"),
                depth: z.number().optional().describe("Maximum depth to traverse (-1 for unlimited)"),
                includeInactive: z.boolean().optional().describe("Include inactive GameObjects (default: true)")
            },
            annotations: {
                title: "Get Hierarchy",
                readOnlyHint: true,
                openWorldHint: false
            }
        });

        // hierarchy.create - Create new GameObjects
        tools.set("hierarchy_create", {
            description: "Create a new GameObject in the Unity scene",
            parameterSchema: {
                name: z.string().describe("Name for the new GameObject"),
                parentPath: z.string().optional().describe("Path to parent GameObject (optional)"),
                components: z.array(z.string()).optional().describe("Array of component type names to add (optional)")
            },
            annotations: {
                title: "Create GameObject",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: false,
                openWorldHint: false
            }
        });

        // hierarchy.delete - Delete GameObjects
        tools.set("hierarchy_delete", {
            description: "Delete GameObjects from the Unity scene",
            parameterSchema: {
                path: z.string().optional().describe("Path to GameObject to delete"),
                paths: z.array(z.string()).optional().describe("Array of paths to delete multiple GameObjects")
            },
            annotations: {
                title: "Delete GameObject",
                readOnlyHint: false,
                destructiveHint: true,
                idempotentHint: false,
                openWorldHint: false
            }
        });

        // hierarchy.rename - Rename GameObjects
        tools.set("hierarchy_rename", {
            description: "Rename a GameObject in the Unity scene",
            parameterSchema: {
                path: z.string().describe("Path to GameObject to rename"),
                newName: z.string().describe("New name for the GameObject")
            },
            annotations: {
                title: "Rename GameObject",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        // hierarchy.setParent - Change parent-child relationships
        tools.set("hierarchy_setParent", {
            description: "Change the parent of a GameObject in the hierarchy",
            parameterSchema: {
                childPath: z.string().describe("Path to child GameObject"),
                parentPath: z.string().optional().describe("Path to new parent GameObject (null for root)"),
                worldPositionStays: z.boolean().optional().describe("Keep world position when re-parenting (default: true)")
            },
            annotations: {
                title: "Set Parent",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        // hierarchy.getChildren - List child GameObjects
        tools.set("hierarchy_getChildren", {
            description: "Get direct children of a GameObject",
            parameterSchema: {
                parentPath: z.string().optional().describe("Path to parent GameObject (empty for root objects)"),
                includeInactive: z.boolean().optional().describe("Include inactive children (default: true)")
            },
            annotations: {
                title: "Get Children",
                readOnlyHint: true,
                openWorldHint: false
            }
        });

        // hierarchy.find - Search for GameObjects
        tools.set("hierarchy_find", {
            description: "Search for GameObjects by name, tag, or component",
            parameterSchema: {
                query: z.string().describe("Search query string"),
                searchType: z.enum(["name", "tag", "component"]).optional().describe("Type of search (default: name)"),
                limit: z.number().optional().describe("Maximum number of results (default: 100)")
            },
            annotations: {
                title: "Find GameObjects",
                readOnlyHint: true,
                openWorldHint: false
            }
        });

        // hierarchy.setActive - Toggle GameObject active state
        tools.set("hierarchy_setActive", {
            description: "Set the active state of a GameObject",
            parameterSchema: {
                path: z.string().describe("Path to GameObject"),
                active: z.boolean().describe("New active state")
            },
            annotations: {
                title: "Set Active State",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });


        // hierarchy.duplicate - Clone GameObjects
        tools.set("hierarchy_duplicate", {
            description: "Create a duplicate of a GameObject",
            parameterSchema: {
                path: z.string().describe("Path to GameObject to duplicate"),
                newName: z.string().optional().describe("Name for the duplicate (optional)"),
                parentPath: z.string().optional().describe("Path to parent for the duplicate (optional)")
            },
            annotations: {
                title: "Duplicate GameObject",
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
        // All hierarchy operations are forwarded to Unity
        try {
            // Validate action
            const validActions = [
                "get", "create", "delete", "rename", "setparent",
                "getchildren", "find", "setactive", "duplicate"
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
            console.error(`Error executing hierarchy.${action}: ${errorMessage}`);

            return {
                success: false,
                error: errorMessage
            };
        }
    }
}