using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor.Core;

namespace UnityMCP.Editor.Handlers
{
    /// <summary>
    /// Command handler for Unity Project window operations (CRUD for assets and project structure).
    /// </summary>
    internal sealed class ProjectCommandHandler : IMcpCommandHandler
    {
        /// <summary>
        /// Gets the command prefix for this handler.
        /// </summary>
        public string CommandPrefix => "project";

        /// <summary>
        /// Gets the description of this command handler.
        /// </summary>
        public string Description => "Manage Unity project assets and folder structure (create, read, update, delete)";

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
                // Read operations
                "search" => SearchAssets(parameters),
                "getinfo" => GetAssetInfo(parameters),
                "getdependencies" => GetAssetDependencies(parameters),
                "browse" => BrowseProjectStructure(parameters),
                
                // Create operations
                "createfolder" => CreateFolder(parameters),
                "createasset" => CreateAsset(parameters),
                "duplicate" => DuplicateAsset(parameters),
                "importasset" => ImportAsset(parameters),
                
                // Update operations
                "rename" => RenameAsset(parameters),
                "move" => MoveAsset(parameters),
                "setlabels" => SetAssetLabels(parameters),
                "refresh" => RefreshAssets(parameters),
                
                // Delete operations
                "delete" => DeleteAsset(parameters),
                "deleteemptyfolders" => DeleteEmptyFolders(parameters),
                
                _ => new JObject { ["success"] = false, ["error"] = $"Unknown action: {action}. See project tools documentation for available actions." }
            };
        }

        #region Read Operations

        /// <summary>
        /// Searches for assets using the provided query.
        /// </summary>
        private JObject SearchAssets(JObject parameters)
        {
            try
            {
                var query = parameters["query"]?.ToString();
                if (string.IsNullOrWhiteSpace(query))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Query parameter is required"
                    };
                }

                var searchType = parameters["searchType"]?.ToString() ?? "all";
                var assetType = parameters["assetType"]?.ToString();
                var folder = parameters["folder"]?.ToString();
                var exact = parameters["exact"]?.Value<bool>() ?? false;
                var limit = parameters["limit"]?.Value<int>() ?? 100;
                var includePackages = parameters["includePackages"]?.Value<bool>() ?? false;

                // Check if query looks like a folder pattern first
                if (IsLikelyFolderPattern(query))
                {
                    var folderResults = SearchAssetsInFolderPattern(query, limit, includePackages);
                    if (folderResults["success"].Value<bool>())
                    {
                        return folderResults;
                    }
                }

                // Build search filter based on search type
                string searchFilter = "";
                
                switch (searchType.ToLower())
                {
                    case "name":
                        searchFilter = query;
                        break;
                    case "type":
                        searchFilter = $"t:{query}";
                        break;
                    case "content":
                        // For content search, we'll search broadly and filter later
                        searchFilter = query;
                        break;
                    case "all":
                    default:
                        searchFilter = query;
                        break;
                }

                // Add asset type filter if specified
                if (!string.IsNullOrEmpty(assetType))
                {
                    searchFilter = $"{searchFilter} t:{assetType}";
                }

                // Add folder filter if specified
                if (!string.IsNullOrEmpty(folder))
                {
                    // Ensure folder path is properly formatted
                    folder = folder.Replace("\\", "/");
                    if (!folder.StartsWith("Assets/") && folder != "Assets")
                    {
                        folder = "Assets/" + folder.TrimStart('/');
                    }
                }

                // Add package filter
                if (!includePackages)
                {
                    searchFilter = $"{searchFilter} -packages";
                }

                var results = AssetDatabase.FindAssets(searchFilter.Trim(), 
                    !string.IsNullOrEmpty(folder) ? new[] { folder } : null);

                // Post-process results for exact name matching if requested
                if (searchType.ToLower() == "name" && exact)
                {
                    var exactResults = new List<string>();
                    foreach (var guid in results)
                    {
                        var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                        var assetName = Path.GetFileNameWithoutExtension(assetPath);
                        if (string.Equals(assetName, query, StringComparison.OrdinalIgnoreCase))
                        {
                            exactResults.Add(guid);
                        }
                    }
                    results = exactResults.ToArray();
                }

                return FormatSearchResults(results, limit, includePackages);
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
        /// Gets detailed information about an asset.
        /// </summary>
        private JObject GetAssetInfo(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                var guid = parameters["guid"]?.ToString();

                string assetPath;
                if (!string.IsNullOrEmpty(guid))
                {
                    assetPath = AssetDatabase.GUIDToAssetPath(guid);
                }
                else if (!string.IsNullOrEmpty(path))
                {
                    assetPath = path;
                }
                else
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Either path or guid parameter is required"
                    };
                }

                if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Asset not found: {assetPath}"
                    };
                }

                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (asset == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Could not load asset: {assetPath}"
                    };
                }

                var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                var importer = AssetImporter.GetAtPath(assetPath);
                var labels = AssetDatabase.GetLabels(asset);

                var info = new JObject
                {
                    ["success"] = true,
                    ["asset"] = new JObject
                    {
                        ["name"] = Path.GetFileNameWithoutExtension(assetPath),
                        ["path"] = assetPath,
                        ["guid"] = assetGuid,
                        ["type"] = asset.GetType().Name,
                        ["extension"] = Path.GetExtension(assetPath),
                        ["size"] = new FileInfo(assetPath).Length,
                        ["labels"] = new JArray(labels),
                        ["lastModified"] = File.GetLastWriteTime(assetPath).ToString("yyyy-MM-dd HH:mm:ss"),
                        ["isFolder"] = AssetDatabase.IsValidFolder(assetPath)
                    }
                };

                if (importer != null)
                {
                    info["asset"]["importerType"] = importer.GetType().Name;
                    info["asset"]["assetBundleName"] = importer.assetBundleName;
                    info["asset"]["assetBundleVariant"] = importer.assetBundleVariant;
                }

                return info;
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
        /// Gets asset dependencies.
        /// </summary>
        private JObject GetAssetDependencies(JObject parameters)
        {
            try
            {
                var path = parameters["path"]?.ToString();
                var recursive = parameters["recursive"]?.Value<bool>() ?? false;

                if (string.IsNullOrEmpty(path))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Path parameter is required"
                    };
                }

                var dependencies = AssetDatabase.GetDependencies(path, recursive);
                var dependenciesArray = new JArray();

                foreach (var dep in dependencies)
                {
                    if (dep == path) continue; // Skip self-reference

                    var depAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(dep);
                    if (depAsset != null)
                    {
                        dependenciesArray.Add(new JObject
                        {
                            ["path"] = dep,
                            ["name"] = Path.GetFileNameWithoutExtension(dep),
                            ["type"] = depAsset.GetType().Name,
                            ["guid"] = AssetDatabase.AssetPathToGUID(dep)
                        });
                    }
                }

                return new JObject
                {
                    ["success"] = true,
                    ["asset"] = path,
                    ["dependencies"] = dependenciesArray,
                    ["count"] = dependenciesArray.Count,
                    ["recursive"] = recursive
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
        /// Browses the project structure starting from a given path.
        /// </summary>
        private JObject BrowseProjectStructure(JObject parameters)
        {
            try
            {
                var rootPath = parameters["rootPath"]?.ToString() ?? "Assets";
                var depth = parameters["depth"]?.Value<int>() ?? 1;
                var includeFiles = parameters["includeFiles"]?.Value<bool>() ?? true;
                var fileTypes = parameters["fileTypes"] as JArray;

                if (!AssetDatabase.IsValidFolder(rootPath))
                {
                    // Try smart path resolution
                    var resolvedPaths = ResolveSmartPath(rootPath);
                    
                    if (resolvedPaths.Count == 0)
                    {
                        return new JObject
                        {
                            ["success"] = false,
                            ["error"] = $"No folders found matching '{rootPath}'. Try a different search pattern."
                        };
                    }
                    
                    if (resolvedPaths.Count > 1)
                    {
                        return CreateMultipleMatchesResponse(resolvedPaths, rootPath);
                    }
                    
                    // Use the single resolved path
                    rootPath = resolvedPaths[0];
                }

                var structure = BuildFolderStructure(rootPath, depth, 0, includeFiles, fileTypes);

                return new JObject
                {
                    ["success"] = true,
                    ["structure"] = structure,
                    ["rootPath"] = rootPath
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

        #region Create Operations

        /// <summary>
        /// Creates a new folder in the project.
        /// </summary>
        private JObject CreateFolder(JObject parameters)
        {
            try
            {
                var parentPath = parameters["parentPath"]?.ToString() ?? "Assets";
                var folderName = parameters["folderName"]?.ToString();

                if (string.IsNullOrEmpty(folderName))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "folderName parameter is required"
                    };
                }

                if (!AssetDatabase.IsValidFolder(parentPath))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Parent path is not a valid folder: {parentPath}"
                    };
                }

                var guid = AssetDatabase.CreateFolder(parentPath, folderName);
                var createdPath = AssetDatabase.GUIDToAssetPath(guid);

                return new JObject
                {
                    ["success"] = true,
                    ["folder"] = new JObject
                    {
                        ["name"] = folderName,
                        ["path"] = createdPath,
                        ["guid"] = guid,
                        ["parentPath"] = parentPath
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
        /// Creates a new asset in the project.
        /// </summary>
        private JObject CreateAsset(JObject parameters)
        {
            try
            {
                var assetType = parameters["assetType"]?.ToString();
                var assetName = parameters["assetName"]?.ToString();
                var parentPath = parameters["parentPath"]?.ToString() ?? "Assets";
                var content = parameters["content"]?.ToString();

                if (string.IsNullOrEmpty(assetType) || string.IsNullOrEmpty(assetName))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "assetType and assetName parameters are required"
                    };
                }

                var fullPath = Path.Combine(parentPath, assetName).Replace('\\', '/');

                switch (assetType.ToLower())
                {
                    case "script":
                    case "csharp":
                        return CreateScript(fullPath, content);
                    
                    case "material":
                        return CreateMaterial(fullPath);
                    
                    case "animatorcontroller":
                        return CreateAnimatorController(fullPath);
                    
                    case "scriptableobject":
                        var soTypeName = parameters["scriptableObjectType"]?.ToString();
                        return CreateScriptableObject(fullPath, soTypeName);
                    
                    default:
                        return new JObject
                        {
                            ["success"] = false,
                            ["error"] = $"Unsupported asset type: {assetType}. Supported types: script, material, animatorcontroller, scriptableobject"
                        };
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
        /// Duplicates an existing asset.
        /// </summary>
        private JObject DuplicateAsset(JObject parameters)
        {
            try
            {
                var sourcePath = parameters["sourcePath"]?.ToString();
                var newName = parameters["newName"]?.ToString();

                if (string.IsNullOrEmpty(sourcePath))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "sourcePath parameter is required"
                    };
                }

                if (!File.Exists(sourcePath))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Source asset not found: {sourcePath}"
                    };
                }

                var targetPath = sourcePath;
                if (!string.IsNullOrEmpty(newName))
                {
                    var directory = Path.GetDirectoryName(sourcePath);
                    var extension = Path.GetExtension(sourcePath);
                    targetPath = Path.Combine(directory, newName + extension).Replace('\\', '/');
                }
                else
                {
                    var directory = Path.GetDirectoryName(sourcePath);
                    var nameWithoutExt = Path.GetFileNameWithoutExtension(sourcePath);
                    var extension = Path.GetExtension(sourcePath);
                    targetPath = Path.Combine(directory, $"{nameWithoutExt} Copy{extension}").Replace('\\', '/');
                }

                if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Failed to duplicate asset"
                    };
                }

                var guid = AssetDatabase.AssetPathToGUID(targetPath);

                return new JObject
                {
                    ["success"] = true,
                    ["original"] = sourcePath,
                    ["duplicate"] = new JObject
                    {
                        ["name"] = Path.GetFileNameWithoutExtension(targetPath),
                        ["path"] = targetPath,
                        ["guid"] = guid
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
        /// Imports an external asset into the project.
        /// </summary>
        private JObject ImportAsset(JObject parameters)
        {
            try
            {
                var sourcePath = parameters["sourcePath"]?.ToString();
                var targetPath = parameters["targetPath"]?.ToString();

                if (string.IsNullOrEmpty(sourcePath))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "sourcePath parameter is required"
                    };
                }

                if (!File.Exists(sourcePath))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Source file not found: {sourcePath}"
                    };
                }

                if (string.IsNullOrEmpty(targetPath))
                {
                    var fileName = Path.GetFileName(sourcePath);
                    targetPath = Path.Combine("Assets", fileName).Replace('\\', '/');
                }

                File.Copy(sourcePath, targetPath, true);
                AssetDatabase.ImportAsset(targetPath);

                var guid = AssetDatabase.AssetPathToGUID(targetPath);

                return new JObject
                {
                    ["success"] = true,
                    ["imported"] = new JObject
                    {
                        ["name"] = Path.GetFileNameWithoutExtension(targetPath),
                        ["path"] = targetPath,
                        ["guid"] = guid,
                        ["sourcePath"] = sourcePath
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

        #endregion

        #region Update Operations

        /// <summary>
        /// Renames an asset or folder.
        /// </summary>
        private JObject RenameAsset(JObject parameters)
        {
            try
            {
                var assetPath = parameters["assetPath"]?.ToString();
                var newName = parameters["newName"]?.ToString();

                if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(newName))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "assetPath and newName parameters are required"
                    };
                }

                var errorMsg = AssetDatabase.RenameAsset(assetPath, newName);
                if (!string.IsNullOrEmpty(errorMsg))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = errorMsg
                    };
                }

                var newPath = Path.Combine(Path.GetDirectoryName(assetPath), newName + Path.GetExtension(assetPath)).Replace('\\', '/');
                var guid = AssetDatabase.AssetPathToGUID(newPath);

                return new JObject
                {
                    ["success"] = true,
                    ["asset"] = new JObject
                    {
                        ["oldPath"] = assetPath,
                        ["newPath"] = newPath,
                        ["name"] = newName,
                        ["guid"] = guid
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
        /// Moves an asset or folder to a new location.
        /// </summary>
        private JObject MoveAsset(JObject parameters)
        {
            try
            {
                var sourcePath = parameters["sourcePath"]?.ToString();
                var targetPath = parameters["targetPath"]?.ToString();

                if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(targetPath))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "sourcePath and targetPath parameters are required"
                    };
                }

                var errorMsg = AssetDatabase.MoveAsset(sourcePath, targetPath);
                if (!string.IsNullOrEmpty(errorMsg))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = errorMsg
                    };
                }

                var guid = AssetDatabase.AssetPathToGUID(targetPath);

                return new JObject
                {
                    ["success"] = true,
                    ["asset"] = new JObject
                    {
                        ["sourcePath"] = sourcePath,
                        ["targetPath"] = targetPath,
                        ["name"] = Path.GetFileNameWithoutExtension(targetPath),
                        ["guid"] = guid
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
        /// Sets labels on an asset.
        /// </summary>
        private JObject SetAssetLabels(JObject parameters)
        {
            try
            {
                var assetPath = parameters["assetPath"]?.ToString();
                var labels = parameters["labels"] as JArray;

                if (string.IsNullOrEmpty(assetPath))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "assetPath parameter is required"
                    };
                }

                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (asset == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Asset not found: {assetPath}"
                    };
                }

                var labelArray = labels?.Select(l => l.ToString()).ToArray() ?? new string[0];
                AssetDatabase.SetLabels(asset, labelArray);

                return new JObject
                {
                    ["success"] = true,
                    ["asset"] = assetPath,
                    ["labels"] = new JArray(labelArray)
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
        /// Refreshes the asset database.
        /// </summary>
        private JObject RefreshAssets(JObject parameters)
        {
            try
            {
                AssetDatabase.Refresh();
                return new JObject
                {
                    ["success"] = true,
                    ["message"] = "Asset database refreshed"
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

        #region Delete Operations

        /// <summary>
        /// Deletes an asset or folder.
        /// </summary>
        private JObject DeleteAsset(JObject parameters)
        {
            try
            {
                var assetPath = parameters["assetPath"]?.ToString();
                var checkDependencies = parameters["checkDependencies"]?.Value<bool>() ?? true;

                if (string.IsNullOrEmpty(assetPath))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "assetPath parameter is required"
                    };
                }

                // Check dependencies before deletion if requested
                if (checkDependencies)
                {
                    var dependents = AssetDatabase.GetDependencies(assetPath, false);
                    if (dependents.Length > 1) // More than just itself
                    {
                        var dependentsList = new JArray();
                        foreach (var dep in dependents)
                        {
                            if (dep != assetPath)
                            {
                                dependentsList.Add(dep);
                            }
                        }

                        return new JObject
                        {
                            ["success"] = false,
                            ["error"] = "Asset has dependencies. Use checkDependencies: false to force deletion.",
                            ["dependents"] = dependentsList
                        };
                    }
                }

                if (!AssetDatabase.DeleteAsset(assetPath))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Failed to delete asset"
                    };
                }

                return new JObject
                {
                    ["success"] = true,
                    ["deletedAsset"] = assetPath
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
        /// Deletes empty folders in the project.
        /// </summary>
        private JObject DeleteEmptyFolders(JObject parameters)
        {
            try
            {
                var rootPath = parameters["rootPath"]?.ToString() ?? "Assets";
                var deletedFolders = new List<string>();

                DeleteEmptyFoldersRecursive(rootPath, deletedFolders);

                return new JObject
                {
                    ["success"] = true,
                    ["deletedFolders"] = new JArray(deletedFolders),
                    ["count"] = deletedFolders.Count
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
        /// Formats search results as a JSON object.
        /// </summary>
        private JObject FormatSearchResults(string[] guids, int limit, bool includePackages)
        {
            var results = new JArray();
            var count = Math.Min(guids.Length, limit);

            for (int i = 0; i < count; i++)
            {
                var guid = guids[i];
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);

                if (string.IsNullOrEmpty(assetPath))
                    continue;

                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (asset == null)
                    continue;

                var assetName = Path.GetFileNameWithoutExtension(assetPath);
                var assetType = asset.GetType().Name;
                var isFolder = AssetDatabase.IsValidFolder(assetPath);

                results.Add(new JObject
                {
                    ["guid"] = guid,
                    ["path"] = assetPath,
                    ["name"] = assetName,
                    ["type"] = assetType,
                    ["isFolder"] = isFolder,
                    ["extension"] = Path.GetExtension(assetPath),
                    ["labels"] = new JArray(AssetDatabase.GetLabels(asset))
                });
            }

            return new JObject
            {
                ["success"] = true,
                ["count"] = results.Count,
                ["total"] = guids.Length,
                ["results"] = results,
                ["includePackages"] = includePackages
            };
        }

        /// <summary>
        /// Builds the folder structure for browsing.
        /// </summary>
        private JObject BuildFolderStructure(string folderPath, int maxDepth, int currentDepth, bool includeFiles, JArray fileTypes)
        {
            var folderInfo = new JObject
            {
                ["name"] = Path.GetFileName(folderPath),
                ["path"] = folderPath,
                ["isFolder"] = true,
                ["guid"] = AssetDatabase.AssetPathToGUID(folderPath)
            };

            if (currentDepth >= maxDepth && maxDepth != -1)
                return folderInfo;

            var children = new JArray();

            // Add subfolders
            var subFolders = AssetDatabase.GetSubFolders(folderPath);
            foreach (var subFolder in subFolders)
            {
                children.Add(BuildFolderStructure(subFolder, maxDepth, currentDepth + 1, includeFiles, fileTypes));
            }

            // Add files if requested
            if (includeFiles)
            {
                var allAssets = AssetDatabase.FindAssets("", new[] { folderPath });
                foreach (var guid in allAssets)
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (Path.GetDirectoryName(assetPath).Replace('\\', '/') != folderPath)
                        continue; // Skip assets in subfolders

                    if (AssetDatabase.IsValidFolder(assetPath))
                        continue; // Skip folders (already handled)

                    // Filter by file types if specified
                    if (fileTypes != null && fileTypes.Count > 0)
                    {
                        var extension = Path.GetExtension(assetPath).ToLower();
                        bool matchesFilter = false;
                        foreach (var type in fileTypes)
                        {
                            if (extension.Contains(type.ToString().ToLower()))
                            {
                                matchesFilter = true;
                                break;
                            }
                        }
                        if (!matchesFilter) continue;
                    }

                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                    if (asset != null)
                    {
                        children.Add(new JObject
                        {
                            ["name"] = Path.GetFileNameWithoutExtension(assetPath),
                            ["path"] = assetPath,
                            ["isFolder"] = false,
                            ["type"] = asset.GetType().Name,
                            ["extension"] = Path.GetExtension(assetPath),
                            ["guid"] = guid
                        });
                    }
                }
            }

            if (children.Count > 0)
                folderInfo["children"] = children;

            return folderInfo;
        }

        /// <summary>
        /// Creates a C# script asset.
        /// </summary>
        private JObject CreateScript(string scriptPath, string content)
        {
            if (!scriptPath.EndsWith(".cs"))
                scriptPath += ".cs";

            var templateContent = content ?? GenerateDefaultScriptContent(Path.GetFileNameWithoutExtension(scriptPath));

            File.WriteAllText(scriptPath, templateContent);
            AssetDatabase.ImportAsset(scriptPath);

            var guid = AssetDatabase.AssetPathToGUID(scriptPath);

            return new JObject
            {
                ["success"] = true,
                ["asset"] = new JObject
                {
                    ["name"] = Path.GetFileNameWithoutExtension(scriptPath),
                    ["path"] = scriptPath,
                    ["guid"] = guid,
                    ["type"] = "MonoScript"
                }
            };
        }

        /// <summary>
        /// Creates a material asset.
        /// </summary>
        private JObject CreateMaterial(string materialPath)
        {
            if (!materialPath.EndsWith(".mat"))
                materialPath += ".mat";

            var material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, materialPath);

            var guid = AssetDatabase.AssetPathToGUID(materialPath);

            return new JObject
            {
                ["success"] = true,
                ["asset"] = new JObject
                {
                    ["name"] = Path.GetFileNameWithoutExtension(materialPath),
                    ["path"] = materialPath,
                    ["guid"] = guid,
                    ["type"] = "Material"
                }
            };
        }

        /// <summary>
        /// Creates an animator controller asset.
        /// </summary>
        private JObject CreateAnimatorController(string controllerPath)
        {
            if (!controllerPath.EndsWith(".controller"))
                controllerPath += ".controller";

            var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var guid = AssetDatabase.AssetPathToGUID(controllerPath);

            return new JObject
            {
                ["success"] = true,
                ["asset"] = new JObject
                {
                    ["name"] = Path.GetFileNameWithoutExtension(controllerPath),
                    ["path"] = controllerPath,
                    ["guid"] = guid,
                    ["type"] = "AnimatorController"
                }
            };
        }

        /// <summary>
        /// Creates a ScriptableObject asset.
        /// </summary>
        private JObject CreateScriptableObject(string assetPath, string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = "scriptableObjectType parameter is required for ScriptableObject creation"
                };
            }

            if (!assetPath.EndsWith(".asset"))
                assetPath += ".asset";

            var type = GetScriptableObjectType(typeName);
            if (type == null)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = $"ScriptableObject type not found: {typeName}"
                };
            }

            var instance = ScriptableObject.CreateInstance(type);
            AssetDatabase.CreateAsset(instance, assetPath);

            var guid = AssetDatabase.AssetPathToGUID(assetPath);

            return new JObject
            {
                ["success"] = true,
                ["asset"] = new JObject
                {
                    ["name"] = Path.GetFileNameWithoutExtension(assetPath),
                    ["path"] = assetPath,
                    ["guid"] = guid,
                    ["type"] = type.Name
                }
            };
        }

        /// <summary>
        /// Recursively deletes empty folders.
        /// </summary>
        private void DeleteEmptyFoldersRecursive(string folderPath, List<string> deletedFolders)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
                return;

            var subFolders = AssetDatabase.GetSubFolders(folderPath);
            foreach (var subFolder in subFolders)
            {
                DeleteEmptyFoldersRecursive(subFolder, deletedFolders);
            }

            // Check if folder is empty after processing subfolders
            var remainingAssets = AssetDatabase.FindAssets("", new[] { folderPath });
            var hasContentInThisFolder = false;

            foreach (var guid in remainingAssets)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetDirectoryName(assetPath).Replace('\\', '/') == folderPath)
                {
                    hasContentInThisFolder = true;
                    break;
                }
            }

            if (!hasContentInThisFolder && folderPath != "Assets")
            {
                if (AssetDatabase.DeleteAsset(folderPath))
                {
                    deletedFolders.Add(folderPath);
                }
            }
        }

        /// <summary>
        /// Generates default C# script content.
        /// </summary>
        private string GenerateDefaultScriptContent(string className)
        {
            return $@"using UnityEngine;

public class {className} : MonoBehaviour
{{
    void Start()
    {{
        
    }}
    
    void Update()
    {{
        
    }}
}}
";
        }

        /// <summary>
        /// Gets a ScriptableObject type by name.
        /// </summary>
        private Type GetScriptableObjectType(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null && typeof(ScriptableObject).IsAssignableFrom(type))
                    return type;
            }

            // Try common Unity ScriptableObject types
            var unityType = Type.GetType($"UnityEngine.{typeName}, UnityEngine");
            if (unityType != null && typeof(ScriptableObject).IsAssignableFrom(unityType))
                return unityType;

            return null;
        }

        /// <summary>
        /// Resolves a potentially partial or hierarchical path to actual folder paths.
        /// Supports patterns like "Sol > Run", "Sol/Run", or "Sol\Run".
        /// </summary>
        private List<string> ResolveSmartPath(string inputPath)
        {
            var resolvedPaths = new List<string>();
            
            // If it's already a valid path, return it
            if (AssetDatabase.IsValidFolder(inputPath))
            {
                resolvedPaths.Add(inputPath);
                return resolvedPaths;
            }
            
            // Parse hierarchical patterns
            var pathParts = ParseHierarchicalPath(inputPath);
            if (pathParts.Count == 0)
                return resolvedPaths;
            
            // Search for matching folder hierarchies
            resolvedPaths = FindMatchingFolderHierarchies(pathParts);
            
            return resolvedPaths;
        }
        
        /// <summary>
        /// Parses hierarchical path patterns like "Sol > Run", "Sol/Run", etc.
        /// </summary>
        private List<string> ParseHierarchicalPath(string inputPath)
        {
            var parts = new List<string>();
            
            // Support various separators
            var separators = new[] { " > ", " → ", "/", "\\", "|" };
            
            foreach (var separator in separators)
            {
                if (inputPath.Contains(separator))
                {
                    var splitParts = inputPath.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries);
                    parts.AddRange(splitParts.Select(p => p.Trim()));
                    break;
                }
            }
            
            // If no separators found, treat as single folder name
            if (parts.Count == 0)
            {
                parts.Add(inputPath.Trim());
            }
            
            return parts;
        }
        
        /// <summary>
        /// Finds folders matching a hierarchical pattern.
        /// </summary>
        private List<string> FindMatchingFolderHierarchies(List<string> pathParts)
        {
            var matches = new List<string>();
            
            if (pathParts.Count == 0)
                return matches;
            
            // Start with all folders matching the first part
            var firstPartMatches = FindFoldersByName(pathParts[0]);
            
            if (pathParts.Count == 1)
            {
                return firstPartMatches;
            }
            
            // For hierarchical patterns, check if subsequent parts exist as children
            foreach (var firstMatch in firstPartMatches)
            {
                var currentPath = firstMatch;
                bool allPartsFound = true;
                
                for (int i = 1; i < pathParts.Count; i++)
                {
                    var childFolders = AssetDatabase.GetSubFolders(currentPath);
                    var nextMatch = childFolders.FirstOrDefault(folder => 
                        Path.GetFileName(folder).Contains(pathParts[i], StringComparison.OrdinalIgnoreCase));
                    
                    if (nextMatch != null)
                    {
                        currentPath = nextMatch;
                    }
                    else
                    {
                        allPartsFound = false;
                        break;
                    }
                }
                
                if (allPartsFound)
                {
                    matches.Add(currentPath);
                }
            }
            
            return matches;
        }
        
        /// <summary>
        /// Finds all folders in the project that contain the specified name (case-insensitive).
        /// </summary>
        private List<string> FindFoldersByName(string folderName)
        {
            var matches = new List<string>();
            SearchFoldersRecursive("Assets", folderName, matches);
            return matches;
        }
        
        /// <summary>
        /// Recursively searches for folders matching a name pattern.
        /// </summary>
        private void SearchFoldersRecursive(string parentPath, string searchName, List<string> matches)
        {
            if (!AssetDatabase.IsValidFolder(parentPath))
                return;
            
            var subFolders = AssetDatabase.GetSubFolders(parentPath);
            
            foreach (var folder in subFolders)
            {
                var folderName = Path.GetFileName(folder);
                
                // Check for partial match (case-insensitive)
                if (folderName.Contains(searchName, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(folder);
                }
                
                // Recursively search in subfolders
                SearchFoldersRecursive(folder, searchName, matches);
            }
        }
        
        /// <summary>
        /// Creates a user-friendly response when multiple folder matches are found.
        /// </summary>
        private JObject CreateMultipleMatchesResponse(List<string> matches, string originalPath)
        {
            var matchesArray = new JArray();
            
            foreach (var match in matches)
            {
                matchesArray.Add(new JObject
                {
                    ["path"] = match,
                    ["name"] = Path.GetFileName(match),
                    ["fullPath"] = match
                });
            }
            
            return new JObject
            {
                ["success"] = false,
                ["error"] = $"Multiple folders found matching '{originalPath}'. Please specify which one:",
                ["originalPath"] = originalPath,
                ["suggestions"] = matchesArray,
                ["count"] = matches.Count
            };
        }
        
        /// <summary>
        /// Determines if a query string looks like a folder pattern rather than an asset search.
        /// </summary>
        private bool IsLikelyFolderPattern(string query)
        {
            // Check for hierarchical separators
            var separators = new[] { " > ", " → ", "/", "\\", "|" };
            if (separators.Any(sep => query.Contains(sep)))
                return true;
            
            // Check if it's a common folder name pattern (no file extensions)
            if (!query.Contains(".") && query.Length > 2)
                return true;
            
            return false;
        }
        
        /// <summary>
        /// Searches for assets within folders matching a pattern.
        /// </summary>
        private JObject SearchAssetsInFolderPattern(string pattern, int limit, bool includePackages)
        {
            try
            {
                var resolvedPaths = ResolveSmartPath(pattern);
                
                if (resolvedPaths.Count == 0)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"No folders found matching '{pattern}'"
                    };
                }
                
                if (resolvedPaths.Count > 1)
                {
                    return CreateMultipleMatchesResponse(resolvedPaths, pattern);
                }
                
                // Search for all assets in the resolved folder
                var folderPath = resolvedPaths[0];
                var searchFilter = includePackages ? "" : "-packages";
                var allAssets = AssetDatabase.FindAssets(searchFilter, new[] { folderPath });
                
                // Limit results
                var limitedAssets = allAssets.Take(limit).ToArray();
                
                var result = FormatSearchResults(limitedAssets, limit, includePackages);
                result["searchedInFolder"] = folderPath;
                result["pattern"] = pattern;
                
                return result;
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
    }
}