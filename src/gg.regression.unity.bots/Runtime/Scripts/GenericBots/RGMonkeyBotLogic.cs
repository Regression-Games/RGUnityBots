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

    public class RGMonkeyBotLogic
    {
        public float ActionInterval { get; set; } = 0.05f;

        private float _lastActionTime;
        private IList<MonkeyBotActionEntry> _actionsBuf;
        private IList<MonkeyBotActionEntry> _remainingActionsBuf;
        private ISet<int> _mouseBtnsBuf;

        public RGMonkeyBotLogic()
        {
            Reset();
        }

        public void Reset()
        {
            _lastActionTime = Time.unscaledTime;
            _actionsBuf = new List<MonkeyBotActionEntry>();
            _remainingActionsBuf = new List<MonkeyBotActionEntry>();
            _mouseBtnsBuf = new HashSet<int>();
        }

        private void ResetState()
        {
            _actionsBuf.Clear();
            _remainingActionsBuf.Clear();
            _mouseBtnsBuf.Clear();
        }

        /// <summary>
        /// Perform an update step of the monkey bot.
        /// </summary>
        /// <param name="segmentNumber">The bot sequence segment number</param>
        /// <returns>Whether any action was performed (inputs simulated).</returns>
        public bool Update(int segmentNumber)
        {
            bool didAnyAction = false;
            if (GameObject.Find("Selection Panel") != null)
            {
                // Pause sending events while the overlay panel is open
                return didAnyAction;
            }

            float currentTimeUnscaled = Time.unscaledTime;
            if (currentTimeUnscaled - _lastActionTime < ActionInterval)
            {
                // if not deciding on a new action, then repeat inputs sent on the last frame
                foreach (var act in _actionsBuf.Where(act => act.Performed))
                {
                    // since we are repeating here.. the object target could no longer exist (was destroyed since last frame) due to the action taken.. this is common for choosing spell actions or other temporary object targets
                    if (act.ActionInstance.TargetObject != null)
                    {
                        // record before doing because often times once we do, that action longer exists as a game object
                        RGActionRuntimeCoverageAnalysis.RecordActionUsage(act.ActionInstance.BaseAction, act.ActionInstance.TargetObject);
                        foreach (var inp in act.Inputs)
                        {
                            inp.Perform(segmentNumber);
                            didAnyAction = true;
                        }
                    }
                }
                return didAnyAction;
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
                        var inst = action.GetInstance(targetObject);
                        if (inst.IsValidParameter(false))
                        {
                            // record before doing because often times once we do, that action longer exists as a game object
                            RGActionRuntimeCoverageAnalysis.RecordActionUsage(inst.BaseAction, inst.TargetObject);
                            foreach (var inp in inst.GetInputs(false))
                            {
                                inp.Perform(segmentNumber);
                                didAnyAction = true;
                            }
                        }
                        didHeuristic = true;
                    }
                }
            }
            if (didHeuristic)
            {
                ResetState();
                return didAnyAction;
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
                            new MouseButtonInput(btn, false).Perform(segmentNumber);
                            didAnyAction = true;
                        }
                        didHeuristic = true;
                    }
                }
            }
            if (didHeuristic)
            {
                ResetState();
                return didAnyAction;
            }

            ResetState();

            // Compute the set of valid actions
            foreach (var actionInstance in RGActionManager.GetValidActions())
            {
                // try to find a valid parameter within a fixed number of attempts
                object param = null;
                bool isParamValid = false;
                for (int i = 0; i < 64 && !isParamValid; ++i)
                {
                    param = actionInstance.BaseAction.ParameterRange.RandomSample();
                    isParamValid = actionInstance.IsValidParameter(param);
                }
                if (!isParamValid)
                    continue;

                // store the action inputs
                var entry = new MonkeyBotActionEntry()
                {
                    ActionInstance = actionInstance,
                    Parameter = param,
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
            do
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
                    // record before doing because often times once we do, that action longer exists as a game object
                    RGActionRuntimeCoverageAnalysis.RecordActionUsage(action.ActionInstance.BaseAction, action.ActionInstance.TargetObject);
                    foreach (var inp in action.Inputs)
                    {
                        inp.Perform(segmentNumber);
                        didAnyAction = true;
                    }
                    action.Performed = true;
                }
            } while (_remainingActionsBuf.Count > 0);

            return didAnyAction;
        }
    }
}
