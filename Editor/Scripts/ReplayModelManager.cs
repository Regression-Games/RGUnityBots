using System;
using System.Linq;
using RegressionGames.Editor;
using UnityEditor;
using UnityEngine;

namespace RegressionGames
{
    public class ReplayModelManager : MonoBehaviour
    {
        [SerializeField] private NamedModel[] models = new NamedModel[0];

        public GameObject getModelPrefabForType(string type, string charType)
        {
#if UNITY_EDITOR
            NamedModel nm;

            if (charType != null)
            {
                nm = models.FirstOrDefault(model => model.characterType == charType);
                if (nm.characterType != null) return nm.GFXprefab;
            }

            nm = models.FirstOrDefault(model => model.characterType == type);
            if (nm.characterType != null) return nm.GFXprefab;
                
            GameObject defaultPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{RGBotReplayWindow.PREFAB_PATH}/DefaultModel.prefab");

            // could be null
            return defaultPrefab;
#endif
            
        }

        [Serializable]
        public struct NamedModel
        {
            public string characterType;
            public GameObject GFXprefab;
        }
    }
}
