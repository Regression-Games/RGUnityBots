using System;
using System.Linq;
using UnityEngine;

namespace RegressionGames
{
    public class ReplayModelManager : MonoBehaviour
    {
        [SerializeField] private NamedModel[] models = new NamedModel[0];

#if UNITY_EDITOR

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

            return null;
        }

#endif

        [Serializable]
        public struct NamedModel
        {
            public string characterType;
            public GameObject GFXprefab;
        }
    }
}
