using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityMCP.Editor.Core;

namespace UnityMCP.Editor.Handlers
{
    /// <summary>
    /// Command handler for Inspector operations - component manipulation on GameObjects.
    /// </summary>
    internal sealed class InspectorCommandHandler : IMcpCommandHandler
    {
        /// <summary>
        /// Gets the command prefix for this handler.
        /// </summary>
        public string CommandPrefix => "inspector";

        /// <summary>
        /// Gets the description of this command handler.
        /// </summary>
        public string Description => "Manipulate components on GameObjects (add, remove, modify, find references, prefab operations)";

        private static Dictionary<string, JObject> componentClipboard = new Dictionary<string, JObject>();

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
                "addcomponent" => AddComponent(parameters),
                "removecomponent" => RemoveComponent(parameters),
                "modifycomponent" => ModifyComponent(parameters),
                "enablecomponent" => EnableComponent(parameters),
                "copycomponent" => CopyComponent(parameters),
                "pastecomponent" => PasteComponent(parameters),
                "resetcomponent" => ResetComponent(parameters),
                "getcomponents" => GetComponents(parameters),
                "getcomponentproperties" => GetComponentProperties(parameters),
                "findreferences" => FindReferences(parameters),
                "applytoprefab" => ApplyToPrefab(parameters),
                "revertfromprefab" => RevertFromPrefab(parameters),
                "isprefab" => IsPrefab(parameters),
                _ => new JObject { ["success"] = false, ["error"] = $"Unknown action: {action}" }
            };
        }

        /// <summary>
        /// Adds a component to a GameObject.
        /// </summary>
        private JObject AddComponent(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                var componentType = parameters["componentType"]?.ToString();

                if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(componentType))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Path and componentType parameters are required"
                    };
                }

                var gameObject = FindGameObjectByPath(path);
                if (gameObject == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"GameObject not found at path: {path}"
                    };
                }

                var type = GetComponentType(componentType);
                if (type == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Component type not found: {componentType}"
                    };
                }

                // Check if component already exists (for non-duplicate components)
                if (!AllowsMultiple(type) && gameObject.GetComponent(type) != null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Component {componentType} already exists on GameObject and doesn't allow duplicates"
                    };
                }

                Undo.RecordObject(gameObject, $"Add {componentType}");
                var component = gameObject.AddComponent(type);

                return new JObject
                {
                    ["success"] = true,
                    ["component"] = new JObject
                    {
                        ["type"] = component.GetType().Name,
                        ["fullType"] = component.GetType().FullName,
                        ["enabled"] = component is Behaviour behaviour ? behaviour.enabled : true
                    }
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
        /// Removes a component from a GameObject.
        /// </summary>
        private JObject RemoveComponent(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                var componentType = parameters["componentType"]?.ToString();
                var index = parameters["index"]?.Value<int>() ?? 0;

                if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(componentType))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Path and componentType parameters are required"
                    };
                }

                var gameObject = FindGameObjectByPath(path);
                if (gameObject == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"GameObject not found at path: {path}"
                    };
                }

                var type = GetComponentType(componentType);
                if (type == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Component type not found: {componentType}"
                    };
                }

                // Don't allow removing Transform component
                if (type == typeof(Transform))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Cannot remove Transform component"
                    };
                }

                var components = gameObject.GetComponents(type);
                if (components.Length == 0)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Component {componentType} not found on GameObject"
                    };
                }

                if (index >= components.Length)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Component index {index} out of range (found {components.Length} components)"
                    };
                }

                var component = components[index];
                Undo.DestroyObjectImmediate(component);

                return new JObject
                {
                    ["success"] = true,
                    ["removed"] = componentType
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
        /// Modifies properties of a component.
        /// </summary>
        private JObject ModifyComponent(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                var componentType = parameters["componentType"]?.ToString();
                var index = parameters["index"]?.Value<int>() ?? 0;
                var properties = parameters["properties"] as JObject;

                if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(componentType) || properties == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Path, componentType, and properties parameters are required"
                    };
                }

                var gameObject = FindGameObjectByPath(path);
                if (gameObject == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"GameObject not found at path: {path}"
                    };
                }

                var type = GetComponentType(componentType);
                if (type == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Component type not found: {componentType}"
                    };
                }

                var components = gameObject.GetComponents(type);
                if (index >= components.Length)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Component index {index} out of range"
                    };
                }

                var component = components[index];
                Undo.RecordObject(component, $"Modify {componentType}");

                var modifiedProperties = new JArray();
                foreach (var prop in properties)
                {
                    if (SetComponentProperty(component, prop.Key, prop.Value))
                    {
                        modifiedProperties.Add(prop.Key);
                    }
                }

                return new JObject
                {
                    ["success"] = true,
                    ["modifiedProperties"] = modifiedProperties
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
        /// Enables or disables a component.
        /// </summary>
        private JObject EnableComponent(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                var componentType = parameters["componentType"]?.ToString();
                var enabled = parameters["enabled"]?.Value<bool>();
                var index = parameters["index"]?.Value<int>() ?? 0;

                if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(componentType) || enabled == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Path, componentType, and enabled parameters are required"
                    };
                }

                var gameObject = FindGameObjectByPath(path);
                if (gameObject == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"GameObject not found at path: {path}"
                    };
                }

                var type = GetComponentType(componentType);
                if (type == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Component type not found: {componentType}"
                    };
                }

                var components = gameObject.GetComponents(type);
                if (index >= components.Length)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Component index {index} out of range"
                    };
                }

                var component = components[index];
                if (component is Behaviour behaviour)
                {
                    Undo.RecordObject(behaviour, $"Enable {componentType}");
                    behaviour.enabled = enabled.Value;
                }
                else if (component is Renderer renderer)
                {
                    Undo.RecordObject(renderer, $"Enable {componentType}");
                    renderer.enabled = enabled.Value;
                }
                else if (component is Collider collider)
                {
                    Undo.RecordObject(collider, $"Enable {componentType}");
                    collider.enabled = enabled.Value;
                }
                else
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Component type does not support enable/disable"
                    };
                }

                return new JObject
                {
                    ["success"] = true,
                    ["enabled"] = enabled.Value
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
        /// Copies a component's values to clipboard.
        /// </summary>
        private JObject CopyComponent(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                var componentType = parameters["componentType"]?.ToString();
                var index = parameters["index"]?.Value<int>() ?? 0;

                if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(componentType))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Path and componentType parameters are required"
                    };
                }

                var gameObject = FindGameObjectByPath(path);
                if (gameObject == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"GameObject not found at path: {path}"
                    };
                }

                var type = GetComponentType(componentType);
                if (type == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Component type not found: {componentType}"
                    };
                }

                var components = gameObject.GetComponents(type);
                if (index >= components.Length)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Component index {index} out of range"
                    };
                }

                var component = components[index];
                var serializedData = SerializeComponent(component);
                componentClipboard[componentType] = serializedData;

                return new JObject
                {
                    ["success"] = true,
                    ["copiedType"] = componentType
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
        /// Pastes component values from clipboard.
        /// </summary>
        private JObject PasteComponent(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                var componentType = parameters["componentType"]?.ToString();
                var index = parameters["index"]?.Value<int>() ?? 0;

                if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(componentType))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Path and componentType parameters are required"
                    };
                }

                if (!componentClipboard.ContainsKey(componentType))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"No copied data for component type: {componentType}"
                    };
                }

                var gameObject = FindGameObjectByPath(path);
                if (gameObject == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"GameObject not found at path: {path}"
                    };
                }

                var type = GetComponentType(componentType);
                if (type == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Component type not found: {componentType}"
                    };
                }

                var components = gameObject.GetComponents(type);
                if (index >= components.Length)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Component index {index} out of range"
                    };
                }

                var component = components[index];
                Undo.RecordObject(component, $"Paste {componentType}");
                DeserializeComponent(component, componentClipboard[componentType]);

                return new JObject
                {
                    ["success"] = true,
                    ["pastedType"] = componentType
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
        /// Resets a component to default values.
        /// </summary>
        private JObject ResetComponent(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                var componentType = parameters["componentType"]?.ToString();
                var index = parameters["index"]?.Value<int>() ?? 0;

                if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(componentType))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Path and componentType parameters are required"
                    };
                }

                var gameObject = FindGameObjectByPath(path);
                if (gameObject == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"GameObject not found at path: {path}"
                    };
                }

                var type = GetComponentType(componentType);
                if (type == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Component type not found: {componentType}"
                    };
                }

                var components = gameObject.GetComponents(type);
                if (index >= components.Length)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Component index {index} out of range"
                    };
                }

                var component = components[index];
                Undo.RecordObject(component, $"Reset {componentType}");

                // Try to call Reset method if it exists
                var resetMethod = type.GetMethod("Reset", BindingFlags.NonPublic | BindingFlags.Instance);
                if (resetMethod != null)
                {
                    resetMethod.Invoke(component, null);
                }
                else
                {
                    // Manual reset for common Unity components
                    ResetComponentManually(component);
                }

                return new JObject
                {
                    ["success"] = true,
                    ["resetType"] = componentType
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
        /// Gets all components on a GameObject.
        /// </summary>
        private JObject GetComponents(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                var includeProperties = parameters["includeProperties"]?.Value<bool>() ?? false;

                if (string.IsNullOrEmpty(path))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Path parameter is required"
                    };
                }

                var gameObject = FindGameObjectByPath(path);
                if (gameObject == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"GameObject not found at path: {path}"
                    };
                }

                var components = gameObject.GetComponents<Component>();
                var componentArray = new JArray();

                foreach (var component in components)
                {
                    if (component == null) continue;
                    
                    var componentInfo = new JObject
                    {
                        ["type"] = component.GetType().Name,
                        ["fullType"] = component.GetType().FullName,
                        ["enabled"] = component is Behaviour behaviour ? behaviour.enabled :
                                     component is Renderer renderer ? renderer.enabled :
                                     component is Collider collider ? collider.enabled : true
                    };

                    if (includeProperties)
                    {
                        componentInfo["properties"] = SerializeComponent(component);
                    }

                    componentArray.Add(componentInfo);
                }

                return new JObject
                {
                    ["success"] = true,
                    ["gameObject"] = gameObject.name,
                    ["components"] = componentArray,
                    ["count"] = componentArray.Count
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
        /// Gets detailed properties of a specific component.
        /// </summary>
        private JObject GetComponentProperties(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                var componentType = parameters["componentType"]?.ToString();
                var index = parameters["index"]?.Value<int>() ?? 0;

                if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(componentType))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Path and componentType parameters are required"
                    };
                }

                var gameObject = FindGameObjectByPath(path);
                if (gameObject == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"GameObject not found at path: {path}"
                    };
                }

                var type = GetComponentType(componentType);
                if (type == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Component type not found: {componentType}"
                    };
                }

                var components = gameObject.GetComponents(type);
                if (index >= components.Length)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Component index {index} out of range"
                    };
                }

                var component = components[index];
                var properties = SerializeComponent(component);

                return new JObject
                {
                    ["success"] = true,
                    ["componentType"] = componentType,
                    ["properties"] = properties
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
        /// Finds all GameObjects that have a specific component type.
        /// </summary>
        private JObject FindReferences(JObject parameters)
        {
            try
            {
                var componentType = parameters["componentType"]?.ToString();
                var searchInactive = parameters["searchInactive"]?.Value<bool>() ?? true;
                var searchPrefabs = parameters["searchPrefabs"]?.Value<bool>() ?? false;
                var limit = parameters["limit"]?.Value<int>() ?? 100;

                if (string.IsNullOrEmpty(componentType))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "ComponentType parameter is required"
                    };
                }

                var type = GetComponentType(componentType);
                if (type == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Component type not found: {componentType}"
                    };
                }

                var results = new List<GameObject>();

                // Search in scene
                if (searchInactive)
                {
                    var allComponents = global::UnityEngine.Resources.FindObjectsOfTypeAll(type);
                    foreach (var comp in allComponents)
                    {
                        if (comp is Component component && !IsEditorOnly(component.gameObject))
                        {
                            results.Add(component.gameObject);
                        }
                    }
                }
                else
                {
                    var activeComponents = global::UnityEngine.Object.FindObjectsByType(type, global::UnityEngine.FindObjectsSortMode.None);
                    foreach (var comp in activeComponents)
                    {
                        if (comp is Component component)
                        {
                            results.Add(component.gameObject);
                        }
                    }
                }

                // Search in prefabs if requested
                if (searchPrefabs)
                {
                    var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
                    foreach (var guid in prefabGuids)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (prefab != null && prefab.GetComponent(type) != null)
                        {
                            results.Add(prefab);
                        }
                    }
                }

                // Remove duplicates and limit results
                results = results.Distinct().Take(limit).ToList();

                var resultArray = new JArray();
                foreach (var obj in results)
                {
                    resultArray.Add(new JObject
                    {
                        ["name"] = obj.name,
                        ["path"] = GetGameObjectPath(obj),
                        ["isPrefab"] = PrefabUtility.IsPartOfPrefabAsset(obj),
                        ["instanceId"] = obj.GetInstanceID()
                    });
                }

                return new JObject
                {
                    ["success"] = true,
                    ["componentType"] = componentType,
                    ["results"] = resultArray,
                    ["count"] = resultArray.Count
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
        /// Applies changes to the prefab source.
        /// </summary>
        private JObject ApplyToPrefab(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                var applyAll = parameters["applyAll"]?.Value<bool>() ?? true;

                if (string.IsNullOrEmpty(path))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Path parameter is required"
                    };
                }

                var gameObject = FindGameObjectByPath(path);
                if (gameObject == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"GameObject not found at path: {path}"
                    };
                }

                // Check if it's a prefab instance
                if (!PrefabUtility.IsPartOfPrefabInstance(gameObject))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "GameObject is not a prefab instance"
                    };
                }

                // Get the nearest prefab instance root
                var prefabInstanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(gameObject);
                if (prefabInstanceRoot == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Could not find prefab instance root"
                    };
                }

                // Apply overrides
                if (applyAll)
                {
                    PrefabUtility.ApplyPrefabInstance(prefabInstanceRoot, InteractionMode.UserAction);
                }
                else
                {
                    // Apply only the specific GameObject's overrides
                    var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
                    if (!string.IsNullOrEmpty(prefabPath))
                    {
                        PrefabUtility.ApplyObjectOverride(gameObject, prefabPath, InteractionMode.UserAction);
                    }
                }

                return new JObject
                {
                    ["success"] = true,
                    ["appliedTo"] = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject)
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
        /// Reverts changes from the prefab source.
        /// </summary>
        private JObject RevertFromPrefab(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                var revertAll = parameters["revertAll"]?.Value<bool>() ?? true;

                if (string.IsNullOrEmpty(path))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Path parameter is required"
                    };
                }

                var gameObject = FindGameObjectByPath(path);
                if (gameObject == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"GameObject not found at path: {path}"
                    };
                }

                // Check if it's a prefab instance
                if (!PrefabUtility.IsPartOfPrefabInstance(gameObject))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "GameObject is not a prefab instance"
                    };
                }

                // Get the nearest prefab instance root
                var prefabInstanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(gameObject);
                if (prefabInstanceRoot == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Could not find prefab instance root"
                    };
                }

                // Revert overrides
                if (revertAll)
                {
                    PrefabUtility.RevertPrefabInstance(prefabInstanceRoot, InteractionMode.UserAction);
                }
                else
                {
                    // Revert only the specific GameObject's overrides
                    PrefabUtility.RevertObjectOverride(gameObject, InteractionMode.UserAction);
                }

                return new JObject
                {
                    ["success"] = true,
                    ["revertedFrom"] = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject)
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
        /// Checks if a GameObject is a prefab or prefab instance.
        /// </summary>
        private JObject IsPrefab(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();

                if (string.IsNullOrEmpty(path))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Path parameter is required"
                    };
                }

                var gameObject = FindGameObjectByPath(path);
                if (gameObject == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"GameObject not found at path: {path}"
                    };
                }

                var isPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(gameObject);
                var isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(gameObject);
                var hasOverrides = false;
                string prefabPath = null;

                if (isPrefabInstance)
                {
                    prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
                    var prefabInstanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(gameObject);
                    hasOverrides = PrefabUtility.HasPrefabInstanceAnyOverrides(prefabInstanceRoot, false);
                }

                return new JObject
                {
                    ["success"] = true,
                    ["isPrefabInstance"] = isPrefabInstance,
                    ["isPrefabAsset"] = isPrefabAsset,
                    ["hasOverrides"] = hasOverrides,
                    ["prefabPath"] = prefabPath
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

        // Helper methods

        /// <summary>
        /// Finds a GameObject by its hierarchy path.
        /// </summary>
        private GameObject FindGameObjectByPath(string path)
        {
            var obj = GameObject.Find(path);
            if (obj != null) return obj;

            // Try to find inactive object
            return FindInactiveObjectByPath(path);
        }

        /// <summary>
        /// Finds an inactive GameObject by path.
        /// </summary>
        private GameObject FindInactiveObjectByPath(string path)
        {
            var parts = path.Split('/');
            if (parts.Length == 0) return null;

            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();
            
            GameObject current = null;
            foreach (var root in rootObjects)
            {
                if (root.name == parts[0])
                {
                    current = root;
                    break;
                }
            }

            if (current == null) return null;

            for (int i = 1; i < parts.Length; i++)
            {
                Transform child = current.transform.Find(parts[i]);
                if (child == null) return null;
                current = child.gameObject;
            }

            return current;
        }

        /// <summary>
        /// Gets the full path of a GameObject in the hierarchy.
        /// </summary>
        private string GetGameObjectPath(GameObject obj)
        {
            var path = obj.name;
            var parent = obj.transform.parent;
            
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            
            return path;
        }

        /// <summary>
        /// Gets a component type from its name.
        /// </summary>
        private Type GetComponentType(string typeName)
        {
            // Try common Unity components first
            var unityType = Type.GetType($"UnityEngine.{typeName}, UnityEngine");
            if (unityType != null && typeof(Component).IsAssignableFrom(unityType))
                return unityType;

            // Try UnityEngine.UI components
            var uiType = Type.GetType($"UnityEngine.UI.{typeName}, UnityEngine.UI");
            if (uiType != null && typeof(Component).IsAssignableFrom(uiType))
                return uiType;

            // Search all loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null && typeof(Component).IsAssignableFrom(type))
                    return type;

                // Try with just the type name (without namespace)
                var types = assembly.GetTypes().Where(t => t.Name == typeName && typeof(Component).IsAssignableFrom(t));
                if (types.Any())
                    return types.First();
            }

            return null;
        }

        /// <summary>
        /// Checks if a component type allows multiple instances.
        /// </summary>
        private bool AllowsMultiple(Type componentType)
        {
            var disallowMultiple = componentType.GetCustomAttribute<DisallowMultipleComponent>();
            return disallowMultiple == null;
        }

        /// <summary>
        /// Serializes a component's properties to JSON.
        /// </summary>
        private JObject SerializeComponent(Component component)
        {
            var result = new JObject();
            var type = component.GetType();

            // Get serialized fields
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => f.GetCustomAttribute<SerializeField>() != null || 
                           (f.IsPublic && f.GetCustomAttribute<System.NonSerializedAttribute>() == null));

            foreach (var field in fields)
            {
                try
                {
                    var value = field.GetValue(component);
                    result[field.Name] = SerializeValue(value);
                }
                catch { }
            }

            // Get public properties with setters
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite);

            foreach (var prop in properties)
            {
                try
                {
                    // Skip properties that cause instantiation in edit mode
                    // For Renderers: skip 'material' and 'materials' - use 'sharedMaterial'/'sharedMaterials' instead
                    if (component is Renderer && (prop.Name == "material" || prop.Name == "materials"))
                    {
                        continue;
                    }
                    
                    // For MeshFilter: skip 'mesh' - use 'sharedMesh' instead
                    if (component is MeshFilter && prop.Name == "mesh")
                    {
                        continue;
                    }
                    
                    // For MeshCollider: skip 'material' property which also causes instantiation
                    if (component is Collider && prop.Name == "material")
                    {
                        continue;
                    }
                    
                    var value = prop.GetValue(component);
                    result[prop.Name] = SerializeValue(value);
                }
                catch { }
            }

            return result;
        }

        /// <summary>
        /// Serializes a value to JToken.
        /// </summary>
        private JToken SerializeValue(object value)
        {
            if (value == null) return JValue.CreateNull();
            
            var type = value.GetType();
            
            if (type.IsPrimitive || type == typeof(string))
                return JToken.FromObject(value);
            
            if (type == typeof(Vector3))
            {
                var v = (Vector3)value;
                return new JObject { ["x"] = v.x, ["y"] = v.y, ["z"] = v.z };
            }
            
            if (type == typeof(Vector2))
            {
                var v = (Vector2)value;
                return new JObject { ["x"] = v.x, ["y"] = v.y };
            }
            
            if (type == typeof(Quaternion))
            {
                var q = (Quaternion)value;
                return new JObject { ["x"] = q.x, ["y"] = q.y, ["z"] = q.z, ["w"] = q.w };
            }
            
            if (type == typeof(Color))
            {
                var c = (Color)value;
                return new JObject { ["r"] = c.r, ["g"] = c.g, ["b"] = c.b, ["a"] = c.a };
            }
            
            // Special handling for Materials to avoid accessing properties that might not exist
            if (value is Material material)
            {
                return new JObject
                {
                    ["name"] = material.name,
                    ["shader"] = material.shader != null ? material.shader.name : "null"
                };
            }

            // For other types, try JSON serialization
            try
            {
                return JToken.FromObject(value);
            }
            catch
            {
                return JValue.CreateString(value.ToString());
            }
        }

        /// <summary>
        /// Deserializes component data from JSON.
        /// </summary>
        private void DeserializeComponent(Component component, JObject data)
        {
            var type = component.GetType();

            foreach (var prop in data)
            {
                SetComponentProperty(component, prop.Key, prop.Value);
            }
        }

        /// <summary>
        /// Sets a property value on a component.
        /// </summary>
        private bool SetComponentProperty(Component component, string propertyName, JToken value)
        {
            var type = component.GetType();

            // Try field first
            var field = type.GetField(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                try
                {
                    var convertedValue = ConvertValue(value, field.FieldType);
                    field.SetValue(component, convertedValue);
                    return true;
                }
                catch { }
            }

            // Try property
            var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.CanWrite)
            {
                try
                {
                    var convertedValue = ConvertValue(value, property.PropertyType);
                    property.SetValue(component, convertedValue);
                    return true;
                }
                catch { }
            }

            return false;
        }

        /// <summary>
        /// Converts a JToken value to the specified type.
        /// </summary>
        private object ConvertValue(JToken value, Type targetType)
        {
            if (targetType == typeof(Vector3))
            {
                var obj = value as JObject;
                if (obj != null)
                {
                    return new Vector3(
                        obj["x"]?.Value<float>() ?? 0,
                        obj["y"]?.Value<float>() ?? 0,
                        obj["z"]?.Value<float>() ?? 0
                    );
                }
            }
            
            if (targetType == typeof(Vector2))
            {
                var obj = value as JObject;
                if (obj != null)
                {
                    return new Vector2(
                        obj["x"]?.Value<float>() ?? 0,
                        obj["y"]?.Value<float>() ?? 0
                    );
                }
            }
            
            if (targetType == typeof(Quaternion))
            {
                var obj = value as JObject;
                if (obj != null)
                {
                    return new Quaternion(
                        obj["x"]?.Value<float>() ?? 0,
                        obj["y"]?.Value<float>() ?? 0,
                        obj["z"]?.Value<float>() ?? 0,
                        obj["w"]?.Value<float>() ?? 1
                    );
                }
            }
            
            if (targetType == typeof(Color))
            {
                var obj = value as JObject;
                if (obj != null)
                {
                    return new Color(
                        obj["r"]?.Value<float>() ?? 0,
                        obj["g"]?.Value<float>() ?? 0,
                        obj["b"]?.Value<float>() ?? 0,
                        obj["a"]?.Value<float>() ?? 1
                    );
                }
            }

            // Default conversion
            return value.ToObject(targetType);
        }

        /// <summary>
        /// Manually resets common Unity components.
        /// </summary>
        private void ResetComponentManually(Component component)
        {
            switch (component)
            {
                case Transform transform:
                    transform.localPosition = Vector3.zero;
                    transform.localRotation = Quaternion.identity;
                    transform.localScale = Vector3.one;
                    break;
                case Rigidbody rb:
                    rb.mass = 1;
                    rb.linearDamping = 0;
                    rb.angularDamping = 0.05f;
                    rb.useGravity = true;
                    rb.isKinematic = false;
                    break;
                case BoxCollider box:
                    box.center = Vector3.zero;
                    box.size = Vector3.one;
                    break;
                case SphereCollider sphere:
                    sphere.center = Vector3.zero;
                    sphere.radius = 0.5f;
                    break;
                case CapsuleCollider capsule:
                    capsule.center = Vector3.zero;
                    capsule.radius = 0.5f;
                    capsule.height = 2;
                    break;
            }
        }

        /// <summary>
        /// Checks if a GameObject is editor-only.
        /// </summary>
        private bool IsEditorOnly(GameObject obj)
        {
            return obj.hideFlags == HideFlags.HideAndDontSave ||
                   obj.scene.name == null ||
                   obj.scene.name == "DontDestroyOnLoad" ||
                   obj.scene.name == "PreviewScene";
        }
    }
}