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

        private ISet<int> _mouseBtnsBuf;

        private void ResetState()
        {
            _actionsBuf.Clear();
            _remainingActionsBuf.Clear();
            _mouseBtnsBuf.Clear();
        }
    
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
            _mouseBtnsBuf = new HashSet<int>();
            
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
            
            bool didHeuristic = false;
            
            // Heuristic: If the last action was a mouse down on a Unity UI button, always release the mouse button on this frame over
            // the same mouse coordinate to trigger the click event
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
                        didHeuristic = true;
                    }
                }
            }
            if (didHeuristic)
            {
                ResetState();
                return;
            }
            
            // Heuristic: If the last set of inputs was a mouse position movement + a set of mouse button presses, 
            // then have some random chance of releasing those buttons over the same mouse position to complete a potential click event.
            // Otherwise, in general it is very unlikely that a mouse button press and release would occur over the same position.
            {
                bool haveMousePos = false;
                _mouseBtnsBuf.Clear();
                foreach (var inp in _actionsBuf.Where(act => act.Performed).SelectMany(act => act.Inputs))
                {
                    if (inp is MousePositionInput)
                    {
                        haveMousePos = true;
                    } else if (inp is MouseButtonInput mbInput)
                    {
                        if (mbInput.IsPressed)
                        {
                            _mouseBtnsBuf.Add(mbInput.MouseButton);
                        }
                    } else if (inp is LegacyKeyInput keyInput)
                    {
                        if (keyInput.KeyCode >= KeyCode.Mouse0 && keyInput.KeyCode <= KeyCode.Mouse6)
                        {
                            _mouseBtnsBuf.Add(keyInput.KeyCode - KeyCode.Mouse0);
                        }
                    }
                }

                if (haveMousePos && _mouseBtnsBuf.Count > 0)
                {
                    if (Random.Range(0, 2) == 1) // 50% chance of attempting a click event
                    {
                        foreach (var btn in _mouseBtnsBuf)
                        {
                            new MouseButtonInput(btn, false).Perform();
                        }
                        didHeuristic = true;
                    }
                }
            }
            if (didHeuristic)
            {
                ResetState();
                return;
            }
            
            ResetState();

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