using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RegressionGames.Editor
{
    public static class RGEditorUtils
    {
        public static string GetAssetPathParent(string assetPath)
        {
            var lastSlashIndex = assetPath.LastIndexOf('/');
            if (lastSlashIndex < 0)
            {
                return null;
            }

            return assetPath[..lastSlashIndex];
        }

        public static string GetAssetPathLeaf(string assetPath)
        {
            var lastSlashIndex = assetPath.LastIndexOf('/');
            if (lastSlashIndex < 0)
            {
                return null;
            }

            return assetPath[(lastSlashIndex + 1)..];
        }

        public static void CreateAllAssetFolders(string path)
        {
            path = path.TrimEnd('/');
//TODO: REG-1424 Cannot depend on Editor only tools for bot runtimes
#if UNITY_EDITOR
            if (AssetDatabase.IsValidFolder(path))
            {
                // This path already exists as a folder, so we're done.
                return;
            }

            if (path == "Assets")
            {
                // The asset folder should always exist, so if we get here, something is wrong.
                throw new InvalidOperationException("Asset folder does not exist");
            }

            // We need to create this directory, but we may need to create it's parents too.
            var parentPath = GetAssetPathParent(path);
            var leafName = GetAssetPathLeaf(path);
            CreateAllAssetFolders(parentPath);
            var createdGuid = AssetDatabase.CreateFolder(parentPath, leafName);
            var createdPath = AssetDatabase.GUIDToAssetPath(createdGuid);

            // Unity will deduplicate folder names in CreateFolder if the folder already exists
            // However, we should be OK because we've already checked that the folder doesn't exist.
            // Still, make sure we got the folder we expected.
            if (createdPath != path)
            {
                throw new InvalidOperationException("Unexpected folder name from AssetDatabase.CreateFolder");
            }
#endif
        }
    }
}
