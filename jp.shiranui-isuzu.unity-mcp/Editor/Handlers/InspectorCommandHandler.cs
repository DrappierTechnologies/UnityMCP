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
                "createprefab" => CreatePrefab(parameters),
                "instantiateprefab" => InstantiatePrefab(parameters),
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

                var type = GetComponentType(componentType);
                if (type == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Component type not found: {componentType}"
                    };
                }

                return ExecuteOnGameObject(path, gameObject =>
                {
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
                });
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
        /// Modifies properties of a component on a GameObject or prefab asset.
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

                // Check if path is a prefab asset
                if (path.EndsWith(".prefab"))
                {
                    return ModifyPrefabAssetComponent(path, componentType, index, properties);
                }

                // Handle scene GameObject (existing behavior)
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
        /// Modifies properties of a component on a prefab asset using PrefabUtility workflow.
        /// </summary>
        private JObject ModifyPrefabAssetComponent(string prefabPath, string componentType, int index, JObject properties)
        {
            try
            {
                // Validate prefab asset exists
                if (!AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Prefab asset not found at path: {prefabPath}"
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

                // Load prefab contents for direct modification
                var contentsRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                
                try
                {
                    var components = contentsRoot.GetComponents(type);
                    if (index >= components.Length)
                    {
                        return new JObject
                        {
                            ["success"] = false,
                            ["error"] = $"Component index {index} out of range (found {components.Length} components)"
                        };
                    }

                    var component = components[index];
                    var modifiedProperties = new JArray();
                    
                    foreach (var prop in properties)
                    {
                        if (SetComponentProperty(component, prop.Key, prop.Value))
                        {
                            modifiedProperties.Add(prop.Key);
                        }
                    }

                    // Save contents back to prefab asset
                    PrefabUtility.SaveAsPrefabAsset(contentsRoot, prefabPath);

                    return new JObject
                    {
                        ["success"] = true,
                        ["modifiedProperties"] = modifiedProperties,
                        ["prefabPath"] = prefabPath,
                        ["componentType"] = componentType
                    };
                }
                finally
                {
                    // Always unload prefab contents to free memory
                    PrefabUtility.UnloadPrefabContents(contentsRoot);
                }
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

        /// <summary>
        /// Creates a prefab asset from a GameObject.
        /// </summary>
        private JObject CreatePrefab(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                var prefabPath = parameters["prefabPath"]?.ToString();
                var replacePrefab = parameters["replacePrefab"]?.Value<bool>() ?? true;

                if (string.IsNullOrEmpty(path))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Path parameter is required"
                    };
                }

                if (string.IsNullOrEmpty(prefabPath))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "PrefabPath parameter is required"
                    };
                }

                // Ensure prefab path has .prefab extension
                if (!prefabPath.EndsWith(".prefab"))
                {
                    prefabPath += ".prefab";
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

                // Check if prefab already exists and replacePrefab is false
                if (!replacePrefab && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Prefab already exists at {prefabPath} and replacePrefab is false"
                    };
                }

                // Create directory if it doesn't exist
                var directory = System.IO.Path.GetDirectoryName(prefabPath);
                if (!AssetDatabase.IsValidFolder(directory))
                {
                    var folders = directory.Split('/');
                    var currentPath = folders[0]; // Should be "Assets"
                    
                    for (int i = 1; i < folders.Length; i++)
                    {
                        var newPath = currentPath + "/" + folders[i];
                        if (!AssetDatabase.IsValidFolder(newPath))
                        {
                            AssetDatabase.CreateFolder(currentPath, folders[i]);
                        }
                        currentPath = newPath;
                    }
                }

                // Save as prefab asset
                bool success;
                var savedPrefab = PrefabUtility.SaveAsPrefabAsset(gameObject, prefabPath, out success);

                if (!success || savedPrefab == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Failed to create prefab asset"
                    };
                }

                // Refresh asset database to ensure the prefab appears in the Project window
                AssetDatabase.Refresh();

                return new JObject
                {
                    ["success"] = true,
                    ["prefabPath"] = prefabPath,
                    ["prefabName"] = savedPrefab.name,
                    ["replaced"] = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null
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
        /// Instantiates a prefab asset into the scene as a GameObject.
        /// </summary>
        private JObject InstantiatePrefab(JObject parameters)
        {
            try
            {
                var prefabPath = parameters["prefabPath"]?.ToString();
                var instanceName = parameters["instanceName"]?.ToString();
                var parentPath = parameters["parentPath"]?.ToString();

                if (string.IsNullOrEmpty(prefabPath))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "PrefabPath parameter is required"
                    };
                }

                // Load the prefab asset
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Prefab not found at path: {prefabPath}"
                    };
                }

                // Instantiate the prefab (maintains all references and component values)
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                if (instance == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Failed to instantiate prefab"
                    };
                }

                // Set custom name if provided
                if (!string.IsNullOrEmpty(instanceName))
                {
                    instance.name = instanceName;
                }

                // Set parent if specified
                if (!string.IsNullOrEmpty(parentPath))
                {
                    var parentObject = FindGameObjectByPath(parentPath);
                    if (parentObject != null)
                    {
                        instance.transform.SetParent(parentObject.transform);
                    }
                    else
                    {
                        return new JObject
                        {
                            ["success"] = false,
                            ["error"] = $"Parent GameObject not found at path: {parentPath}"
                        };
                    }
                }

                return new JObject
                {
                    ["success"] = true,
                    ["instanceName"] = instance.name,
                    ["instancePath"] = GetGameObjectPath(instance),
                    ["prefabPath"] = prefabPath,
                    ["instanceId"] = instance.GetInstanceID()
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
            // Check if this is an asset path (starts with "Assets/")
            if (!string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                // Try to load as prefab asset
                var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                return prefabAsset;
            }

            // Scene object path - try active objects first
            var obj = GameObject.Find(path);
            if (obj != null) return obj;

            // Try to find inactive object in scene
            return FindInactiveObjectByPath(path);
        }

        /// <summary>
        /// Executes an action on a GameObject, handling both scene objects and prefab assets.
        /// </summary>
        private JObject ExecuteOnGameObject(string path, Func<GameObject, JObject> action)
        {
            // Check if this is an asset path (prefab)
            if (!string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return ExecuteOnPrefabAsset(path, action);
            }
            else
            {
                // Scene object - find and execute directly
                var gameObject = FindGameObjectByPath(path);
                if (gameObject == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"GameObject not found at path: {path}"
                    };
                }
                
                return action(gameObject);
            }
        }

        /// <summary>
        /// Executes an action on a prefab asset using the proper prefab workflow.
        /// </summary>
        private JObject ExecuteOnPrefabAsset(string assetPath, Func<GameObject, JObject> action)
        {
            try
            {
                // Load prefab contents for editing
                var prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);
                if (prefabRoot == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Failed to load prefab asset at path: {assetPath}"
                    };
                }

                try
                {
                    // Execute the action on the prefab root
                    var result = action(prefabRoot);
                    
                    // If the action was successful, save the prefab
                    if (result["success"]?.Value<bool>() == true)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
                        AssetDatabase.Refresh();
                    }
                    
                    return result;
                }
                finally
                {
                    // Always unload prefab contents to clean up
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = $"Error modifying prefab asset: {ex.Message}"
                };
            }
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

            // Special handling for 2D collider autoFit
            if (propertyName == "autoFit" && value.Value<bool>() == true)
            {
                var spriteRenderer = component.GetComponent<SpriteRenderer>();
                if (spriteRenderer?.sprite != null)
                {
                    var bounds = CalculateSpriteBounds(spriteRenderer.sprite);
                    
                    if (component is BoxCollider2D boxCollider)
                    {
                        boxCollider.size = bounds.size;
                        boxCollider.offset = bounds.center;
                        return true;
                    }
                    else if (component is CircleCollider2D circleCollider)
                    {
                        // Use the larger dimension as radius for best fit
                        float radius = Mathf.Max(bounds.size.x, bounds.size.y) * 0.5f;
                        circleCollider.radius = radius;
                        circleCollider.offset = bounds.center;
                        return true;
                    }
                    else if (component is CapsuleCollider2D capsuleCollider)
                    {
                        capsuleCollider.size = bounds.size;
                        capsuleCollider.offset = bounds.center;
                        // Set direction based on which dimension is larger
                        capsuleCollider.direction = bounds.size.x > bounds.size.y ? 
                            CapsuleDirection2D.Horizontal : CapsuleDirection2D.Vertical;
                        return true;
                    }
                    else if (component is PolygonCollider2D polygonCollider)
                    {
                        var points = GeneratePolygonPoints(spriteRenderer.sprite);
                        if (points != null && points.Length > 0)
                        {
                            polygonCollider.points = points;
                            return true;
                        }
                    }
                    else if (component is EdgeCollider2D edgeCollider)
                    {
                        var points = GenerateEdgePoints(spriteRenderer.sprite);
                        if (points != null && points.Length > 0)
                        {
                            edgeCollider.points = points;
                            return true;
                        }
                    }
                }
                else
                {
                    // No sprite found - could return error, but we'll just skip for now
                    return false;
                }
            }

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

            // Handle Sprite asset loading
            if (targetType == typeof(Sprite) && value.Type == JTokenType.String)
            {
                string spritePath = value.ToString();
                if (!string.IsNullOrEmpty(spritePath))
                {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                    if (sprite != null)
                    {
                        return sprite;
                    }
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
        /// Calculates tight-fitting bounds for a sprite based on non-transparent pixels.
        /// </summary>
        private Bounds CalculateSpriteBounds(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
            {
                return new Bounds(Vector3.zero, Vector3.one);
            }

            // If texture is not readable, try to make it temporarily readable
            var originalTexture = sprite.texture;
            bool wasReadable = originalTexture.isReadable;
            
            if (!wasReadable)
            {
                // Try to get the texture importer and make it readable temporarily
                string assetPath = AssetDatabase.GetAssetPath(originalTexture);
                var textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                
                if (textureImporter != null)
                {
                    textureImporter.isReadable = true;
                    textureImporter.SaveAndReimport();
                }
                else
                {
                    // Can't make readable, fall back to sprite bounds
                    return sprite.bounds;
                }
            }

            try
            {
                // Get pixel data from the sprite's texture region
                Color[] pixels = sprite.texture.GetPixels(
                    (int)sprite.rect.x, (int)sprite.rect.y,
                    (int)sprite.rect.width, (int)sprite.rect.height
                );

                int width = (int)sprite.rect.width;
                int height = (int)sprite.rect.height;

                // Find bounds of non-transparent pixels
                int minX = width, maxX = -1;
                int minY = height, maxY = -1;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        // Check if pixel has some opacity (not fully transparent)
                        if (pixels[y * width + x].a > 0.01f)
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    }
                }

                // If no non-transparent pixels found, use original bounds
                if (maxX < minX || maxY < minY)
                {
                    return sprite.bounds;
                }

                // Convert pixel coordinates to Unity units
                float pixelsPerUnit = sprite.pixelsPerUnit;
                
                Vector2 size = new Vector2(
                    (maxX - minX + 1) / pixelsPerUnit,
                    (maxY - minY + 1) / pixelsPerUnit
                );

                // Calculate center offset relative to sprite center
                Vector2 spriteCenter = new Vector2(width * 0.5f, height * 0.5f);
                Vector2 boundsCenter = new Vector2(
                    (minX + maxX) * 0.5f,
                    (minY + maxY) * 0.5f
                );
                
                Vector2 centerOffset = new Vector2(
                    (boundsCenter.x - spriteCenter.x) / pixelsPerUnit,
                    (boundsCenter.y - spriteCenter.y) / pixelsPerUnit
                );

                return new Bounds(centerOffset, size);
            }
            catch (System.Exception)
            {
                // If any error occurs, fall back to original sprite bounds
                return sprite.bounds;
            }
            finally
            {
                // Restore original texture settings if we changed them
                if (!wasReadable && originalTexture != null)
                {
                    string assetPath = AssetDatabase.GetAssetPath(originalTexture);
                    var textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    
                    if (textureImporter != null)
                    {
                        textureImporter.isReadable = false;
                        textureImporter.SaveAndReimport();
                    }
                }
            }
        }

        /// <summary>
        /// Generates polygon points for a PolygonCollider2D based on sprite transparency.
        /// </summary>
        private Vector2[] GeneratePolygonPoints(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
            {
                return null;
            }

            // Make texture readable if needed
            var originalTexture = sprite.texture;
            bool wasReadable = originalTexture.isReadable;
            
            if (!wasReadable)
            {
                string assetPath = AssetDatabase.GetAssetPath(originalTexture);
                var textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                
                if (textureImporter != null)
                {
                    textureImporter.isReadable = true;
                    textureImporter.SaveAndReimport();
                }
                else
                {
                    return null;
                }
            }

            try
            {
                // Get pixel data
                Color[] pixels = sprite.texture.GetPixels(
                    (int)sprite.rect.x, (int)sprite.rect.y,
                    (int)sprite.rect.width, (int)sprite.rect.height
                );

                int width = (int)sprite.rect.width;
                int height = (int)sprite.rect.height;
                float pixelsPerUnit = sprite.pixelsPerUnit;

                // Create a simplified polygon by sampling around the edges
                var points = new List<Vector2>();
                
                // Sample points around the perimeter, checking for non-transparent pixels
                int samples = Mathf.Min(32, Mathf.Max(8, width + height) / 4); // Adaptive sampling
                
                for (int i = 0; i < samples; i++)
                {
                    float angle = i * 2f * Mathf.PI / samples;
                    
                    // Cast a ray from center outward to find edge
                    Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
                    Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    
                    // Find the furthest non-transparent pixel in this direction
                    float maxDistance = Mathf.Min(width, height) * 0.6f; // Reasonable search distance
                    Vector2? edgePoint = null;
                    
                    for (float distance = 1f; distance <= maxDistance; distance += 1f)
                    {
                        Vector2 testPoint = center + direction * distance;
                        int x = Mathf.RoundToInt(testPoint.x);
                        int y = Mathf.RoundToInt(testPoint.y);
                        
                        if (x >= 0 && x < width && y >= 0 && y < height)
                        {
                            if (pixels[y * width + x].a > 0.01f)
                            {
                                edgePoint = testPoint;
                            }
                        }
                        else
                        {
                            break; // Out of bounds
                        }
                    }
                    
                    if (edgePoint.HasValue)
                    {
                        // Convert to Unity units relative to sprite center
                        Vector2 unityPoint = new Vector2(
                            (edgePoint.Value.x - center.x) / pixelsPerUnit,
                            (edgePoint.Value.y - center.y) / pixelsPerUnit
                        );
                        points.Add(unityPoint);
                    }
                }

                return points.Count >= 3 ? points.ToArray() : null;
            }
            catch (System.Exception)
            {
                return null;
            }
            finally
            {
                // Restore original texture settings if we changed them
                if (!wasReadable && originalTexture != null)
                {
                    string assetPath = AssetDatabase.GetAssetPath(originalTexture);
                    var textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    
                    if (textureImporter != null)
                    {
                        textureImporter.isReadable = false;
                        textureImporter.SaveAndReimport();
                    }
                }
            }
        }

        /// <summary>
        /// Generates edge points for an EdgeCollider2D based on sprite outline.
        /// </summary>
        private Vector2[] GenerateEdgePoints(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
            {
                return null;
            }

            // Make texture readable if needed
            var originalTexture = sprite.texture;
            bool wasReadable = originalTexture.isReadable;
            
            if (!wasReadable)
            {
                string assetPath = AssetDatabase.GetAssetPath(originalTexture);
                var textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                
                if (textureImporter != null)
                {
                    textureImporter.isReadable = true;
                    textureImporter.SaveAndReimport();
                }
                else
                {
                    return null;
                }
            }

            try
            {
                // Get pixel data
                Color[] pixels = sprite.texture.GetPixels(
                    (int)sprite.rect.x, (int)sprite.rect.y,
                    (int)sprite.rect.width, (int)sprite.rect.height
                );

                int width = (int)sprite.rect.width;
                int height = (int)sprite.rect.height;
                float pixelsPerUnit = sprite.pixelsPerUnit;

                // Find the outline by tracing the edge of non-transparent pixels
                var edgePoints = new List<Vector2>();
                
                // Simple edge detection: find pixels that are non-transparent but adjacent to transparent pixels
                for (int y = 1; y < height - 1; y++)
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        int index = y * width + x;
                        
                        // If this pixel is non-transparent
                        if (pixels[index].a > 0.01f)
                        {
                            // Check if any adjacent pixel is transparent (edge detection)
                            bool isEdge = false;
                            
                            // Check 4-connected neighbors
                            if (pixels[(y - 1) * width + x].a <= 0.01f || // Above
                                pixels[(y + 1) * width + x].a <= 0.01f || // Below
                                pixels[y * width + (x - 1)].a <= 0.01f || // Left
                                pixels[y * width + (x + 1)].a <= 0.01f)   // Right
                            {
                                isEdge = true;
                            }
                            
                            if (isEdge)
                            {
                                // Convert to Unity units relative to sprite center
                                Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
                                Vector2 unityPoint = new Vector2(
                                    (x - center.x) / pixelsPerUnit,
                                    (y - center.y) / pixelsPerUnit
                                );
                                edgePoints.Add(unityPoint);
                            }
                        }
                    }
                }

                // Sort points to create a more coherent edge line (simple approach)
                if (edgePoints.Count > 2)
                {
                    // Sort by angle from center to create a roughly circular path
                    edgePoints.Sort((a, b) => 
                        Mathf.Atan2(a.y, a.x).CompareTo(Mathf.Atan2(b.y, b.x))
                    );
                    
                    // Reduce density for performance (keep every nth point)
                    int step = Mathf.Max(1, edgePoints.Count / 24); // Max 24 points
                    var reducedPoints = new List<Vector2>();
                    for (int i = 0; i < edgePoints.Count; i += step)
                    {
                        reducedPoints.Add(edgePoints[i]);
                    }
                    edgePoints = reducedPoints;
                }

                return edgePoints.Count >= 2 ? edgePoints.ToArray() : null;
            }
            catch (System.Exception)
            {
                return null;
            }
            finally
            {
                // Restore original texture settings if we changed them
                if (!wasReadable && originalTexture != null)
                {
                    string assetPath = AssetDatabase.GetAssetPath(originalTexture);
                    var textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    
                    if (textureImporter != null)
                    {
                        textureImporter.isReadable = false;
                        textureImporter.SaveAndReimport();
                    }
                }
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