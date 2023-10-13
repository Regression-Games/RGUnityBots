using System.Collections.Generic;
using RegressionGames.StateActionTypes;
using UnityEngine;
using UnityEngine.UI;

namespace RegressionGames.RGBotConfigs
{
    public class RGButtonState : MonoBehaviour, IRGState
    {
        [Tooltip("A type name for associating like objects in the state")]
        public string objectType = "Button";

        public RGStateEntity GetGameObjectState()
        {
            var state = new RGStateEntity()
            {
                ["id"] = transform.GetInstanceID(),
                ["type"] = objectType,
            };
            
            CanvasGroup cg = this.gameObject.GetComponentInParent<CanvasGroup>();
            state["interactable"] = (cg == null || cg.interactable) && this.gameObject.GetComponent<Button>().interactable;
            
            return state;
        }
    }
}
