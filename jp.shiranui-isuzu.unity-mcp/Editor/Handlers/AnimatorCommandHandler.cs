using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityMCP.Editor.Core;

namespace UnityMCP.Editor.Handlers
{
    /// <summary>
    /// Command handler for Animator operations - animation creation, controller management, and runtime control.
    /// </summary>
    internal sealed class AnimatorCommandHandler : IMcpCommandHandler
    {
        /// <summary>
        /// Gets the command prefix for this handler.
        /// </summary>
        public string CommandPrefix => "animator";

        /// <summary>
        /// Gets the description of this command handler.
        /// </summary>
        public string Description => "Create and manage animations, animator controllers, and control animation playback";

        /// <summary>
        /// Executes the command with the given parameters.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <param name="parameters">The parameters for the command.</param>
        /// <returns>A JSON object containing the execution result.</returns>
        public JObject Execute(string action, JObject parameters)
        {
            return action.ToLower() switch
            {
                // Controller operations
                "createcontroller" => CreateController(parameters),
                "addstate" => AddState(parameters),
                "addtransition" => AddTransition(parameters),
                "setparameter" => SetParameter(parameters),
                "addlayer" => AddLayer(parameters),
                "setblendtree" => SetBlendTree(parameters),
                "getcontrollerinfo" => GetControllerInfo(parameters),
                
                // Animation clip operations
                "createclip" => CreateClip(parameters),
                "addcurve" => AddCurve(parameters),
                "setcurvekeys" => SetCurveKeys(parameters),
                "setspriteanimation" => SetSpriteAnimationCurve(parameters),
                "getspriteanimation" => GetSpriteAnimationCurve(parameters),
                "getclipinfo" => GetClipInfo(parameters),
                "duplicateclip" => DuplicateClip(parameters),
                
                // Runtime animator control
                "play" => Play(parameters),
                "setparametervalue" => SetParameterValue(parameters),
                "crossfade" => CrossFade(parameters),
                "getstate" => GetState(parameters),
                "setspeed" => SetSpeed(parameters),
                "getparameters" => GetParameters(parameters),
                
                // Component management
                "attachanimator" => AttachAnimator(parameters),
                "assigncontroller" => AssignController(parameters),
                "configureavatar" => ConfigureAvatar(parameters),
                "applyrootmotion" => ApplyRootMotion(parameters),
                
                _ => new JObject { ["success"] = false, ["error"] = $"Unknown action: {action}" }
            };
        }

        #region Controller Operations

        /// <summary>
        /// Creates a new animator controller asset.
        /// </summary>
        private JObject CreateController(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                var name = parameters["name"]?.ToString() ?? "NewAnimatorController";

                if (string.IsNullOrEmpty(path))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Path parameter is required"
                    };
                }

                // Ensure path ends with .controller
                if (!path.EndsWith(".controller"))
                {
                    path = System.IO.Path.Combine(path, $"{name}.controller");
                }

                // Create the controller
                var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
                
                // Add default parameters if specified
                var defaultParams = parameters["defaultParameters"] as JArray;
                if (defaultParams != null)
                {
                    foreach (var param in defaultParams)
                    {
                        var paramName = param["name"]?.ToString();
                        var paramType = param["type"]?.ToString();
                        
                        if (!string.IsNullOrEmpty(paramName) && !string.IsNullOrEmpty(paramType))
                        {
                            AddParameterToController(controller, paramName, paramType);
                        }
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                return new JObject
                {
                    ["success"] = true,
                    ["path"] = path,
                    ["layers"] = controller.layers.Length,
                    ["parameters"] = controller.parameters.Length
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = ex.Message
                };
            }
        }

        /// <summary>
        /// Adds a state to an animator controller.
        /// </summary>
        private JObject AddState(JObject parameters)
        {
            try
            {
                var controllerPath = parameters["controllerPath"]?.ToString();
                var stateName = parameters["stateName"]?.ToString();
                var layerIndex = parameters["layerIndex"]?.Value<int>() ?? 0;
                var isDefault = parameters["isDefault"]?.Value<bool>() ?? false;
                var clipPath = parameters["clipPath"]?.ToString();

                if (string.IsNullOrEmpty(controllerPath) || string.IsNullOrEmpty(stateName))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "ControllerPath and stateName parameters are required"
                    };
                }

                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controller == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Animator controller not found at path: {controllerPath}"
                    };
                }

                if (layerIndex >= controller.layers.Length)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Layer index {layerIndex} out of range"
                    };
                }

                var stateMachine = controller.layers[layerIndex].stateMachine;
                var state = stateMachine.AddState(stateName);

                // Set as default if requested
                if (isDefault)
                {
                    stateMachine.defaultState = state;
                }

                // Assign animation clip if provided
                if (!string.IsNullOrEmpty(clipPath))
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                    if (clip != null)
                    {
                        state.motion = clip;
                    }
                }

                // Set position in state machine
                var position = new Vector3(
                    parameters["positionX"]?.Value<float>() ?? 250,
                    parameters["positionY"]?.Value<float>() ?? 100,
                    0
                );
                
                // Find the state in the state machine to set position
                var states = stateMachine.states;
                for (int i = 0; i < states.Length; i++)
                {
                    if (states[i].state == state)
                    {
                        var childState = states[i];
                        childState.position = position;
                        states[i] = childState;
                        break;
                    }
                }
                stateMachine.states = states;

                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();

                return new JObject
                {
                    ["success"] = true,
                    ["stateName"] = stateName,
                    ["stateHash"] = state.nameHash,
                    ["isDefault"] = state == stateMachine.defaultState
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = ex.Message
                };
            }
        }

        /// <summary>
        /// Adds a transition between states.
        /// </summary>
        private JObject AddTransition(JObject parameters)
        {
            try
            {
                var controllerPath = parameters["controllerPath"]?.ToString();
                var sourceState = parameters["sourceState"]?.ToString();
                var destinationState = parameters["destinationState"]?.ToString();
                var layerIndex = parameters["layerIndex"]?.Value<int>() ?? 0;
                var hasExitTime = parameters["hasExitTime"]?.Value<bool>() ?? true;
                var exitTime = parameters["exitTime"]?.Value<float>() ?? 1.0f;
                var duration = parameters["duration"]?.Value<float>() ?? 0.25f;
                var conditions = parameters["conditions"] as JArray;

                if (string.IsNullOrEmpty(controllerPath) || string.IsNullOrEmpty(destinationState))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "ControllerPath and destinationState parameters are required"
                    };
                }

                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controller == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Animator controller not found at path: {controllerPath}"
                    };
                }

                var stateMachine = controller.layers[layerIndex].stateMachine;
                AnimatorStateTransition transition = null;

                // Handle different source types
                if (string.IsNullOrEmpty(sourceState) || sourceState.ToLower() == "entry" || sourceState.ToLower() == "any")
                {
                    // Transition from Entry or Any state
                    var destState = FindState(stateMachine, destinationState);
                    if (destState == null)
                    {
                        return new JObject
                        {
                            ["success"] = false,
                            ["error"] = $"Destination state '{destinationState}' not found"
                        };
                    }

                    if (sourceState?.ToLower() == "any")
                    {
                        transition = stateMachine.AddAnyStateTransition(destState);
                    }
                    else
                    {
                        // Entry transitions return AnimatorTransition, which has limited properties
                        var entryTransition = stateMachine.AddEntryTransition(destState);
                        // AnimatorTransition only supports conditions, not timing properties
                        
                        // Add conditions if provided
                        if (conditions != null)
                        {
                            foreach (var condition in conditions)
                            {
                                var paramName = condition["parameter"]?.ToString();
                                var mode = condition["mode"]?.ToString();
                                var threshold = condition["threshold"]?.Value<float>() ?? 0;

                                if (!string.IsNullOrEmpty(paramName) && !string.IsNullOrEmpty(mode))
                                {
                                    var conditionMode = ParseConditionMode(mode);
                                    entryTransition.AddCondition(conditionMode, threshold, paramName);
                                }
                            }
                        }
                        
                        EditorUtility.SetDirty(controller);
                        AssetDatabase.SaveAssets();
                        
                        return new JObject
                        {
                            ["success"] = true,
                            ["sourceState"] = "Entry",
                            ["destinationState"] = destinationState,
                            ["hasExitTime"] = false, // Entry transitions don't support exit time
                            ["conditionCount"] = entryTransition.conditions.Length
                        };
                    }
                }
                else
                {
                    // Transition between two states
                    var srcState = FindState(stateMachine, sourceState);
                    var destState = FindState(stateMachine, destinationState);

                    if (srcState == null || destState == null)
                    {
                        return new JObject
                        {
                            ["success"] = false,
                            ["error"] = $"Source or destination state not found"
                        };
                    }

                    transition = srcState.AddTransition(destState);
                }

                // Configure transition (only for state-to-state and any-state transitions)
                if (transition != null)
                {
                    transition.hasExitTime = hasExitTime;
                    transition.exitTime = exitTime;
                    transition.duration = duration;
                    transition.hasFixedDuration = true;

                    // Add conditions if provided
                    if (conditions != null)
                    {
                        foreach (var condition in conditions)
                        {
                            var paramName = condition["parameter"]?.ToString();
                            var mode = condition["mode"]?.ToString();
                            var threshold = condition["threshold"]?.Value<float>() ?? 0;

                            if (!string.IsNullOrEmpty(paramName) && !string.IsNullOrEmpty(mode))
                            {
                                var conditionMode = ParseConditionMode(mode);
                                transition.AddCondition(conditionMode, threshold, paramName);
                            }
                        }
                    }
                }

                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();

                return new JObject
                {
                    ["success"] = true,
                    ["sourceState"] = sourceState ?? "Entry",
                    ["destinationState"] = destinationState,
                    ["hasExitTime"] = hasExitTime,
                    ["conditionCount"] = transition?.conditions.Length ?? 0
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = ex.Message
                };
            }
        }

        /// <summary>
        /// Sets a parameter in an animator controller.
        /// </summary>
        private JObject SetParameter(JObject parameters)
        {
            try
            {
                var controllerPath = parameters["controllerPath"]?.ToString();
                var parameterName = parameters["parameterName"]?.ToString();
                var parameterType = parameters["parameterType"]?.ToString();
                var defaultValue = parameters["defaultValue"];

                if (string.IsNullOrEmpty(controllerPath) || string.IsNullOrEmpty(parameterName) || string.IsNullOrEmpty(parameterType))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "ControllerPath, parameterName, and parameterType parameters are required"
                    };
                }

                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controller == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Animator controller not found at path: {controllerPath}"
                    };
                }

                // Check if parameter already exists
                var existingParam = controller.parameters.FirstOrDefault(p => p.name == parameterName);
                if (existingParam != null)
                {
                    controller.RemoveParameter(existingParam);
                }

                // Add new parameter
                var param = AddParameterToController(controller, parameterName, parameterType);

                // Set default value if provided
                if (defaultValue != null && param != null)
                {
                    switch (param.type)
                    {
                        case AnimatorControllerParameterType.Float:
                            param.defaultFloat = defaultValue.Value<float>();
                            break;
                        case AnimatorControllerParameterType.Int:
                            param.defaultInt = defaultValue.Value<int>();
                            break;
                        case AnimatorControllerParameterType.Bool:
                            param.defaultBool = defaultValue.Value<bool>();
                            break;
                    }
                }

                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();

                return new JObject
                {
                    ["success"] = true,
                    ["parameterName"] = parameterName,
                    ["parameterType"] = parameterType,
                    ["totalParameters"] = controller.parameters.Length
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = ex.Message
                };
            }
        }

        /// <summary>
        /// Adds a layer to an animator controller.
        /// </summary>
        private JObject AddLayer(JObject parameters)
        {
            try
            {
                var controllerPath = parameters["controllerPath"]?.ToString();
                var layerName = parameters["layerName"]?.ToString();
                var weight = parameters["weight"]?.Value<float>() ?? 1.0f;
                var blendMode = parameters["blendMode"]?.ToString() ?? "Override";

                if (string.IsNullOrEmpty(controllerPath) || string.IsNullOrEmpty(layerName))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "ControllerPath and layerName parameters are required"
                    };
                }

                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controller == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Animator controller not found at path: {controllerPath}"
                    };
                }

                // Create new layer
                controller.AddLayer(layerName);
                var layerIndex = controller.layers.Length - 1;
                var layer = controller.layers[layerIndex];
                
                // Configure layer
                layer.defaultWeight = weight;
                
                // Set blend mode
                if (Enum.TryParse<AnimatorLayerBlendingMode>(blendMode, true, out var mode))
                {
                    layer.blendingMode = mode;
                }

                controller.layers[layerIndex] = layer;

                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();

                return new JObject
                {
                    ["success"] = true,
                    ["layerName"] = layerName,
                    ["layerIndex"] = layerIndex,
                    ["totalLayers"] = controller.layers.Length
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = ex.Message
                };
            }
        }

        /// <summary>
        /// Sets up a blend tree in a state.
        /// </summary>
        private JObject SetBlendTree(JObject parameters)
        {
            try
            {
                var controllerPath = parameters["controllerPath"]?.ToString();
                var stateName = parameters["stateName"]?.ToString();
                var layerIndex = parameters["layerIndex"]?.Value<int>() ?? 0;
                var blendType = parameters["blendType"]?.ToString() ?? "1D";
                var blendParameter = parameters["blendParameter"]?.ToString();
                var motions = parameters["motions"] as JArray;

                if (string.IsNullOrEmpty(controllerPath) || string.IsNullOrEmpty(stateName) || string.IsNullOrEmpty(blendParameter))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "ControllerPath, stateName, and blendParameter parameters are required"
                    };
                }

                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controller == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Animator controller not found at path: {controllerPath}"
                    };
                }

                var stateMachine = controller.layers[layerIndex].stateMachine;
                var state = FindState(stateMachine, stateName);
                
                if (state == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"State '{stateName}' not found"
                    };
                }

                // Create blend tree
                var blendTree = new BlendTree();
                blendTree.name = $"{stateName}_BlendTree";
                blendTree.blendParameter = blendParameter;
                
                // Set blend type
                BlendTreeType treeType = blendType.ToUpper() switch
                {
                    "1D" => BlendTreeType.Simple1D,
                    "2D" => BlendTreeType.SimpleDirectional2D,
                    "2DCARTESIAN" => BlendTreeType.FreeformCartesian2D,
                    "2DDIRECTIONAL" => BlendTreeType.FreeformDirectional2D,
                    _ => BlendTreeType.Simple1D
                };
                blendTree.blendType = treeType;

                // Add second parameter for 2D blend trees
                if (treeType != BlendTreeType.Simple1D)
                {
                    var blendParameterY = parameters["blendParameterY"]?.ToString() ?? $"{blendParameter}Y";
                    blendTree.blendParameterY = blendParameterY;
                }

                // Add motions
                if (motions != null)
                {
                    var childMotions = new List<ChildMotion>();
                    foreach (var motion in motions)
                    {
                        var clipPath = motion["clipPath"]?.ToString();
                        var threshold = motion["threshold"]?.Value<float>() ?? 0;
                        var position = motion["position"] as JObject;
                        
                        if (!string.IsNullOrEmpty(clipPath))
                        {
                            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                            if (clip != null)
                            {
                                var childMotion = new ChildMotion
                                {
                                    motion = clip,
                                    threshold = threshold,
                                    timeScale = motion["timeScale"]?.Value<float>() ?? 1.0f
                                };
                                
                                // Set position for 2D blend trees
                                if (position != null && treeType != BlendTreeType.Simple1D)
                                {
                                    childMotion.position = new Vector2(
                                        position["x"]?.Value<float>() ?? 0,
                                        position["y"]?.Value<float>() ?? 0
                                    );
                                }
                                
                                childMotions.Add(childMotion);
                            }
                        }
                    }
                    blendTree.children = childMotions.ToArray();
                }

                // Assign blend tree to state
                state.motion = blendTree;
                
                // Add blend tree as sub-asset
                AssetDatabase.AddObjectToAsset(blendTree, controller);
                blendTree.hideFlags = HideFlags.HideInHierarchy;

                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();

                return new JObject
                {
                    ["success"] = true,
                    ["stateName"] = stateName,
                    ["blendType"] = blendType,
                    ["motionCount"] = blendTree.children.Length
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = ex.Message
                };
            }
        }

        /// <summary>
        /// Gets information about an animator controller.
        /// </summary>
        private JObject GetControllerInfo(JObject parameters)
        {
            try
            {
                var controllerPath = parameters["controllerPath"]?.ToString();

                if (string.IsNullOrEmpty(controllerPath))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "ControllerPath parameter is required"
                    };
                }

                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controller == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Animator controller not found at path: {controllerPath}"
                    };
                }

                // Gather layers info
                var layersArray = new JArray();
                foreach (var layer in controller.layers)
                {
                    var states = new JArray();
                    GatherStatesInfo(layer.stateMachine, states);
                    
                    layersArray.Add(new JObject
                    {
                        ["name"] = layer.name,
                        ["weight"] = layer.defaultWeight,
                        ["blendMode"] = layer.blendingMode.ToString(),
                        ["states"] = states
                    });
                }

                // Gather parameters info
                var parametersArray = new JArray();
                foreach (var param in controller.parameters)
                {
                    var paramInfo = new JObject
                    {
                        ["name"] = param.name,
                        ["type"] = param.type.ToString()
                    };

                    switch (param.type)
                    {
                        case AnimatorControllerParameterType.Float:
                            paramInfo["defaultValue"] = param.defaultFloat;
                            break;
                        case AnimatorControllerParameterType.Int:
                            paramInfo["defaultValue"] = param.defaultInt;
                            break;
                        case AnimatorControllerParameterType.Bool:
                            paramInfo["defaultValue"] = param.defaultBool;
                            break;
                    }

                    parametersArray.Add(paramInfo);
                }

                return new JObject
                {
                    ["success"] = true,
                    ["name"] = controller.name,
                    ["layers"] = layersArray,
                    ["parameters"] = parametersArray,
                    ["layerCount"] = controller.layers.Length,
                    ["parameterCount"] = controller.parameters.Length
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = ex.Message
                };
            }
        }

        #endregion

        #region Animation Clip Operations

        /// <summary>
        /// Creates a new animation clip.
        /// </summary>
        private JObject CreateClip(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                var name = parameters["name"]?.ToString() ?? "NewAnimationClip";
                var frameRate = parameters["frameRate"]?.Value<float>() ?? 30f;
                var isLooping = parameters["isLooping"]?.Value<bool>() ?? false;

                if (string.IsNullOrEmpty(path))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Path parameter is required"
                    };
                }

                // Ensure path ends with .anim
                if (!path.EndsWith(".anim"))
                {
                    path = System.IO.Path.Combine(path, $"{name}.anim");
                }

                // Create the animation clip
                var clip = new AnimationClip
                {
                    name = name,
                    frameRate = frameRate
                };

                // Set loop time
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = isLooping;
                AnimationUtility.SetAnimationClipSettings(clip, settings);

                // Add initial curves if provided
                var curves = parameters["curves"] as JArray;
                if (curves != null)
                {
                    foreach (var curveData in curves)
                    {
                        AddCurveToClip(clip, curveData as JObject);
                    }
                }

                // Create the asset
                AssetDatabase.CreateAsset(clip, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                return new JObject
                {
                    ["success"] = true,
                    ["path"] = path,
                    ["name"] = name,
                    ["frameRate"] = frameRate,
                    ["length"] = clip.length,
                    ["isLooping"] = isLooping
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = ex.Message
                };
            }
        }

        /// <summary>
        /// Adds a curve to an animation clip.
        /// </summary>
        private JObject AddCurve(JObject parameters)
        {
            try
            {
                var clipPath = parameters["clipPath"]?.ToString();
                var propertyPath = parameters["propertyPath"]?.ToString();
                var propertyName = parameters["propertyName"]?.ToString();
                var targetType = parameters["targetType"]?.ToString() ?? "Transform";
                var curveData = parameters["curve"] as JObject;

                if (string.IsNullOrEmpty(clipPath) || string.IsNullOrEmpty(propertyName))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "ClipPath and propertyName parameters are required"
                    };
                }

                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Animation clip not found at path: {clipPath}"
                    };
                }

                // Parse target type
                var type = GetTypeFromString(targetType);
                if (type == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Target type not found: {targetType}"
                    };
                }

                // Add curve to clip
                AddCurveToClip(clip, new JObject
                {
                    ["propertyPath"] = propertyPath ?? "",
                    ["propertyName"] = propertyName,
                    ["targetType"] = targetType,
                    ["curve"] = curveData
                });

                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssets();

                return new JObject
                {
                    ["success"] = true,
                    ["clipPath"] = clipPath,
                    ["propertyName"] = propertyName,
                    ["curveCount"] = AnimationUtility.GetCurveBindings(clip).Length
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = ex.Message
                };
            }
        }

        /// <summary>
        /// Sets keyframes for a curve in an animation clip.
        /// </summary>
        private JObject SetCurveKeys(JObject parameters)
        {
            try
            {
                var clipPath = parameters["clipPath"]?.ToString();
                var propertyPath = parameters["propertyPath"]?.ToString();
                var propertyName = parameters["propertyName"]?.ToString();
                var targetType = parameters["targetType"]?.ToString() ?? "Transform";
                var keyframes = parameters["keyframes"] as JArray;

                if (string.IsNullOrEmpty(clipPath) || string.IsNullOrEmpty(propertyName) || keyframes == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "ClipPath, propertyName, and keyframes parameters are required"
                    };
                }

                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Animation clip not found at path: {clipPath}"
                    };
                }

                // Parse target type
                var type = GetTypeFromString(targetType);
                if (type == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Target type not found: {targetType}"
                    };
                }

                // Create curve from keyframes
                var curve = new AnimationCurve();
                foreach (var keyData in keyframes)
                {
                    var time = keyData["time"]?.Value<float>() ?? 0;
                    var value = keyData["value"]?.Value<float>() ?? 0;
                    var inTangent = keyData["inTangent"]?.Value<float>() ?? 0;
                    var outTangent = keyData["outTangent"]?.Value<float>() ?? 0;
                    var tangentMode = keyData["tangentMode"]?.ToString() ?? "Auto";

                    var keyframe = new Keyframe(time, value, inTangent, outTangent);
                    
                    // Set tangent mode
                    if (tangentMode == "Linear")
                    {
                        keyframe.weightedMode = WeightedMode.None;
                    }
                    
                    curve.AddKey(keyframe);
                }

                // Apply curve to clip
                var binding = new EditorCurveBinding
                {
                    path = propertyPath ?? "",
                    type = type,
                    propertyName = propertyName
                };

                AnimationUtility.SetEditorCurve(clip, binding, curve);

                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssets();

                return new JObject
                {
                    ["success"] = true,
                    ["clipPath"] = clipPath,
                    ["propertyName"] = propertyName,
                    ["keyframeCount"] = curve.keys.Length
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = ex.Message
                };
            }
        }

        /// <summary>
        /// Gets information about an animation clip.
        /// </summary>
        private JObject GetClipInfo(JObject parameters)
        {
            try
            {
                var clipPath = parameters["clipPath"]?.ToString();

                if (string.IsNullOrEmpty(clipPath))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "ClipPath parameter is required"
                    };
                }

                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Animation clip not found at path: {clipPath}"
                    };
                }

                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                var bindings = AnimationUtility.GetCurveBindings(clip);

                var curvesArray = new JArray();
                foreach (var binding in bindings)
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    var keyframesArray = new JArray();
                    
                    foreach (var key in curve.keys)
                    {
                        keyframesArray.Add(new JObject
                        {
                            ["time"] = key.time,
                            ["value"] = key.value,
                            ["inTangent"] = key.inTangent,
                            ["outTangent"] = key.outTangent
                        });
                    }

                    curvesArray.Add(new JObject
                    {
                        ["propertyPath"] = binding.path,
                        ["propertyName"] = binding.propertyName,
                        ["targetType"] = binding.type.Name,
                        ["keyframes"] = keyframesArray
                    });
                }

                return new JObject
                {
                    ["success"] = true,
                    ["name"] = clip.name,
                    ["length"] = clip.length,
                    ["frameRate"] = clip.frameRate,
                    ["isLooping"] = settings.loopTime,
                    ["curves"] = curvesArray,
                    ["curveCount"] = bindings.Length
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = ex.Message
                };
            }
        }

        /// <summary>
        /// Duplicates an animation clip.
        /// </summary>
        private JObject DuplicateClip(JObject parameters)
        {
            try
            {
                var sourcePath = parameters["sourcePath"]?.ToString();
                var destinationPath = parameters["destinationPath"]?.ToString();
                var newName = parameters["newName"]?.ToString();

                if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destinationPath))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "SourcePath and destinationPath parameters are required"
                    };
                }

                var sourceClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(sourcePath);
                if (sourceClip == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Source animation clip not found at path: {sourcePath}"
                    };
                }

                // Create a copy
                var newClip = UnityEngine.Object.Instantiate(sourceClip);
                newClip.name = newName ?? $"{sourceClip.name}_Copy";

                // Ensure destination path ends with .anim
                if (!destinationPath.EndsWith(".anim"))
                {
                    destinationPath = System.IO.Path.Combine(destinationPath, $"{newClip.name}.anim");
                }

                AssetDatabase.CreateAsset(newClip, destinationPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                return new JObject
                {
                    ["success"] = true,
                    ["sourcePath"] = sourcePath,
                    ["destinationPath"] = destinationPath,
                    ["newName"] = newClip.name
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = ex.Message
                };
            }
        }

        /// <summary>
        /// Sets sprite animation curve using ObjectReferenceKeyframe for sprite sequences.
        /// </summary>
        private JObject SetSpriteAnimationCurve(JObject parameters)
        {
            try
            {
                var clipPath = parameters["clipPath"]?.ToString();
                var spritePaths = parameters["spritePaths"]?.ToObject<string[]>();
                var frameRate = parameters["frameRate"]?.Value<float>() ?? 24f;
                var targetType = parameters["targetType"]?.ToString() ?? "SpriteRenderer";
                var propertyPath = parameters["propertyPath"]?.ToString() ?? "";

                if (string.IsNullOrEmpty(clipPath))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "ClipPath parameter is required"
                    };
                }

                if (spritePaths == null || spritePaths.Length == 0)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "SpritePaths parameter is required and must contain at least one sprite path"
                    };
                }

                // Load the animation clip
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Animation clip not found at path: {clipPath}"
                    };
                }

                // Load sprites from asset paths
                var sprites = new List<Sprite>();
                foreach (var spritePath in spritePaths)
                {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                    if (sprite != null)
                    {
                        sprites.Add(sprite);
                    }
                    else
                    {
                        Debug.LogWarning($"Sprite not found at path: {spritePath}");
                    }
                }

                if (sprites.Count == 0)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "No valid sprites found from provided paths"
                    };
                }

                // Create ObjectReferenceKeyframe array
                var spriteKeyframes = new ObjectReferenceKeyframe[sprites.Count];
                var timePerFrame = 1f / frameRate;

                for (int i = 0; i < sprites.Count; i++)
                {
                    spriteKeyframes[i] = new ObjectReferenceKeyframe
                    {
                        time = i * timePerFrame,
                        value = sprites[i]
                    };
                }

                // Create binding for sprite property (m_Sprite is the backing field)
                System.Type componentType = targetType == "SpriteRenderer" ? typeof(SpriteRenderer) : typeof(SpriteRenderer);
                var spriteBinding = EditorCurveBinding.PPtrCurve(propertyPath, componentType, "m_Sprite");

                // Apply the sprite animation curve
                AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, spriteKeyframes);

                // Update clip settings
                var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
                clipSettings.loopTime = parameters["isLooping"]?.Value<bool>() ?? true;
                clipSettings.stopTime = (sprites.Count - 1) * timePerFrame;
                AnimationUtility.SetAnimationClipSettings(clip, clipSettings);

                // Set frame rate
                clip.frameRate = frameRate;

                // Mark clip as dirty and save
                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssets();

                return new JObject
                {
                    ["success"] = true,
                    ["clipPath"] = clipPath,
                    ["frameCount"] = sprites.Count,
                    ["frameRate"] = frameRate,
                    ["duration"] = (sprites.Count - 1) * timePerFrame,
                    ["isLooping"] = clipSettings.loopTime
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = ex.Message
                };
            }
        }

        /// <summary>
        /// Gets sprite animation curve information from an animation clip.
        /// </summary>
        private JObject GetSpriteAnimationCurve(JObject parameters)
        {
            try
            {
                var clipPath = parameters["clipPath"]?.ToString();
                var targetType = parameters["targetType"]?.ToString() ?? "SpriteRenderer";
                var propertyPath = parameters["propertyPath"]?.ToString() ?? "";

                if (string.IsNullOrEmpty(clipPath))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "ClipPath parameter is required"
                    };
                }

                // Load the animation clip
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Animation clip not found at path: {clipPath}"
                    };
                }

                // Create binding for sprite property
                System.Type componentType = targetType == "SpriteRenderer" ? typeof(SpriteRenderer) : typeof(SpriteRenderer);
                var spriteBinding = EditorCurveBinding.PPtrCurve(propertyPath, componentType, "m_Sprite");

                // Get the sprite animation curve
                var spriteKeyframes = AnimationUtility.GetObjectReferenceCurve(clip, spriteBinding);

                var spritePaths = new List<string>();
                if (spriteKeyframes != null)
                {
                    foreach (var keyframe in spriteKeyframes)
                    {
                        if (keyframe.value is Sprite sprite)
                        {
                            string assetPath = AssetDatabase.GetAssetPath(sprite);
                            spritePaths.Add(assetPath);
                        }
                    }
                }

                var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);

                return new JObject
                {
                    ["success"] = true,
                    ["clipPath"] = clipPath,
                    ["frameCount"] = spriteKeyframes?.Length ?? 0,
                    ["frameRate"] = clip.frameRate,
                    ["duration"] = clip.length,
                    ["isLooping"] = clipSettings.loopTime,
                    ["spritePaths"] = new JArray(spritePaths)
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = ex.Message
                };
            }
        }

        #endregion

        #region Runtime Animator Control

        /// <summary>
        /// Plays a specific animation state.
        /// </summary>
        private JObject Play(JObject parameters)
        {
            try
            {
                var gameObjectPath = parameters["gameObjectPath"]?.ToString();
                var stateName = parameters["stateName"]?.ToString();
                var layer = parameters["layer"]?.Value<int>() ?? -1;
                var normalizedTime = parameters["normalizedTime"]?.Value<float>() ?? float.NegativeInfinity;

                if (string.IsNullOrEmpty(gameObjectPath) || string.IsNullOrEmpty(stateName))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "GameObjectPath and stateName parameters are required"
                    };
                }

                var gameObject = GameObject.Find(gameObjectPath);
                if (gameObject == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"GameObject not found at path: {gameObjectPath}"
                    };
                }

                var animator = gameObject.GetComponent<Animator>();
                if (animator == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "GameObject does not have an Animator component"
                    };
                }

                animator.Play(stateName, layer, normalizedTime);

                return new JObject
                {
                    ["success"] = true,
                    ["gameObject"] = gameObjectPath,
                    ["stateName"] = stateName,
                    ["layer"] = layer
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = ex.Message
                };
            }
        }

        /// <summary>
        /// Sets a parameter value on a runtime animator.
        /// </summary>
        private JObject SetParameterValue(JObject parameters)
        {
            try
            {
                var gameObjectPath = parameters["gameObjectPath"]?.ToString();
                var parameterName = parameters["parameterName"]?.ToString();
                var parameterType = parameters["parameterType"]?.ToString();
                var value = parameters["value"];

                if (string.IsNullOrEmpty(gameObjectPath) || string.IsNullOrEmpty(parameterName) || string.IsNullOrEmpty(parameterType))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "GameObjectPath, parameterName, and parameterType parameters are required"
                    };
                }

                var gameObject = GameObject.Find(gameObjectPath);
                if (gameObject == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"GameObject not found at path: {gameObjectPath}"
                    };
                }

                var animator = gameObject.GetComponent<Animator>();
                if (animator == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "GameObject does not have an Animator component"
                    };
                }

                // Set parameter value based on type
                switch (parameterType.ToLower())
                {
                    case "float":
                        animator.SetFloat(parameterName, value?.Value<float>() ?? 0);
                        break;
                    case "int":
                    case "integer":
                        animator.SetInteger(parameterName, value?.Value<int>() ?? 0);
                        break;
                    case "bool":
                    case "boolean":
                        animator.SetBool(parameterName, value?.Value<bool>() ?? false);
                        break;
                    case "trigger":
                        animator.SetTrigger(parameterName);
                        break;
                    default:
                        return new JObject
                        {
                            ["success"] = false,
                            ["error"] = $"Unknown parameter type: {parameterType}"
                        };
                }

                return new JObject
                {
                    ["success"] = true,
                    ["parameterName"] = parameterName,
                    ["parameterType"] = parameterType,
                    ["value"] = value?.ToString() ?? "trigger"
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = ex.Message
                };
            }
        }

        /// <summary>
        /// Cross-fades to a specific animation state.
        /// </summary>
        private JObject CrossFade(JObject parameters)
        {
            try
            {
                var gameObjectPath = parameters["gameObjectPath"]?.ToString();
                var stateName = parameters["stateName"]?.ToString();
                var duration = parameters["duration"]?.Value<float>() ?? 0.25f;
                var layer = parameters["layer"]?.Value<int>() ?? -1;
                var normalizedTime = parameters["normalizedTime"]?.Value<float>() ?? float.NegativeInfinity;

                if (string.IsNullOrEmpty(gameObjectPath) || string.IsNullOrEmpty(stateName))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "GameObjectPath and stateName parameters are required"
                    };
                }

                var gameObject = GameObject.Find(gameObjectPath);
                if (gameObject == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"GameObject not found at path: {gameObjectPath}"
                    };
                }

                var animator = gameObject.GetComponent<Animator>();
                if (animator == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "GameObject does not have an Animator component"
                    };
                }

                animator.CrossFade(stateName, duration, layer, normalizedTime);

                return new JObject
                {
                    ["success"] = true,
                    ["gameObject"] = gameObjectPath,
                    ["stateName"] = stateName,
                    ["duration"] = duration,
                    ["layer"] = layer
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = ex.Message
                };
            }
        }

        /// <summary>
        /// Gets the current state of an animator.
        /// </summary>
        private JObject GetState(JObject parameters)
        {
            try
            {
                var gameObjectPath = parameters["gameObjectPath"]?.ToString();
                var layer = parameters["layer"]?.Value<int>() ?? 0;

                if (string.IsNullOrEmpty(gameObjectPath))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "GameObjectPath parameter is required"
                    };
                }

                var gameObject = GameObject.Find(gameObjectPath);
                if (gameObject == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"GameObject not found at path: {gameObjectPath}"
                    };
                }

                var animator = gameObject.GetComponent<Animator>();
                if (animator == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "GameObject does not have an Animator component"
                    };
                }

                var stateInfo = animator.GetCurrentAnimatorStateInfo(layer);
                var nextStateInfo = animator.GetNextAnimatorStateInfo(layer);
                var transition = animator.GetAnimatorTransitionInfo(layer);

                return new JObject
                {
                    ["success"] = true,
                    ["currentState"] = new JObject
                    {
                        ["fullPathHash"] = stateInfo.fullPathHash,
                        ["shortNameHash"] = stateInfo.shortNameHash,
                        ["normalizedTime"] = stateInfo.normalizedTime,
                        ["length"] = stateInfo.length,
                        ["speed"] = stateInfo.speed,
                        ["speedMultiplier"] = stateInfo.speedMultiplier,
                        ["isLooping"] = stateInfo.loop
                    },
                    ["isInTransition"] = animator.IsInTransition(layer),
                    ["nextState"] = animator.IsInTransition(layer) ? new JObject
                    {
                        ["fullPathHash"] = nextStateInfo.fullPathHash,
                        ["shortNameHash"] = nextStateInfo.shortNameHash,
                        ["normalizedTime"] = nextStateInfo.normalizedTime,
                        ["length"] = nextStateInfo.length
                    } : null,
                    ["transition"] = animator.IsInTransition(layer) ? new JObject
                    {
                        ["duration"] = transition.duration,
                        ["normalizedTime"] = transition.normalizedTime
                    } : null
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = ex.Message
                };
            }
        }

        /// <summary>
        /// Sets the speed of the animator.
        /// </summary>
        private JObject SetSpeed(JObject parameters)
        {
            try
            {
                var gameObjectPath = parameters["gameObjectPath"]?.ToString();
                var speed = parameters["speed"]?.Value<float>() ?? 1.0f;

                if (string.IsNullOrEmpty(gameObjectPath))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "GameObjectPath parameter is required"
                    };
                }

                var gameObject = GameObject.Find(gameObjectPath);
                if (gameObject == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"GameObject not found at path: {gameObjectPath}"
                    };
                }

                var animator = gameObject.GetComponent<Animator>();
                if (animator == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "GameObject does not have an Animator component"
                    };
                }

                animator.speed = speed;

                return new JObject
                {
                    ["success"] = true,
                    ["gameObject"] = gameObjectPath,
                    ["speed"] = speed
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = ex.Message
                };
            }
        }

        /// <summary>
        /// Gets all parameters from an animator.
        /// </summary>
        private JObject GetParameters(JObject parameters)
        {
            try
            {
                var gameObjectPath = parameters["gameObjectPath"]?.ToString();

                if (string.IsNullOrEmpty(gameObjectPath))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "GameObjectPath parameter is required"
                    };
                }

                var gameObject = GameObject.Find(gameObjectPath);
                if (gameObject == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"GameObject not found at path: {gameObjectPath}"
                    };
                }

                var animator = gameObject.GetComponent<Animator>();
                if (animator == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "GameObject does not have an Animator component"
                    };
                }

                var parametersArray = new JArray();
                for (int i = 0; i < animator.parameterCount; i++)
                {
                    var param = animator.GetParameter(i);
                    var paramInfo = new JObject
                    {
                        ["name"] = param.name,
                        ["type"] = param.type.ToString(),
                        ["nameHash"] = param.nameHash
                    };

                    // Get current value
                    switch (param.type)
                    {
                        case AnimatorControllerParameterType.Float:
                            paramInfo["value"] = animator.GetFloat(param.name);
                            break;
                        case AnimatorControllerParameterType.Int:
                            paramInfo["value"] = animator.GetInteger(param.name);
                            break;
                        case AnimatorControllerParameterType.Bool:
                            paramInfo["value"] = animator.GetBool(param.name);
                            break;
                    }

                    parametersArray.Add(paramInfo);
                }

                return new JObject
                {
                    ["success"] = true,
                    ["parameters"] = parametersArray,
                    ["parameterCount"] = animator.parameterCount
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = ex.Message
                };
            }
        }

        #endregion

        #region Component Management

        /// <summary>
        /// Attaches an Animator component to a GameObject.
        /// </summary>
        private JObject AttachAnimator(JObject parameters)
        {
            try
            {
                var gameObjectPath = parameters["gameObjectPath"]?.ToString();
                var controllerPath = parameters["controllerPath"]?.ToString();
                var applyRootMotion = parameters["applyRootMotion"]?.Value<bool>() ?? false;
                var updateMode = parameters["updateMode"]?.ToString() ?? "Normal";
                var cullingMode = parameters["cullingMode"]?.ToString() ?? "AlwaysAnimate";

                if (string.IsNullOrEmpty(gameObjectPath))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "GameObjectPath parameter is required"
                    };
                }

                var gameObject = GameObject.Find(gameObjectPath);
                if (gameObject == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"GameObject not found at path: {gameObjectPath}"
                    };
                }

                // Get or add Animator component
                var animator = gameObject.GetComponent<Animator>();
                if (animator == null)
                {
                    Undo.RecordObject(gameObject, "Add Animator");
                    animator = gameObject.AddComponent<Animator>();
                }
                else
                {
                    Undo.RecordObject(animator, "Configure Animator");
                }

                // Assign controller if provided
                if (!string.IsNullOrEmpty(controllerPath))
                {
                    var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
                    if (controller != null)
                    {
                        animator.runtimeAnimatorController = controller;
                    }
                }

                // Configure animator
                animator.applyRootMotion = applyRootMotion;
                
                if (Enum.TryParse<AnimatorUpdateMode>(updateMode, true, out var mode))
                {
                    animator.updateMode = mode;
                }
                
                if (Enum.TryParse<AnimatorCullingMode>(cullingMode, true, out var cullMode))
                {
                    animator.cullingMode = cullMode;
                }

                return new JObject
                {
                    ["success"] = true,
                    ["gameObject"] = gameObjectPath,
                    ["hasController"] = animator.runtimeAnimatorController != null,
                    ["applyRootMotion"] = animator.applyRootMotion,
                    ["updateMode"] = animator.updateMode.ToString(),
                    ["cullingMode"] = animator.cullingMode.ToString()
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = ex.Message
                };
            }
        }

        /// <summary>
        /// Assigns a controller to an existing Animator component.
        /// </summary>
        private JObject AssignController(JObject parameters)
        {
            try
            {
                var gameObjectPath = parameters["gameObjectPath"]?.ToString();
                var controllerPath = parameters["controllerPath"]?.ToString();

                if (string.IsNullOrEmpty(gameObjectPath) || string.IsNullOrEmpty(controllerPath))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "GameObjectPath and controllerPath parameters are required"
                    };
                }

                var gameObject = GameObject.Find(gameObjectPath);
                if (gameObject == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"GameObject not found at path: {gameObjectPath}"
                    };
                }

                var animator = gameObject.GetComponent<Animator>();
                if (animator == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "GameObject does not have an Animator component"
                    };
                }

                var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
                if (controller == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Animator controller not found at path: {controllerPath}"
                    };
                }

                Undo.RecordObject(animator, "Assign Controller");
                animator.runtimeAnimatorController = controller;

                return new JObject
                {
                    ["success"] = true,
                    ["gameObject"] = gameObjectPath,
                    ["controller"] = controller.name
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = ex.Message
                };
            }
        }

        /// <summary>
        /// Configures the avatar for an Animator.
        /// </summary>
        private JObject ConfigureAvatar(JObject parameters)
        {
            try
            {
                var gameObjectPath = parameters["gameObjectPath"]?.ToString();
                var avatarPath = parameters["avatarPath"]?.ToString();
                var avatarType = parameters["avatarType"]?.ToString() ?? "Generic";

                if (string.IsNullOrEmpty(gameObjectPath))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "GameObjectPath parameter is required"
                    };
                }

                var gameObject = GameObject.Find(gameObjectPath);
                if (gameObject == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"GameObject not found at path: {gameObjectPath}"
                    };
                }

                var animator = gameObject.GetComponent<Animator>();
                if (animator == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "GameObject does not have an Animator component"
                    };
                }

                // Load avatar if path provided
                if (!string.IsNullOrEmpty(avatarPath))
                {
                    var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);
                    if (avatar != null)
                    {
                        Undo.RecordObject(animator, "Configure Avatar");
                        animator.avatar = avatar;
                    }
                }

                return new JObject
                {
                    ["success"] = true,
                    ["gameObject"] = gameObjectPath,
                    ["hasAvatar"] = animator.avatar != null,
                    ["avatarName"] = animator.avatar?.name ?? "None",
                    ["isHuman"] = animator.avatar?.isHuman ?? false
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = ex.Message
                };
            }
        }

        /// <summary>
        /// Configures root motion settings for an Animator.
        /// </summary>
        private JObject ApplyRootMotion(JObject parameters)
        {
            try
            {
                var gameObjectPath = parameters["gameObjectPath"]?.ToString();
                var enabled = parameters["enabled"]?.Value<bool>() ?? true;

                if (string.IsNullOrEmpty(gameObjectPath))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "GameObjectPath parameter is required"
                    };
                }

                var gameObject = GameObject.Find(gameObjectPath);
                if (gameObject == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"GameObject not found at path: {gameObjectPath}"
                    };
                }

                var animator = gameObject.GetComponent<Animator>();
                if (animator == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "GameObject does not have an Animator component"
                    };
                }

                Undo.RecordObject(animator, "Apply Root Motion");
                animator.applyRootMotion = enabled;

                return new JObject
                {
                    ["success"] = true,
                    ["gameObject"] = gameObjectPath,
                    ["applyRootMotion"] = animator.applyRootMotion
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = ex.Message
                };
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Adds a parameter to an animator controller.
        /// </summary>
        private AnimatorControllerParameter AddParameterToController(AnimatorController controller, string name, string type)
        {
            AnimatorControllerParameterType paramType = type.ToLower() switch
            {
                "float" => AnimatorControllerParameterType.Float,
                "int" or "integer" => AnimatorControllerParameterType.Int,
                "bool" or "boolean" => AnimatorControllerParameterType.Bool,
                "trigger" => AnimatorControllerParameterType.Trigger,
                _ => AnimatorControllerParameterType.Float
            };

            controller.AddParameter(name, paramType);
            return controller.parameters.FirstOrDefault(p => p.name == name);
        }

        /// <summary>
        /// Finds a state in a state machine by name.
        /// </summary>
        private AnimatorState FindState(AnimatorStateMachine stateMachine, string name)
        {
            foreach (var state in stateMachine.states)
            {
                if (state.state.name == name)
                    return state.state;
            }

            // Check sub state machines
            foreach (var subMachine in stateMachine.stateMachines)
            {
                var found = FindState(subMachine.stateMachine, name);
                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>
        /// Parses a condition mode string.
        /// </summary>
        private AnimatorConditionMode ParseConditionMode(string mode)
        {
            return mode.ToLower() switch
            {
                "greater" => AnimatorConditionMode.Greater,
                "less" => AnimatorConditionMode.Less,
                "equals" => AnimatorConditionMode.Equals,
                "notequal" or "notequals" => AnimatorConditionMode.NotEqual,
                "if" or "true" => AnimatorConditionMode.If,
                "ifnot" or "false" => AnimatorConditionMode.IfNot,
                _ => AnimatorConditionMode.Equals
            };
        }

        /// <summary>
        /// Gets a Type from a string name.
        /// </summary>
        private Type GetTypeFromString(string typeName)
        {
            // Try common Unity types first
            var unityType = Type.GetType($"UnityEngine.{typeName}, UnityEngine");
            if (unityType != null)
                return unityType;

            // Try all loaded assemblies
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null)
                    return type;
            }

            return typeof(Transform); // Default to Transform
        }

        /// <summary>
        /// Adds a curve to an animation clip.
        /// </summary>
        private void AddCurveToClip(AnimationClip clip, JObject curveData)
        {
            if (curveData == null) return;

            var propertyPath = curveData["propertyPath"]?.ToString() ?? "";
            var propertyName = curveData["propertyName"]?.ToString();
            var targetType = curveData["targetType"]?.ToString() ?? "Transform";
            var curve = curveData["curve"] as JObject;

            if (string.IsNullOrEmpty(propertyName)) return;

            var type = GetTypeFromString(targetType);
            var animCurve = new AnimationCurve();

            // Add keyframes from curve data
            if (curve != null)
            {
                var keyframes = curve["keyframes"] as JArray;
                if (keyframes != null)
                {
                    foreach (var keyData in keyframes)
                    {
                        var time = keyData["time"]?.Value<float>() ?? 0;
                        var value = keyData["value"]?.Value<float>() ?? 0;
                        animCurve.AddKey(time, value);
                    }
                }
            }

            // If no keyframes provided, add default ones
            if (animCurve.keys.Length == 0)
            {
                animCurve.AddKey(0, 0);
                animCurve.AddKey(1, 1);
            }

            var binding = new EditorCurveBinding
            {
                path = propertyPath,
                type = type,
                propertyName = propertyName
            };

            AnimationUtility.SetEditorCurve(clip, binding, animCurve);
        }

        /// <summary>
        /// Gathers information about states in a state machine.
        /// </summary>
        private void GatherStatesInfo(AnimatorStateMachine stateMachine, JArray statesArray)
        {
            foreach (var state in stateMachine.states)
            {
                var stateInfo = new JObject
                {
                    ["name"] = state.state.name,
                    ["position"] = new JObject
                    {
                        ["x"] = state.position.x,
                        ["y"] = state.position.y
                    },
                    ["hasMotion"] = state.state.motion != null,
                    ["motionName"] = state.state.motion?.name ?? "None",
                    ["isBlendTree"] = state.state.motion is BlendTree
                };

                // Add transition info
                var transitions = new JArray();
                foreach (var transition in state.state.transitions)
                {
                    transitions.Add(new JObject
                    {
                        ["destination"] = transition.destinationState?.name ?? "Exit",
                        ["hasExitTime"] = transition.hasExitTime,
                        ["conditions"] = transition.conditions.Length
                    });
                }
                stateInfo["transitions"] = transitions;

                statesArray.Add(stateInfo);
            }

            // Recurse into sub state machines
            foreach (var subMachine in stateMachine.stateMachines)
            {
                GatherStatesInfo(subMachine.stateMachine, statesArray);
            }
        }

        #endregion
    }
}