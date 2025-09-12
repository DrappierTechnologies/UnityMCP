import { BaseResourceHandler } from "../core/BaseResourceHandler.js";
import { JObject } from "../types/index.js";
import { URL } from "url";

/**
 * Resource handler that provides Unity MCP capability overview for AI agents.
 * This helps AI understand available tools, workflows, and Unity domain knowledge.
 */
export class UnityCapabilityResourceHandler extends BaseResourceHandler {
    
    /**
     * Gets the resource name.
     */
    public get resourceName(): string {
        return "unity_capabilities";
    }

    /**
     * Gets the resource URI template.
     */
    public get resourceUriTemplate(): string {
        return "unity://capabilities";
    }

    /**
     * Gets the description of this resource.
     */
    public get description(): string {
        return "Comprehensive overview of Unity MCP capabilities, tool categories, workflows, and domain knowledge for AI agents";
    }

    /**
     * Fetches the Unity capability overview resource.
     * @param uri The resource URI.
     * @param parameters Additional parameters extracted from the URI.
     * @returns Promise resolving to the capability overview.
     */
    protected async fetchResourceData(uri: URL, parameters?: JObject): Promise<JObject> {
        try {
            const capabilities = {
                meta: {
                    version: "1.0",
                    description: "Unity MCP Capability Overview for AI Agents",
                    lastUpdated: new Date().toISOString()
                },
                
                // Tool Categories organized by functionality
                categories: {
                    asset_management: {
                        description: "Tools for managing Unity project assets like scripts, textures, prefabs, and materials",
                        tools: ["project_search", "project_createAsset", "project_duplicate", "project_rename", "project_move", "project_delete"],
                        common_use_cases: [
                            "Finding existing assets before creating new ones",
                            "Creating scripts and materials from scratch", 
                            "Organizing project structure",
                            "Creating asset variants through duplication"
                        ],
                        best_practices: [
                            "Always search for existing assets before creating new ones",
                            "Use descriptive names for easy discovery",
                            "Organize assets in logical folder structures"
                        ]
                    },
                    
                    scene_management: {
                        description: "Tools for managing GameObjects and scene hierarchy structure",
                        tools: ["hierarchy_get", "hierarchy_create", "hierarchy_delete", "hierarchy_rename", "hierarchy_setParent", "hierarchy_find"],
                        common_use_cases: [
                            "Understanding existing scene structure",
                            "Creating new GameObjects for characters, props, UI",
                            "Organizing scene hierarchy with parent-child relationships",
                            "Finding specific GameObjects by name or component"
                        ],
                        best_practices: [
                            "Get hierarchy overview before making changes",
                            "Use descriptive GameObject names",
                            "Organize related objects under parent containers"
                        ]
                    },
                    
                    component_management: {
                        description: "Tools for adding, configuring, and managing components that define GameObject behavior",
                        tools: ["inspector_addComponent", "inspector_modifyComponent", "inspector_removeComponent", "inspector_getComponents", "inspector_findReferences"],
                        common_use_cases: [
                            "Adding functionality to GameObjects (physics, rendering, scripts)",
                            "Configuring component properties for desired behavior",
                            "Finding all objects using specific components",
                            "Managing prefab overrides and variants"
                        ],
                        best_practices: [
                            "Add components incrementally and test functionality",
                            "Use appropriate component types for your needs (2D vs 3D)",
                            "Keep component configurations simple and readable"
                        ]
                    },
                    
                    animation_system: {
                        description: "Tools for creating and managing animations, controllers, and animation workflows",
                        tools: ["animator_createController", "animator_addState", "animator_addTransition", "animator_createClip", "animator_play"],
                        common_use_cases: [
                            "Setting up character animation systems",
                            "Creating state machines for complex animations", 
                            "Building 2D sprite animations",
                            "Testing and debugging animation flows"
                        ],
                        best_practices: [
                            "Start with simple state machines and add complexity gradually",
                            "Use meaningful state and parameter names",
                            "Test transitions thoroughly before adding conditions"
                        ]
                    },
                    
                    debugging_tools: {
                        description: "Tools for monitoring Unity editor state and troubleshooting issues",
                        tools: ["console_getLogs", "console_clear", "console_setFilter"],
                        common_use_cases: [
                            "Checking for compilation errors",
                            "Monitoring runtime warnings and errors",
                            "Debugging script execution issues"
                        ],
                        best_practices: [
                            "Check console logs after making significant changes",
                            "Clear console before testing to see new messages",
                            "Use filters to focus on specific types of messages"
                        ]
                    }
                },
                
                // Common Workflows and Tool Sequences
                workflows: {
                    create_animated_character: {
                        description: "Complete workflow for setting up a 2D animated character",
                        steps: [
                            {
                                tool: "project_search",
                                purpose: "Find existing character sprites or animations",
                                example: "Search for 'character' or 'sprite' assets"
                            },
                            {
                                tool: "hierarchy_create", 
                                purpose: "Create GameObject for the character",
                                example: "Create 'Player' GameObject"
                            },
                            {
                                tool: "inspector_addComponent",
                                purpose: "Add SpriteRenderer for visual display",
                                example: "Add SpriteRenderer component"
                            },
                            {
                                tool: "inspector_modifyComponent",
                                purpose: "Assign sprite to renderer",
                                example: "Set sprite property to character texture"
                            },
                            {
                                tool: "animator_createController",
                                purpose: "Create animation controller",
                                example: "Create 'PlayerController' in Assets/Animations/"
                            },
                            {
                                tool: "inspector_addComponent",
                                purpose: "Add Animator component",
                                example: "Add Animator component"
                            },
                            {
                                tool: "inspector_modifyComponent",
                                purpose: "Assign controller to animator",
                                example: "Set controller property to PlayerController"
                            }
                        ]
                    },
                    
                    setup_physics_object: {
                        description: "Setup a GameObject with physics simulation",
                        steps: [
                            {
                                tool: "hierarchy_create",
                                purpose: "Create GameObject",
                                example: "Create 'FallingBox' GameObject"
                            },
                            {
                                tool: "inspector_addComponent",
                                purpose: "Add visual component",
                                example: "Add SpriteRenderer or MeshRenderer"
                            },
                            {
                                tool: "inspector_addComponent",
                                purpose: "Add physics body",
                                example: "Add Rigidbody2D or Rigidbody component"
                            },
                            {
                                tool: "inspector_addComponent",
                                purpose: "Add collision detection",
                                example: "Add BoxCollider2D or BoxCollider"
                            },
                            {
                                tool: "inspector_modifyComponent",
                                purpose: "Configure physics properties",
                                example: "Set mass, drag, collision detection mode"
                            }
                        ]
                    },
                    
                    organize_project_assets: {
                        description: "Clean up and organize project structure",
                        steps: [
                            {
                                tool: "project_browse",
                                purpose: "Survey current project structure",
                                example: "Browse Assets folder to understand layout"
                            },
                            {
                                tool: "project_createFolder",
                                purpose: "Create organizational folders",
                                example: "Create Scripts, Textures, Prefabs folders"
                            },
                            {
                                tool: "project_search",
                                purpose: "Find misplaced assets",
                                example: "Search for scripts in wrong locations"
                            },
                            {
                                tool: "project_move",
                                purpose: "Move assets to proper folders",
                                example: "Move .cs files to Scripts folder"
                            }
                        ]
                    }
                },
                
                // Unity Domain Knowledge for AI Agents
                unity_concepts: {
                    gameobjects: {
                        description: "GameObjects are the fundamental objects in Unity scenes. Everything in a scene is a GameObject.",
                        key_points: [
                            "GameObjects by themselves do nothing - they need Components",
                            "GameObjects can have parent-child relationships for organization",
                            "Use meaningful names for GameObjects to make them discoverable"
                        ]
                    },
                    
                    components: {
                        description: "Components add functionality to GameObjects. They define what GameObjects can do.",
                        common_types: {
                            "Transform": "Every GameObject has one - defines position, rotation, scale",
                            "SpriteRenderer": "Displays 2D images/sprites", 
                            "Rigidbody2D": "Adds 2D physics simulation",
                            "BoxCollider2D": "2D collision detection box",
                            "Animator": "Controls animations and state machines",
                            "AudioSource": "Plays sound effects and music"
                        },
                        key_points: [
                            "Add components incrementally - start simple",
                            "2D games typically use 2D versions (Rigidbody2D, BoxCollider2D)",
                            "Some components depend on others (Animator needs Controller)"
                        ]
                    },
                    
                    assets: {
                        description: "Assets are files that Unity can use - scripts, textures, audio, etc.",
                        organization: {
                            "Scripts": "C# code files that define custom behaviors",
                            "Textures": "Images and sprites for visual content",
                            "Materials": "Define how surfaces look and behave",
                            "Prefabs": "Reusable GameObject templates",
                            "Animations": "Animation clips and controllers"
                        },
                        best_practices: [
                            "Keep assets organized in logical folders",
                            "Use consistent naming conventions",
                            "Prefer duplication over recreation for similar assets"
                        ]
                    },
                    
                    animation_system: {
                        description: "Unity's animation system uses Controllers, States, and Transitions",
                        workflow: [
                            "Create Animation Controller asset",
                            "Add States for each animation (idle, walk, jump)",
                            "Add Transitions between states with conditions",
                            "Assign Controller to GameObject's Animator component",
                            "Use parameters to trigger transitions from scripts"
                        ],
                        key_concepts: {
                            "State": "Represents a single animation clip",
                            "Transition": "How to move from one state to another",
                            "Parameter": "Variables used to control transitions",
                            "Layer": "Separate animation layers for different body parts"
                        }
                    }
                },
                
                // Tool Selection Guidelines
                tool_selection: {
                    when_to_use_search: [
                        "Before creating any new asset",
                        "To understand existing project structure",
                        "To find assets for reference or duplication"
                    ],
                    
                    when_to_use_create_vs_duplicate: {
                        create: [
                            "When you need completely new functionality",
                            "For base/template assets",
                            "When no similar assets exist"
                        ],
                        duplicate: [
                            "When you need variations of existing assets",
                            "To preserve existing configurations", 
                            "For creating asset families (multiple enemy types)"
                        ]
                    },
                    
                    hierarchy_vs_inspector: {
                        hierarchy: "Use for scene structure, GameObject relationships, finding objects",
                        inspector: "Use for component management, property configuration, prefab operations"
                    }
                }
            };

            return capabilities;
            
        } catch (error) {
            const errorMessage = error instanceof Error ? error.message : String(error);
            console.error(`Error fetching Unity capabilities: ${errorMessage}`);
            
            throw new Error(`Failed to fetch Unity capabilities: ${errorMessage}`);
        }
    }
}