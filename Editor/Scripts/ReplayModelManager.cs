using System;
using System.Linq;
using RegressionGames.Editor;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace RegressionGames.Editor
{
#if UNITY_EDITOR
    public class ReplayModelManager : MonoBehaviour
    {
        [SerializeField] private NamedModel[] models = new NamedModel[0];

        public GameObject getModelPrefabForType(string type, string charType)
        {

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
            
        }

        [Serializable]
        public struct NamedModel
        {
            public string characterType;
            public GameObject GFXprefab;
        }
    }
#endif
}
