using System;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RegressionGames.Editor
{
#if UNITY_EDITOR
    public class ReplayModelManager : ScriptableObject
    {
        
        public readonly static string ASSET_PATH = "Assets/RegressionGames/Editor/CustomReplayModels.asset";
        
        
        [SerializeField] private NamedModel[] models = Array.Empty<NamedModel>();


        public void OnEnable()
        {
            
            if ( AssetDatabase.GetMainAssetTypeAtPath( ASSET_PATH ) != null) {
                _this = AssetDatabase.LoadAssetAtPath<ReplayModelManager>(ASSET_PATH);
            }
        }

        [CanBeNull] private static ReplayModelManager _this = null;

        public static ReplayModelManager GetInstance()
        {
            if (!AssetDatabase.IsValidFolder("Assets/RegressionGames"))
            {
                AssetDatabase.CreateFolder("Assets", "RegressionGames");
            }

            if (!AssetDatabase.IsValidFolder("Assets/RegressionGames/Editor"))
            {
                AssetDatabase.CreateFolder("Assets/RegressionGames", "Editor");
            }

            if (_this == null)
            {
                _this = CreateInstance<ReplayModelManager>();
            }

            if ( AssetDatabase.GetMainAssetTypeAtPath( ASSET_PATH ) == null) {
                AssetDatabase.CreateAsset(_this, ASSET_PATH );
                AssetDatabase.SaveAssets();
            }
            
            return _this;
        }

        public void OpenAssetInspector()
        {
            if (_this != null)
            {
                AssetDatabase.OpenAsset(_this);
            }
        }
        
        public bool HasEntries()
        {
            return models.Length > 0;
        }

        /**
         * Will add a new list entry for the given object type if one does not already exist
         * If this returns 'true', the caller is responsible for calling `AssetDatabase.SaveAssets();`
         */
        public bool AddObjectType(string objectType)
        {
            if (models.FirstOrDefault(x => x.objectType == objectType).objectType == null)
            {
                models = models.Append(new NamedModel(objectType, null)).ToArray();
                return true;
            }

            return false;
        }
        
        [CanBeNull]
        public GameObject getModelPrefabForType(string type, string charType)
        {

            NamedModel nm;

            if (!string.IsNullOrEmpty(charType))
            {
                nm = models.FirstOrDefault(model => model.objectType == charType);
                if (nm.objectType != null && nm.modelPrefab != null) return nm.modelPrefab;
            }

            nm = models.FirstOrDefault(model => model.objectType == type);
            if (nm.objectType != null && nm.modelPrefab != null) return nm.modelPrefab;
                
            GameObject defaultPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{RGBotReplayWindow.PREFAB_PATH}/DefaultModel.prefab");

            // could be null
            return defaultPrefab;
            
        }

        [Serializable]
        public struct NamedModel
        {
            public NamedModel(string objectType, GameObject modelPrefab)
            {
                this.objectType = objectType;
                this.modelPrefab = modelPrefab;
            }
            
            [CanBeNull] public string objectType;
            [CanBeNull] public GameObject modelPrefab;
        }
    }
#endif
}
