using System.Linq;
using RegressionGames;
#if UNITY_EDITOR
using RegressionGames.Editor.CodeGenerators;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
namespace RegressionGames.Editor.CodeGenerators
{
    class BuildAutoPreProcessor : IPreprocessBuildWithReport
    {
        public int callbackOrder { get { return 0; } }
        
        public void OnPreprocessBuild(BuildReport report)
        {
            BuildAssetPostProcessor.TriggerGeneration();
        }
    }
   
    class BuildAssetPostProcessor : UnityEditor.AssetPostprocessor
    {

        static bool _enabled = true;
        
        public static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths, bool didDomainReload)
        {
            // domain reload after build resets enabled to true
            // and this gets called once with the original script changes, once with the generated changes, and then one more time with an empty list
            if (_enabled && importedAssets.FirstOrDefault(ia => ia.EndsWith(".cs") && !ia.StartsWith("Packages/gg.regression.unity.bots") && !ia.Contains("RegressionGames/Runtime/GeneratedScripts")) != null)
            {
                TriggerGeneration();
            }
        }

        public static void TriggerGeneration()
        {
            // domain reload after build resets enabled to true
            if (_enabled)
            {
                _enabled = false;
                RGDebug.LogInfo("Generating Regression Games scripts automatically due to asset change or build.");
                RGCodeGenerator.GenerateRGScripts(); 
            }
        }
    }
}
#endif
