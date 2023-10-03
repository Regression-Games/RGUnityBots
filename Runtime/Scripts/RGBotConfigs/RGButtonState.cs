using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RegressionGames.RGBotConfigs
{
    public class RGButtonState : RGState
    {

        public RGButtonState()
        {
            if (string.IsNullOrEmpty(this.objectType))
            {
                this.objectType = "Button";
            }
        }

        public override Dictionary<string, object> GetState()
        {
            var state = new Dictionary<string, object>();
            CanvasGroup cg = this.gameObject.GetComponentInParent<CanvasGroup>();
            state["interactable"] = (cg == null || cg.interactable) && this.gameObject.GetComponent<Button>().interactable;
            return state;
        }
    }
}
