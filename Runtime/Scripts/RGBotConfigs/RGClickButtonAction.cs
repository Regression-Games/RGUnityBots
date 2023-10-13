using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RegressionGames.RGBotConfigs
{
    public class RGClickButtonAction : RGAction
    {
        private ConcurrentQueue<Button> buttonsToClick = new ConcurrentQueue<Button>();
        

        public void Update()
        {
            if (buttonsToClick.IsEmpty) return;

            while (buttonsToClick.TryDequeue(out Button button))
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
                var target = RGFindUtils.Instance.FindOneByInstanceId<RGButtonState>(targetId);
                if (target != null) // this is the unity overloaded != checking for destroyed
                {
                    buttonsToClick.Enqueue(target.gameObject.GetComponent<Button>());
                }
            }
        }
    }
}
