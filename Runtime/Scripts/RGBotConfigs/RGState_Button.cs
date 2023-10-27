using System;
using System.Collections.Generic;
using RegressionGames.StateActionTypes;
using UnityEngine;
using UnityEngine.UI;

namespace RegressionGames.RGBotConfigs
{
    public class RGState_Button : MonoBehaviour, IRGState
    {
        [Tooltip("A type name for associating like objects in the state")]
        public string objectType = "Button";

        public RGStateEntity GetGameObjectState()
        {
            var state = new RGStateEntity_Button()
            {
                ["id"] = transform.GetInstanceID(),
                ["type"] = objectType,
                ["isPlayer"] = false,
                ["isRuntimeObject"] = false,
                ["position"] = Vector3.zero,
                ["rotation"] = Vector3.zero
            };
            
            CanvasGroup cg = this.gameObject.GetComponentInParent<CanvasGroup>();
            state["interactable"] = (cg == null || cg.interactable) && this.gameObject.GetComponent<Button>().interactable;
            
            return state;
        }

        [Serializable]
        public class RGStateEntity_Button : RGStateEntity
        {
            // ReSharper disable once InconsistentNaming
            public bool interactable => (bool)this.GetValueOrDefault("interactable", false);
        }
    }
}
