import { IMcpToolDefinition } from "../core/interfaces/ICommandHandler.js";
import { JObject } from "../types/index.js";
import { z } from "zod";
import { BaseCommandHandler } from "../core/BaseCommandHandler.js";

/**
 * Command handler for Unity Project window operations (CRUD for assets and project structure).
 */
export class ProjectCommandHandler extends BaseCommandHandler {
    /**
     * Gets the command prefix for this handler.
     */
    public get commandPrefix(): string {
        return "project";
    }

    /**
     * Gets the description of this command handler.
     */
    public get description(): string {
        return "Manage Unity project assets and folder structure (create, read, update, delete)";
    }

    /**
     * Gets the tool definitions supported by this handler.
     * @returns A map of tool names to their definitions.
     */
    public getToolDefinitions(): Map<string, IMcpToolDefinition> {
        const tools = new Map<string, IMcpToolDefinition>();

        // READ OPERATIONS
        
        tools.set("project_search", {
            description: "Search and find assets in Unity project by name, type, or content. Use this to locate existing sprites, scripts, prefabs, materials, and other assets before creating new ones or to understand project structure.",
            parameterSchema: {
                query: z.string().describe("Search term like 'Player', 'texture', or file extension '.cs'. Use descriptive names to find assets."),
                searchType: z.enum(["name", "type", "content", "all"]).optional().describe("Search scope: 'name' for filenames, 'type' for Unity asset types, 'content' for file contents, 'all' for comprehensive search (default: 'all')"),
                assetType: z.string().optional().describe("Unity asset type filter like 'Texture2D' (images), 'Material' (shaders), 'MonoScript' (C# scripts), 'GameObject' (prefabs)"),
                folder: z.string().optional().describe("Folder path to limit search scope (e.g., 'Assets/Characters', 'Assets/Textures/UI')"),
                exact: z.boolean().optional().describe("Whether to perform exact match for name searches (default: false)"),
                limit: z.number().optional().describe("Maximum number of results to return (default: 100)"),
                includePackages: z.boolean().optional().describe("Include package assets in search (default: false)")
            },
            annotations: {
                title: "Search Project Assets",
                readOnlyHint: true,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });


        tools.set("project_getInfo", {
            description: "Get detailed information about a specific asset",
            parameterSchema: {
                path: z.string().optional().describe("Asset path (either path or guid is required)"),
                guid: z.string().optional().describe("Asset GUID (either path or guid is required)")
            },
            annotations: {
                title: "Get Asset Info",
                readOnlyHint: true,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        tools.set("project_getDependencies", {
            description: "Get dependencies of an asset",
            parameterSchema: {
                path: z.string().describe("Asset path to get dependencies for"),
                recursive: z.boolean().optional().describe("Include recursive dependencies (default: false)")
            },
            annotations: {
                title: "Get Asset Dependencies",
                readOnlyHint: true,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        tools.set("project_browse", {
            description: "Browse the Unity project folder structure",
            parameterSchema: {
                rootPath: z.string().optional().describe("Root folder path to start browsing from (default: 'Assets')"),
                depth: z.number().optional().describe("Maximum depth to traverse (default: 1, -1 for unlimited)"),
                includeFiles: z.boolean().optional().describe("Include files in the results (default: true)"),
                fileTypes: z.array(z.string()).optional().describe("Filter files by extensions (e.g., ['.cs', '.prefab'])")
            },
            annotations: {
                title: "Browse Project Structure",
                readOnlyHint: true,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        // CREATE OPERATIONS

        tools.set("project_createFolder", {
            description: "Create a new folder in the Unity project",
            parameterSchema: {
                parentPath: z.string().optional().describe("Parent folder path (default: 'Assets')"),
                folderName: z.string().describe("Name of the new folder")
            },
            annotations: {
                title: "Create Folder",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: false,
                openWorldHint: false
            }
        });

        tools.set("project_createAsset", {
            description: "Create new assets like scripts, materials, or animator controllers in Unity project. Use this to generate new content rather than duplicating existing assets.",
            parameterSchema: {
                assetType: z.enum(["script", "csharp", "material", "animatorcontroller", "scriptableobject"]).describe("Type of asset to create"),
                assetName: z.string().describe("Name of the new asset"),
                parentPath: z.string().optional().describe("Parent folder path (default: 'Assets')"),
                content: z.string().optional().describe("Content for script assets"),
                scriptableObjectType: z.string().optional().describe("Type name for ScriptableObject assets")
            },
            annotations: {
                title: "Create Asset",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: false,
                openWorldHint: false
            }
        });

        tools.set("project_duplicate", {
            description: "Duplicate an existing asset to create variants. More efficient than creating from scratch when you need similar assets with small modifications.",
            parameterSchema: {
                sourcePath: z.string().describe("Path to the asset to duplicate"),
                newName: z.string().optional().describe("Name for the duplicate (optional)")
            },
            annotations: {
                title: "Duplicate Asset",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: false,
                openWorldHint: false
            }
        });

        tools.set("project_importAsset", {
            description: "Import an external file as an asset into the Unity project",
            parameterSchema: {
                sourcePath: z.string().describe("Path to the external file to import"),
                targetPath: z.string().optional().describe("Target path in the project (optional)")
            },
            annotations: {
                title: "Import Asset",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: false,
                openWorldHint: false
            }
        });

        // UPDATE OPERATIONS

        tools.set("project_rename", {
            description: "Rename an asset or folder",
            parameterSchema: {
                assetPath: z.string().describe("Path to the asset to rename"),
                newName: z.string().describe("New name for the asset")
            },
            annotations: {
                title: "Rename Asset",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        tools.set("project_move", {
            description: "Move an asset or folder to a new location",
            parameterSchema: {
                sourcePath: z.string().describe("Current path of the asset"),
                targetPath: z.string().describe("New path for the asset")
            },
            annotations: {
                title: "Move Asset",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        tools.set("project_setLabels", {
            description: "Set labels on an asset for organization",
            parameterSchema: {
                assetPath: z.string().describe("Path to the asset"),
                labels: z.array(z.string()).describe("Array of labels to set on the asset")
            },
            annotations: {
                title: "Set Asset Labels",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        tools.set("project_refresh", {
            description: "Refresh the Unity asset database to detect external changes",
            parameterSchema: {},
            annotations: {
                title: "Refresh Assets",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        // DELETE OPERATIONS

        tools.set("project_delete", {
            description: "Delete an asset or folder from the Unity project",
            parameterSchema: {
                assetPath: z.string().describe("Path to the asset to delete"),
                checkDependencies: z.boolean().optional().describe("Check for dependencies before deletion (default: true)")
            },
            annotations: {
                title: "Delete Asset",
                readOnlyHint: false,
                destructiveHint: true,
                idempotentHint: false,
                openWorldHint: false
            }
        });

        tools.set("project_deleteEmptyFolders", {
            description: "Delete empty folders in the project to clean up structure",
            parameterSchema: {
                rootPath: z.string().optional().describe("Root path to start cleanup from (default: 'Assets')")
            },
            annotations: {
                title: "Delete Empty Folders",
                readOnlyHint: false,
                destructiveHint: true,
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
        try {
            // Validate action
            const validActions = [
                // Read operations
                "search", "getinfo", "getdependencies", "browse",
                // Create operations  
                "createfolder", "createasset", "duplicate", "importasset",
                // Update operations
                "rename", "move", "setlabels", "refresh",
                // Delete operations
                "delete", "deleteemptyfolders"
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
            console.error(`Error executing project.${action}: ${errorMessage}`);

            return {
                success: false,
                error: errorMessage
            };
        }
    }
}