import { IMcpToolDefinition } from "../core/interfaces/ICommandHandler.js";
import { JObject } from "../types/index.js";
import { z } from "zod";
import { BaseCommandHandler } from "../core/BaseCommandHandler.js";

/**
 * Command handler for Animator operations - animation creation, controller management, and runtime control.
 */
export class AnimatorCommandHandler extends BaseCommandHandler {
    /**
     * Gets the command prefix for this handler.
     */
    public get commandPrefix(): string {
        return "animator";
    }

    /**
     * Gets the description of this command handler.
     */
    public get description(): string {
        return "Create and manage animations, animator controllers, and control animation playback";
    }

    /**
     * Gets the tool definitions supported by this handler.
     * @returns A map of tool names to their definitions.
     */
    public getToolDefinitions(): Map<string, IMcpToolDefinition> {
        const tools = new Map<string, IMcpToolDefinition>();

        // Controller operations
        
        // animator.createController - Create a new animator controller
        tools.set("animator_createController", {
            description: "Create animator controllers to manage character animations and state transitions. Controllers define how animations blend and switch based on parameters and conditions.",
            parameterSchema: {
                path: z.string().describe("Folder path where controller will be created (e.g., 'Assets/Animations/Characters/', 'Assets/Controllers/')"),
                name: z.string().optional().describe("Controller filename without extension (e.g., 'PlayerController', 'EnemyAnimator'). Default: 'NewAnimatorController'"),
                defaultParameters: z.array(z.object({
                    name: z.string().describe("Parameter name"),
                    type: z.enum(["float", "int", "bool", "trigger"]).describe("Parameter type")
                })).optional().describe("Default parameters to add to the controller")
            },
            annotations: {
                title: "Create Animator Controller",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: false,
                openWorldHint: false
            }
        });

        // animator.addState - Add a state to controller
        tools.set("animator_addState", {
            description: "Add animation states to controller layers. States represent individual animations (idle, walk, jump) that characters can be in. Essential for building animation state machines.",
            parameterSchema: {
                controllerPath: z.string().describe("Path to the animator controller asset"),
                stateName: z.string().describe("Name of the state to add"),
                layerIndex: z.number().optional().describe("Layer index to add state to (default: 0)"),
                isDefault: z.boolean().optional().describe("Set as default state (default: false)"),
                clipPath: z.string().optional().describe("Path to animation clip to assign"),
                positionX: z.number().optional().describe("X position in state machine view (default: 250)"),
                positionY: z.number().optional().describe("Y position in state machine view (default: 100)")
            },
            annotations: {
                title: "Add State",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: false,
                openWorldHint: false
            }
        });

        // animator.addTransition - Add transition between states
        tools.set("animator_addTransition", {
            description: "Add a transition between animator states",
            parameterSchema: {
                controllerPath: z.string().describe("Path to the animator controller asset"),
                sourceState: z.string().optional().describe("Source state name (empty/null for Entry, 'Any' for any state)"),
                destinationState: z.string().describe("Destination state name"),
                layerIndex: z.number().optional().describe("Layer index (default: 0)"),
                hasExitTime: z.boolean().optional().describe("Use exit time (default: true)"),
                exitTime: z.number().optional().describe("Exit time value (default: 1.0)"),
                duration: z.number().optional().describe("Transition duration (default: 0.25)"),
                conditions: z.array(z.object({
                    parameter: z.string().describe("Parameter name"),
                    mode: z.enum(["greater", "less", "equals", "notequal", "if", "ifnot"]).describe("Condition mode"),
                    threshold: z.number().optional().describe("Threshold value for numeric conditions")
                })).optional().describe("Transition conditions")
            },
            annotations: {
                title: "Add Transition",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: false,
                openWorldHint: false
            }
        });

        // animator.setParameter - Set controller parameter
        tools.set("animator_setParameter", {
            description: "Add or update a parameter in an animator controller",
            parameterSchema: {
                controllerPath: z.string().describe("Path to the animator controller asset"),
                parameterName: z.string().describe("Name of the parameter"),
                parameterType: z.enum(["float", "int", "bool", "trigger"]).describe("Type of the parameter"),
                defaultValue: z.any().optional().describe("Default value for the parameter")
            },
            annotations: {
                title: "Set Parameter",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        // animator.addLayer - Add layer to controller
        tools.set("animator_addLayer", {
            description: "Add a new layer to an animator controller",
            parameterSchema: {
                controllerPath: z.string().describe("Path to the animator controller asset"),
                layerName: z.string().describe("Name of the layer"),
                weight: z.number().optional().describe("Default weight of the layer (default: 1.0)"),
                blendMode: z.enum(["Override", "Additive"]).optional().describe("Blend mode (default: 'Override')")
            },
            annotations: {
                title: "Add Layer",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: false,
                openWorldHint: false
            }
        });

        // animator.setBlendTree - Configure blend tree
        tools.set("animator_setBlendTree", {
            description: "Set up a blend tree for smooth animation blending",
            parameterSchema: {
                controllerPath: z.string().describe("Path to the animator controller asset"),
                stateName: z.string().describe("Name of the state to convert to blend tree"),
                layerIndex: z.number().optional().describe("Layer index (default: 0)"),
                blendType: z.enum(["1D", "2D", "2DCartesian", "2DDirectional"]).optional().describe("Blend tree type (default: '1D')"),
                blendParameter: z.string().describe("Primary blend parameter"),
                blendParameterY: z.string().optional().describe("Secondary blend parameter for 2D blend trees"),
                motions: z.array(z.object({
                    clipPath: z.string().describe("Path to animation clip"),
                    threshold: z.number().optional().describe("Threshold value for 1D blending"),
                    position: z.object({
                        x: z.number(),
                        y: z.number()
                    }).optional().describe("Position for 2D blending"),
                    timeScale: z.number().optional().describe("Time scale multiplier (default: 1.0)")
                })).optional().describe("Motion clips for the blend tree")
            },
            annotations: {
                title: "Set Blend Tree",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: false,
                openWorldHint: false
            }
        });

        // animator.getControllerInfo - Get controller information
        tools.set("animator_getControllerInfo", {
            description: "Get detailed information about an animator controller",
            parameterSchema: {
                controllerPath: z.string().describe("Path to the animator controller asset")
            },
            annotations: {
                title: "Get Controller Info",
                readOnlyHint: true,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        // Animation clip operations

        // animator.createClip - Create animation clip
        tools.set("animator_createClip", {
            description: "Create a new animation clip asset",
            parameterSchema: {
                path: z.string().describe("Asset path for the clip (e.g., 'Assets/Animations/')"),
                name: z.string().optional().describe("Name of the clip (default: 'NewAnimationClip')"),
                frameRate: z.number().optional().describe("Frame rate (default: 30)"),
                isLooping: z.boolean().optional().describe("Loop the animation (default: false)"),
                curves: z.array(z.object({
                    propertyPath: z.string().optional().describe("GameObject path (empty for root)"),
                    propertyName: z.string().describe("Property to animate (e.g., 'localPosition.x')"),
                    targetType: z.string().optional().describe("Component type (default: 'Transform')"),
                    curve: z.object({
                        keyframes: z.array(z.object({
                            time: z.number().describe("Time in seconds"),
                            value: z.number().describe("Value at this time")
                        }))
                    }).optional()
                })).optional().describe("Initial animation curves")
            },
            annotations: {
                title: "Create Animation Clip",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: false,
                openWorldHint: false
            }
        });

        // animator.addCurve - Add curve to clip
        tools.set("animator_addCurve", {
            description: "Add an animation curve to an existing clip",
            parameterSchema: {
                clipPath: z.string().describe("Path to the animation clip asset"),
                propertyPath: z.string().optional().describe("GameObject path (empty for root)"),
                propertyName: z.string().describe("Property to animate (e.g., 'localPosition.x')"),
                targetType: z.string().optional().describe("Component type (default: 'Transform')"),
                curve: z.object({
                    keyframes: z.array(z.object({
                        time: z.number().describe("Time in seconds"),
                        value: z.number().describe("Value at this time")
                    }))
                }).optional().describe("Curve keyframes")
            },
            annotations: {
                title: "Add Curve",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: false,
                openWorldHint: false
            }
        });

        // animator.setCurveKeys - Set curve keyframes
        tools.set("animator_setCurveKeys", {
            description: "Set keyframes for a specific curve in an animation clip",
            parameterSchema: {
                clipPath: z.string().describe("Path to the animation clip asset"),
                propertyPath: z.string().optional().describe("GameObject path (empty for root)"),
                propertyName: z.string().describe("Property name"),
                targetType: z.string().optional().describe("Component type (default: 'Transform')"),
                keyframes: z.array(z.object({
                    time: z.number().describe("Time in seconds"),
                    value: z.number().describe("Value at this time"),
                    inTangent: z.number().optional().describe("In tangent (default: 0)"),
                    outTangent: z.number().optional().describe("Out tangent (default: 0)"),
                    tangentMode: z.enum(["Auto", "Linear", "Constant"]).optional().describe("Tangent mode (default: 'Auto')")
                })).describe("Keyframe data")
            },
            annotations: {
                title: "Set Curve Keys",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        // animator.getClipInfo - Get clip information
        tools.set("animator_getClipInfo", {
            description: "Get detailed information about an animation clip",
            parameterSchema: {
                clipPath: z.string().describe("Path to the animation clip asset")
            },
            annotations: {
                title: "Get Clip Info",
                readOnlyHint: true,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        // animator.duplicateClip - Duplicate animation clip
        tools.set("animator_duplicateClip", {
            description: "Create a copy of an existing animation clip",
            parameterSchema: {
                sourcePath: z.string().describe("Path to the source animation clip"),
                destinationPath: z.string().describe("Path for the new clip"),
                newName: z.string().optional().describe("Name for the new clip")
            },
            annotations: {
                title: "Duplicate Clip",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: false,
                openWorldHint: false
            }
        });

        // Runtime animator control

        // animator.play - Play animation state
        tools.set("animator_play", {
            description: "Play a specific animation state on a GameObject",
            parameterSchema: {
                gameObjectPath: z.string().describe("Path to the GameObject in the scene"),
                stateName: z.string().describe("Name of the state to play"),
                layer: z.number().optional().describe("Layer index (default: -1 for any)"),
                normalizedTime: z.number().optional().describe("Normalized time to start at")
            },
            annotations: {
                title: "Play Animation",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: false,
                openWorldHint: false
            }
        });

        // animator.setParameterValue - Set runtime parameter value
        tools.set("animator_setParameterValue", {
            description: "Set a parameter value on a runtime animator",
            parameterSchema: {
                gameObjectPath: z.string().describe("Path to the GameObject in the scene"),
                parameterName: z.string().describe("Name of the parameter"),
                parameterType: z.enum(["float", "int", "bool", "trigger"]).describe("Type of the parameter"),
                value: z.any().optional().describe("Value to set (not needed for triggers)")
            },
            annotations: {
                title: "Set Parameter Value",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        // animator.crossFade - Cross-fade to state
        tools.set("animator_crossFade", {
            description: "Smoothly transition to an animation state",
            parameterSchema: {
                gameObjectPath: z.string().describe("Path to the GameObject in the scene"),
                stateName: z.string().describe("Name of the state to transition to"),
                duration: z.number().optional().describe("Transition duration in seconds (default: 0.25)"),
                layer: z.number().optional().describe("Layer index (default: -1 for any)"),
                normalizedTime: z.number().optional().describe("Normalized time to start at")
            },
            annotations: {
                title: "Cross Fade",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: false,
                openWorldHint: false
            }
        });

        // animator.getState - Get current animator state
        tools.set("animator_getState", {
            description: "Get the current state information of an animator",
            parameterSchema: {
                gameObjectPath: z.string().describe("Path to the GameObject in the scene"),
                layer: z.number().optional().describe("Layer index to query (default: 0)")
            },
            annotations: {
                title: "Get State",
                readOnlyHint: true,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        // animator.setSpeed - Set animator speed
        tools.set("animator_setSpeed", {
            description: "Set the playback speed of an animator",
            parameterSchema: {
                gameObjectPath: z.string().describe("Path to the GameObject in the scene"),
                speed: z.number().describe("Playback speed multiplier (1.0 = normal speed)")
            },
            annotations: {
                title: "Set Speed",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        // animator.getParameters - Get all animator parameters
        tools.set("animator_getParameters", {
            description: "Get all parameters and their current values from an animator",
            parameterSchema: {
                gameObjectPath: z.string().describe("Path to the GameObject in the scene")
            },
            annotations: {
                title: "Get Parameters",
                readOnlyHint: true,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        // Component management

        // animator.attachAnimator - Attach animator component
        tools.set("animator_attachAnimator", {
            description: "Add or configure an Animator component on a GameObject",
            parameterSchema: {
                gameObjectPath: z.string().describe("Path to the GameObject in the scene"),
                controllerPath: z.string().optional().describe("Path to animator controller to assign"),
                applyRootMotion: z.boolean().optional().describe("Apply root motion (default: false)"),
                updateMode: z.enum(["Normal", "AnimatePhysics", "UnscaledTime"]).optional().describe("Update mode (default: 'Normal')"),
                cullingMode: z.enum(["AlwaysAnimate", "CullUpdateTransforms", "CullCompletely"]).optional().describe("Culling mode (default: 'AlwaysAnimate')")
            },
            annotations: {
                title: "Attach Animator",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: false,
                openWorldHint: false
            }
        });

        // animator.assignController - Assign controller to animator
        tools.set("animator_assignController", {
            description: "Assign an animator controller to an existing Animator component",
            parameterSchema: {
                gameObjectPath: z.string().describe("Path to the GameObject in the scene"),
                controllerPath: z.string().describe("Path to the animator controller asset")
            },
            annotations: {
                title: "Assign Controller",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        // animator.configureAvatar - Configure avatar
        tools.set("animator_configureAvatar", {
            description: "Configure the avatar for an Animator component",
            parameterSchema: {
                gameObjectPath: z.string().describe("Path to the GameObject in the scene"),
                avatarPath: z.string().optional().describe("Path to the avatar asset"),
                avatarType: z.enum(["Generic", "Humanoid"]).optional().describe("Avatar type (default: 'Generic')")
            },
            annotations: {
                title: "Configure Avatar",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        // animator.applyRootMotion - Configure root motion
        tools.set("animator_applyRootMotion", {
            description: "Enable or disable root motion on an Animator",
            parameterSchema: {
                gameObjectPath: z.string().describe("Path to the GameObject in the scene"),
                enabled: z.boolean().describe("Enable root motion")
            },
            annotations: {
                title: "Apply Root Motion",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        // animator.setSpriteAnimation - Create sprite animation from texture sequence
        tools.set("animator_setSpriteAnimation", {
            description: "Create sprite animation from texture sequence using ObjectReferenceKeyframe",
            parameterSchema: {
                clipPath: z.string().describe("Path to animation clip"),
                spritePaths: z.array(z.string()).describe("Array of sprite asset paths in sequence"),
                frameRate: z.number().optional().describe("Animation frame rate (default: 24)"),
                targetType: z.string().optional().describe("Target component type (default: 'SpriteRenderer')"),
                propertyPath: z.string().optional().describe("GameObject path (default: '')"),
                isLooping: z.boolean().optional().describe("Should animation loop (default: true)")
            },
            annotations: {
                title: "Set Sprite Animation",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: false,
                openWorldHint: false
            }
        });

        // animator.getSpriteAnimation - Get sprite animation information
        tools.set("animator_getSpriteAnimation", {
            description: "Get sprite animation curve information from an animation clip",
            parameterSchema: {
                clipPath: z.string().describe("Path to animation clip"),
                targetType: z.string().optional().describe("Target component type (default: 'SpriteRenderer')"),
                propertyPath: z.string().optional().describe("GameObject path (default: '')")
            },
            annotations: {
                title: "Get Sprite Animation",
                readOnlyHint: true,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        // State modification operations

        // animator.modifyState - Modify existing state properties
        tools.set("animator_modifyState", {
            description: "Modify properties of an existing animator state including speed, mirror, cycle offset, write default values, tags, and parameter bindings",
            parameterSchema: {
                controllerPath: z.string().describe("Path to the animator controller asset"),
                stateName: z.string().describe("Name of the state to modify"),
                layerIndex: z.number().optional().describe("Layer index (default: 0)"),
                speed: z.number().optional().describe("State speed multiplier"),
                mirror: z.boolean().optional().describe("Mirror the animation"),
                cycleOffset: z.number().optional().describe("Animation cycle offset (0-1)"),
                writeDefaultValues: z.boolean().optional().describe("Write default values for non-animated properties"),
                tag: z.string().optional().describe("State identification tag"),
                iKOnFeet: z.boolean().optional().describe("Enable foot IK"),
                motionPath: z.string().optional().describe("Path to motion (animation clip or blend tree) to assign"),
                positionX: z.number().optional().describe("X position in state machine view"),
                positionY: z.number().optional().describe("Y position in state machine view"),
                speedParameter: z.string().optional().describe("Parameter name to control speed"),
                speedParameterActive: z.boolean().optional().describe("Enable speed parameter control (default: true)"),
                mirrorParameter: z.string().optional().describe("Parameter name to control mirror"),
                mirrorParameterActive: z.boolean().optional().describe("Enable mirror parameter control (default: true)"),
                cycleOffsetParameter: z.string().optional().describe("Parameter name to control cycle offset"),
                cycleOffsetParameterActive: z.boolean().optional().describe("Enable cycle offset parameter control (default: true)"),
                timeParameter: z.string().optional().describe("Parameter name to control normalized time"),
                timeParameterActive: z.boolean().optional().describe("Enable time parameter control (default: true)")
            },
            annotations: {
                title: "Modify State",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        // animator.getStateProperties - Get comprehensive state information
        tools.set("animator_getStateProperties", {
            description: "Get detailed properties of an animator state including all settings, parameter bindings, motion info, behaviors, and transitions",
            parameterSchema: {
                controllerPath: z.string().describe("Path to the animator controller asset"),
                stateName: z.string().describe("Name of the state to inspect"),
                layerIndex: z.number().optional().describe("Layer index (default: 0)")
            },
            annotations: {
                title: "Get State Properties",
                readOnlyHint: true,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        // animator.configureStateParameters - Advanced parameter binding configuration
        tools.set("animator_configureStateParameters", {
            description: "Configure parameter bindings for state properties (speed, mirror, cycle offset, time) to enable runtime control",
            parameterSchema: {
                controllerPath: z.string().describe("Path to the animator controller asset"),
                stateName: z.string().describe("Name of the state to configure"),
                layerIndex: z.number().optional().describe("Layer index (default: 0)"),
                bindings: z.array(z.object({
                    propertyType: z.enum(["speed", "mirror", "cycleOffset", "time"]).describe("Property to bind"),
                    parameterName: z.string().describe("Controller parameter name"),
                    active: z.boolean().optional().describe("Enable this binding (default: true)")
                })).describe("Parameter binding configurations")
            },
            annotations: {
                title: "Configure State Parameters",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        // animator.configureStateBehaviors - StateMachineBehaviour management
        tools.set("animator_configureStateBehaviors", {
            description: "Add, remove, or list StateMachineBehaviours on animator states for custom behavior scripting",
            parameterSchema: {
                controllerPath: z.string().describe("Path to the animator controller asset"),
                stateName: z.string().describe("Name of the state to configure"),
                layerIndex: z.number().optional().describe("Layer index (default: 0)"),
                action: z.enum(["add", "remove", "list"]).optional().describe("Action to perform (default: 'list')"),
                behaviorType: z.string().optional().describe("StateMachineBehaviour type name for add action"),
                index: z.number().optional().describe("Behavior index for remove action")
            },
            annotations: {
                title: "Configure State Behaviors",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: false,
                openWorldHint: false
            }
        });

        // animator.batchModifyStates - Batch state modifications
        tools.set("animator_batchModifyStates", {
            description: "Apply property changes to multiple states atomically for efficient bulk modifications",
            parameterSchema: {
                controllerPath: z.string().describe("Path to the animator controller asset"),
                stateSelectors: z.array(z.object({
                    stateName: z.string().describe("Name of the state"),
                    layerIndex: z.number().optional().describe("Layer index (default: 0)")
                })).describe("States to modify"),
                properties: z.object({
                    speed: z.number().optional().describe("State speed multiplier"),
                    mirror: z.boolean().optional().describe("Mirror the animation"),
                    cycleOffset: z.number().optional().describe("Animation cycle offset (0-1)"),
                    writeDefaultValues: z.boolean().optional().describe("Write default values for non-animated properties"),
                    tag: z.string().optional().describe("State identification tag"),
                    iKOnFeet: z.boolean().optional().describe("Enable foot IK")
                }).describe("Properties to apply to all selected states")
            },
            annotations: {
                title: "Batch Modify States",
                readOnlyHint: false,
                destructiveHint: false,
                idempotentHint: false,
                openWorldHint: false
            }
        });

        // animator.validateStateConfiguration - State validation and diagnostics
        tools.set("animator_validateStateConfiguration", {
            description: "Validate state machine configuration and identify issues like dead ends, missing parameters, and best practice violations",
            parameterSchema: {
                controllerPath: z.string().describe("Path to the animator controller asset"),
                layerIndex: z.number().optional().describe("Layer index to validate (default: -1 for all layers)")
            },
            annotations: {
                title: "Validate State Configuration",
                readOnlyHint: true,
                destructiveHint: false,
                idempotentHint: true,
                openWorldHint: false
            }
        });

        return tools;
    }

    /**
     * Handles execution of commands.
     * @param action The action to execute.
     * @param parameters The parameters for the action.
     * @returns A promise with the execution result.
     */
    protected async executeCommand(action: string, parameters: JObject): Promise<JObject> {
        // Forward all commands to Unity with proper prefix.action format
        const fullCommand = `${this.commandPrefix}.${action}`;
        console.error(`[DEBUG] AnimatorCommandHandler sending command: ${fullCommand}`);
        return await this.sendUnityRequest(fullCommand, parameters);
    }
}