using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityMCP.Editor.Core;

namespace UnityMCP.Editor.Handlers
{
    /// <summary>
    /// Command handler for manipulating the Unity scene hierarchy.
    /// </summary>
    internal sealed class HierarchyCommandHandler : IMcpCommandHandler
    {
        /// <summary>
        /// Gets the command prefix for this handler.
        /// </summary>
        public string CommandPrefix => "hierarchy";

        /// <summary>
        /// Gets the description of this command handler.
        /// </summary>
        public string Description => "Manipulate Unity scene hierarchy (create, read, update, delete GameObjects)";

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
                "get" => GetHierarchy(parameters),
                "create" => CreateGameObject(parameters),
                "delete" => DeleteGameObject(parameters),
                "rename" => RenameGameObject(parameters),
                "setparent" => SetParent(parameters),
                "getchildren" => GetChildren(parameters),
                "find" => FindGameObjects(parameters),
                "setactive" => SetActive(parameters),
                "getcomponents" => GetComponents(parameters),
                "duplicate" => DuplicateGameObject(parameters),
                _ => new JObject { ["success"] = false, ["error"] = $"Unknown action: {action}" }
            };
        }

        /// <summary>
        /// Gets the hierarchy structure.
        /// </summary>
        private JObject GetHierarchy(JObject parameters)
        {
            try
            {
                var rootPath = parameters["rootPath"]?.ToString();
                var depth = parameters["depth"]?.Value<int>() ?? -1;
                var includeInactive = parameters["includeInactive"]?.Value<bool>() ?? true;

                GameObject rootObject = null;
                if (!string.IsNullOrEmpty(rootPath))
                {
                    rootObject = GameObject.Find(rootPath);
                    if (rootObject == null && includeInactive)
                    {
                        rootObject = FindInactiveObjectByPath(rootPath);
                    }
                    
                    if (rootObject == null)
                    {
                        return new JObject 
                        { 
                            ["success"] = false, 
                            ["error"] = $"GameObject not found at path: {rootPath}" 
                        };
                    }
                }

                var hierarchy = new JArray();
                
                if (rootObject != null)
                {
                    hierarchy.Add(BuildHierarchyNode(rootObject, depth, 0, includeInactive));
                }
                else
                {
                    // Get all root objects in active scene
                    var scene = SceneManager.GetActiveScene();
                    var rootObjects = scene.GetRootGameObjects();
                    
                    foreach (var obj in rootObjects)
                    {
                        if (!includeInactive && !obj.activeInHierarchy) continue;
                        hierarchy.Add(BuildHierarchyNode(obj, depth, 0, includeInactive));
                    }
                }

                return new JObject
                {
                    ["success"] = true,
                    ["hierarchy"] = hierarchy,
                    ["scene"] = SceneManager.GetActiveScene().name
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
        /// Creates a new GameObject.
        /// </summary>
        private JObject CreateGameObject(JObject parameters)
        {
            try
            {
                var name = parameters["name"]?.ToString() ?? "GameObject";
                var parentPath = parameters["parentPath"]?.ToString();
                var components = parameters["components"] as JArray;

                var gameObject = new GameObject(name);
                
                // Set parent if specified
                if (!string.IsNullOrEmpty(parentPath))
                {
                    var parent = GameObject.Find(parentPath) ?? FindInactiveObjectByPath(parentPath);
                    if (parent != null)
                    {
                        gameObject.transform.SetParent(parent.transform);
                    }
                }

                // Add components if specified
                if (components != null)
                {
                    foreach (var componentName in components)
                    {
                        var typeName = componentName.ToString();
                        var type = GetComponentType(typeName);
                        if (type != null)
                        {
                            gameObject.AddComponent(type);
                        }
                    }
                }

                Undo.RegisterCreatedObjectUndo(gameObject, $"Create {name}");
                
                return new JObject
                {
                    ["success"] = true,
                    ["gameObject"] = new JObject
                    {
                        ["name"] = gameObject.name,
                        ["path"] = GetGameObjectPath(gameObject),
                        ["instanceId"] = gameObject.GetInstanceID()
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
        /// Deletes GameObjects.
        /// </summary>
        private JObject DeleteGameObject(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                var paths = parameters["paths"] as JArray;
                
                var objectsToDelete = new List<GameObject>();
                
                if (!string.IsNullOrEmpty(path))
                {
                    var obj = GameObject.Find(path) ?? FindInactiveObjectByPath(path);
                    if (obj != null) objectsToDelete.Add(obj);
                }
                
                if (paths != null)
                {
                    foreach (var p in paths)
                    {
                        var obj = GameObject.Find(p.ToString()) ?? FindInactiveObjectByPath(p.ToString());
                        if (obj != null) objectsToDelete.Add(obj);
                    }
                }

                if (objectsToDelete.Count == 0)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "No GameObjects found to delete"
                    };
                }

                foreach (var obj in objectsToDelete)
                {
                    Undo.DestroyObjectImmediate(obj);
                }

                return new JObject
                {
                    ["success"] = true,
                    ["deletedCount"] = objectsToDelete.Count
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
        /// Renames a GameObject.
        /// </summary>
        private JObject RenameGameObject(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                var newName = parameters["newName"]?.ToString();

                if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(newName))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Path and newName parameters are required"
                    };
                }

                var gameObject = GameObject.Find(path) ?? FindInactiveObjectByPath(path);
                if (gameObject == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"GameObject not found at path: {path}"
                    };
                }

                Undo.RecordObject(gameObject, $"Rename {gameObject.name}");
                gameObject.name = newName;

                return new JObject
                {
                    ["success"] = true,
                    ["gameObject"] = new JObject
                    {
                        ["name"] = gameObject.name,
                        ["path"] = GetGameObjectPath(gameObject),
                        ["instanceId"] = gameObject.GetInstanceID()
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
        /// Changes the parent of a GameObject.
        /// </summary>
        private JObject SetParent(JObject parameters)
        {
            try
            {
                var childPath = parameters["childPath"]?.ToString();
                var parentPath = parameters["parentPath"]?.ToString();
                var worldPositionStays = parameters["worldPositionStays"]?.Value<bool>() ?? true;

                if (string.IsNullOrEmpty(childPath))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "childPath parameter is required"
                    };
                }

                var child = GameObject.Find(childPath) ?? FindInactiveObjectByPath(childPath);
                if (child == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Child GameObject not found at path: {childPath}"
                    };
                }

                Transform newParent = null;
                if (!string.IsNullOrEmpty(parentPath))
                {
                    var parent = GameObject.Find(parentPath) ?? FindInactiveObjectByPath(parentPath);
                    if (parent == null)
                    {
                        return new JObject
                        {
                            ["success"] = false,
                            ["error"] = $"Parent GameObject not found at path: {parentPath}"
                        };
                    }
                    newParent = parent.transform;
                }

                Undo.SetTransformParent(child.transform, newParent, $"Set parent of {child.name}");
                child.transform.SetParent(newParent, worldPositionStays);

                return new JObject
                {
                    ["success"] = true,
                    ["gameObject"] = new JObject
                    {
                        ["name"] = child.name,
                        ["path"] = GetGameObjectPath(child),
                        ["parentPath"] = newParent != null ? GetGameObjectPath(newParent.gameObject) : null
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
        /// Gets the children of a GameObject.
        /// </summary>
        private JObject GetChildren(JObject parameters)
        {
            try
            {
                var parentPath = parameters["parentPath"]?.ToString();
                var includeInactive = parameters["includeInactive"]?.Value<bool>() ?? true;

                Transform parent = null;
                if (!string.IsNullOrEmpty(parentPath))
                {
                    var parentObj = GameObject.Find(parentPath) ?? FindInactiveObjectByPath(parentPath);
                    if (parentObj == null)
                    {
                        return new JObject
                        {
                            ["success"] = false,
                            ["error"] = $"Parent GameObject not found at path: {parentPath}"
                        };
                    }
                    parent = parentObj.transform;
                }

                var children = new JArray();
                
                if (parent != null)
                {
                    foreach (Transform child in parent)
                    {
                        if (!includeInactive && !child.gameObject.activeInHierarchy) continue;
                        
                        children.Add(new JObject
                        {
                            ["name"] = child.name,
                            ["path"] = GetGameObjectPath(child.gameObject),
                            ["active"] = child.gameObject.activeSelf,
                            ["instanceId"] = child.gameObject.GetInstanceID()
                        });
                    }
                }
                else
                {
                    // Get root objects
                    var scene = SceneManager.GetActiveScene();
                    var rootObjects = scene.GetRootGameObjects();
                    
                    foreach (var obj in rootObjects)
                    {
                        if (!includeInactive && !obj.activeInHierarchy) continue;
                        
                        children.Add(new JObject
                        {
                            ["name"] = obj.name,
                            ["path"] = GetGameObjectPath(obj),
                            ["active"] = obj.activeSelf,
                            ["instanceId"] = obj.GetInstanceID()
                        });
                    }
                }

                return new JObject
                {
                    ["success"] = true,
                    ["children"] = children,
                    ["count"] = children.Count
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
        /// Finds GameObjects by name, tag, or component.
        /// </summary>
        private JObject FindGameObjects(JObject parameters)
        {
            try
            {
                var query = parameters["query"]?.ToString();
                var searchType = parameters["searchType"]?.ToString() ?? "name";
                var limit = parameters["limit"]?.Value<int>() ?? 100;

                if (string.IsNullOrEmpty(query))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Query parameter is required"
                    };
                }

                var results = new List<GameObject>();

                switch (searchType.ToLower())
                {
                    case "name":
                        var allObjects = UnityEngine.Resources.FindObjectsOfTypeAll<GameObject>();
                        results = allObjects.Where(obj => 
                            obj.name.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                            !IsEditorOnly(obj)).Take(limit).ToList();
                        break;
                        
                    case "tag":
                        var tagged = GameObject.FindGameObjectsWithTag(query);
                        results = tagged.Take(limit).ToList();
                        break;
                        
                    case "component":
                        var type = GetComponentType(query);
                        if (type != null)
                        {
                            var components = UnityEngine.Resources.FindObjectsOfTypeAll(type);
                            results = components.OfType<Component>()
                                .Select(c => c.gameObject)
                                .Distinct()
                                .Where(obj => !IsEditorOnly(obj))
                                .Take(limit)
                                .ToList();
                        }
                        break;
                }

                var resultArray = new JArray();
                foreach (var obj in results)
                {
                    resultArray.Add(new JObject
                    {
                        ["name"] = obj.name,
                        ["path"] = GetGameObjectPath(obj),
                        ["active"] = obj.activeSelf,
                        ["instanceId"] = obj.GetInstanceID()
                    });
                }

                return new JObject
                {
                    ["success"] = true,
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
        /// Sets the active state of a GameObject.
        /// </summary>
        private JObject SetActive(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                var active = parameters["active"]?.Value<bool>();

                if (string.IsNullOrEmpty(path) || active == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Path and active parameters are required"
                    };
                }

                var gameObject = GameObject.Find(path) ?? FindInactiveObjectByPath(path);
                if (gameObject == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"GameObject not found at path: {path}"
                    };
                }

                Undo.RecordObject(gameObject, $"Set active {gameObject.name}");
                gameObject.SetActive(active.Value);

                return new JObject
                {
                    ["success"] = true,
                    ["gameObject"] = new JObject
                    {
                        ["name"] = gameObject.name,
                        ["path"] = GetGameObjectPath(gameObject),
                        ["active"] = gameObject.activeSelf
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
        /// Gets all components on a GameObject.
        /// </summary>
        private JObject GetComponents(JObject parameters)
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

                var gameObject = GameObject.Find(path) ?? FindInactiveObjectByPath(path);
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
                        ["enabled"] = component is Behaviour behaviour ? behaviour.enabled : true
                    };

                    // Add basic properties for common components
                    if (component is Transform transform)
                    {
                        componentInfo["position"] = JToken.FromObject(transform.position);
                        componentInfo["rotation"] = JToken.FromObject(transform.rotation.eulerAngles);
                        componentInfo["scale"] = JToken.FromObject(transform.localScale);
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
        /// Duplicates a GameObject.
        /// </summary>
        private JObject DuplicateGameObject(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                var newName = parameters["newName"]?.ToString();
                var parentPath = parameters["parentPath"]?.ToString();

                if (string.IsNullOrEmpty(path))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Path parameter is required"
                    };
                }

                var original = GameObject.Find(path) ?? FindInactiveObjectByPath(path);
                if (original == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"GameObject not found at path: {path}"
                    };
                }

                var duplicate = UnityEngine.Object.Instantiate(original);
                
                if (!string.IsNullOrEmpty(newName))
                {
                    duplicate.name = newName;
                }
                else
                {
                    duplicate.name = original.name + " (Copy)";
                }

                if (!string.IsNullOrEmpty(parentPath))
                {
                    var parent = GameObject.Find(parentPath) ?? FindInactiveObjectByPath(parentPath);
                    if (parent != null)
                    {
                        duplicate.transform.SetParent(parent.transform);
                    }
                }
                else if (original.transform.parent != null)
                {
                    duplicate.transform.SetParent(original.transform.parent);
                }

                Undo.RegisterCreatedObjectUndo(duplicate, $"Duplicate {original.name}");

                return new JObject
                {
                    ["success"] = true,
                    ["original"] = new JObject
                    {
                        ["name"] = original.name,
                        ["path"] = GetGameObjectPath(original)
                    },
                    ["duplicate"] = new JObject
                    {
                        ["name"] = duplicate.name,
                        ["path"] = GetGameObjectPath(duplicate),
                        ["instanceId"] = duplicate.GetInstanceID()
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

        // Helper methods

        /// <summary>
        /// Builds a hierarchy node for JSON representation.
        /// </summary>
        private JObject BuildHierarchyNode(GameObject obj, int maxDepth, int currentDepth, bool includeInactive)
        {
            var node = new JObject
            {
                ["name"] = obj.name,
                ["path"] = GetGameObjectPath(obj),
                ["active"] = obj.activeSelf,
                ["instanceId"] = obj.GetInstanceID()
            };

            if (maxDepth == -1 || currentDepth < maxDepth)
            {
                var children = new JArray();
                foreach (Transform child in obj.transform)
                {
                    if (!includeInactive && !child.gameObject.activeInHierarchy) continue;
                    children.Add(BuildHierarchyNode(child.gameObject, maxDepth, currentDepth + 1, includeInactive));
                }
                
                if (children.Count > 0)
                {
                    node["children"] = children;
                }
            }

            return node;
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
        /// Finds an inactive GameObject by path.
        /// </summary>
        private GameObject FindInactiveObjectByPath(string path)
        {
            var parts = path.Split('/');
            if (parts.Length == 0) return null;

            // Find root objects including inactive
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

            // Navigate through the path
            for (int i = 1; i < parts.Length; i++)
            {
                Transform child = current.transform.Find(parts[i]);
                if (child == null) return null;
                current = child.gameObject;
            }

            return current;
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

            // Try all loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null && typeof(Component).IsAssignableFrom(type))
                    return type;
            }

            return null;
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