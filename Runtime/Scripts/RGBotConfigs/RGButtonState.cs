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

        public Dictionary<string, object> GetState()
        {
            var state = new Dictionary<string, object>();
            CanvasGroup cg = this.gameObject.GetComponentInParent<CanvasGroup>();
            state["interactable"] = (cg == null || cg.interactable) && this.gameObject.GetComponent<Button>().interactable;
            return state;
        }

        public RGStateEntity GetGameObjectState()
        {
            var state = new RGStateEntity()
            {
                ["id"] = transform.GetInstanceID(),
            };
            var dict = GetState();
            foreach (var entry in dict)
            {
                state.Add(entry.Key, entry.Value);
            }
            return state;
        }
    }
}
