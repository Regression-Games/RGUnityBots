using System.Collections.Concurrent;
using System.Collections.Generic;
using RegressionGames.StateActionTypes;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RegressionGames.RGBotConfigs
{
    // Only allow one of these on the Overlay game object
    // RGEntity does the enforcement of only looking for this on the RGOverlayMenu
    [DisallowMultipleComponent]
    public class RGAction_ClickButton : RGAction
    {
        private ConcurrentQueue<Button> _buttonsToClick = new();
        
        public void Update()
        {
            // one button click per frame update
            if (_buttonsToClick.TryDequeue(out Button button))
            {
                // this would work, but only fires the action
                // doesn't actually 'click' the button
                // button.onClick.Invoke();

                // this sends the event as though you actually clicked the button
                // so you get button click effects and everything
                ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current),
                    ExecuteEvents.submitHandler);

                // clicking at specific points is also possible, but involves
                // importing dll's and moving the cursor.. let's avoid this for now

            }
        }

        public override string GetActionName()
        {
            return "ClickButton";
        }

        public override void StartAction(Dictionary<string, object> input)
        {
            if (input["targetId"] != null)
            {
                var targetId = int.Parse(input["targetId"].ToString());
                var target = RGFindUtils.Instance.FindOneByInstanceId<RGEntity>(targetId);
                if (target != null) // this is the unity overloaded != checking for destroyed
                {
                    _buttonsToClick.Enqueue(target.gameObject.GetComponent<Button>());
                }
            }
        }
    }

    public class RGActionRequest_ClickButton : RGActionRequest
    {
        public RGActionRequest_ClickButton(int targetId)
        {
            action = "ClickButton";
            Input = new() { { "targetId", targetId } };
        }
    }
}
