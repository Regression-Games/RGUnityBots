using System;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

//TODO: Can we catch  editor start / exit
namespace RegressionGames
{
    [ExecuteInEditMode]
    public class DestroyOn : MonoBehaviour
    {
#if UNITY_EDITOR
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
                    Debug.LogError(e);
                }
        }
#endif
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
}
