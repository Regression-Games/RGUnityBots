using System;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
#endif
using UnityEngine;

namespace RegressionGames
{
#if UNITY_EDITOR
    [ExecuteInEditMode]
    public class DestroyOn : MonoBehaviour
    {
        public DestroyOn()
        {
            EditorApplication.playModeStateChanged += HandlePlayStateChange;
        }

        private static void HandlePlayStateChange(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) DestroyEverything();
        }

        public static void DestroyEverything()
        {
            var objs = FindObjectsOfType<DestroyOn>();
            foreach (var obj in objs)
                try
                {
                    DestroyImmediate(obj.gameObject);
                }
                catch (Exception e)
                {
                    RGDebug.LogException(e);
                }
        }
    }

    public class RGAssetModificationProcessor : AssetModificationProcessor
    {
        public static string[] OnWillSaveAssets(string[] paths)
        {
            foreach (var path in paths)
                if (path.Contains(".unity"))
                    // this is a scene file
                    DestroyOn.DestroyEverything();

            return paths;
        }
    }

    public class ScenePostProcessor
    {
        [PostProcessScene(0)]
        public static void OnPostProcessScene()
        {
            // If we are building.. take this stuff out of the build
            if (BuildPipeline.isBuildingPlayer) DestroyOn.DestroyEverything();
        }
    }

#endif

}
