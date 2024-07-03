using System.Collections.Generic;
using System.Linq;
using RegressionGames.ActionManager;
using RegressionGames.ActionManager.Actions;
using UnityEngine;

namespace RegressionGames.GenericBots
{
    internal class MonkeyBotActionEntry
    {
        public IRGGameActionInstance ActionInstance;
        public object Parameter;
        public bool Performed;
        public IList<RGActionInput> Inputs;
    }
    
    public class RGMonkeyBot : MonoBehaviour, IRGBot
    {
        public float actionInterval = 0.05f; // unscaled time
        
        private float _lastActionTime;

        private IList<MonkeyBotActionEntry> _actionsBuf;
        private IList<MonkeyBotActionEntry> _remainingActionsBuf;
    
        void Start()
        {
            if (!RGActionManager.IsAvailable)
            {
                RGDebug.LogError("Monkey bot is currently unavailable");
                Destroy(this);
                return;
            }
            RGActionManager.StartSession(this);
            
            _lastActionTime = Time.unscaledTime;
            _actionsBuf = new List<MonkeyBotActionEntry>();
            _remainingActionsBuf = new List<MonkeyBotActionEntry>();
            
            DontDestroyOnLoad(this);
        }
    
        void Update()
        {
            if (GameObject.Find("Selection Panel") != null)
            {
                // Pause sending events while the overlay panel is open
                return;
            }

            float currentTimeUnscaled = Time.unscaledTime;
            if (currentTimeUnscaled - _lastActionTime < actionInterval)
            {
                // if not deciding on a new action, then repeat inputs sent on the last frame
                foreach (var act in _actionsBuf.Where(act => act.Performed))
                {
                    foreach (var inp in act.Inputs)
                    {
                        inp.Perform();
                    }
                }
                return;
            }
            _lastActionTime = currentTimeUnscaled;
            
            // If the last input sent was a press on a button that is still valid, always do a release on this frame to activate the button
            // This heuristic makes it more likely to trigger a button press.
            bool didReleaseBtn = false;
            foreach (var performedAction in _actionsBuf.Where(act => act.Performed))
            {
                if (performedAction.ActionInstance is UIButtonPressInstance && (bool)performedAction.Parameter)
                {
                    var action = performedAction.ActionInstance.BaseAction;
                    var targetObject = performedAction.ActionInstance.TargetObject;
                    if (targetObject != null && action.IsValidForObject(targetObject))
                    {
                        foreach (var inp in action.GetInstance(targetObject).GetInputs(false))
                        {
                            inp.Perform();
                        }
                        didReleaseBtn = true;
                    }
                }
            }

            _actionsBuf.Clear();
            
            if (didReleaseBtn)
            {
                return;
            }

            // Compute the set of valid actions
            foreach (var actionInstance in RGActionManager.GetValidActions())
            {
                var entry = new MonkeyBotActionEntry()
                {
                    ActionInstance = actionInstance,
                    Parameter = actionInstance.BaseAction.ParameterRange.RandomSample(),
                    Performed = false
                };
                entry.Inputs = new List<RGActionInput>(actionInstance.GetInputs(entry.Parameter));
                if (entry.Inputs.Count > 0)
                {
                    _actionsBuf.Add(entry);
                }
            }

            // Randomly perform actions from the list 
            // This repeatedly selects actions whose inputs do not overlap
            // with the inputs that have already been performed.
            for (;;)
            {
                _remainingActionsBuf.Clear();
                foreach (var action in _actionsBuf.Where(act => !act.Performed))
                {
                    if (!_actionsBuf.Any(performedAction =>
                            performedAction.Performed && performedAction.Inputs.Overlap(action.Inputs)))
                    {
                        _remainingActionsBuf.Add(action);
                    }
                }

                if (_remainingActionsBuf.Count > 0)
                {
                    var action = _remainingActionsBuf[Random.Range(0, _remainingActionsBuf.Count)];
                    foreach (var inp in action.Inputs)
                    {
                        inp.Perform();
                    }
                    action.Performed = true;
                }
                else
                {
                    break;
                }
            }
        }

        void OnDestroy()
        {
            RGActionManager.StopSession();
        }
    }
}