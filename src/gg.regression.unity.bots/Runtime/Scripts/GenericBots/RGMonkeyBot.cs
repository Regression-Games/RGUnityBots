using System.Collections.Generic;
using System.Linq;
using RegressionGames.ActionManager;
using UnityEngine;

namespace RegressionGames.GenericBots
{
    public class RGMonkeyBot : MonoBehaviour, IRGBot
    {
        public float actionInterval = 0.1f; // unscaled time
        
        private float _lastActionTime;

        private IList<IList<RGActionInput>> _validInputsBuf;
        private IList<IList<RGActionInput>> _remainingInputsBuf;
        private IList<RGActionInput> _performedInputsBuf;
    
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
            _validInputsBuf = new List<IList<RGActionInput>>();
            _performedInputsBuf = new List<RGActionInput>();
            
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
                return;
            }
            _lastActionTime = currentTimeUnscaled;

            // Compute the set of valid input combinations
            int validInputsBufIdx = 0;
            foreach (var inputList in _validInputsBuf)
            {
                inputList.Clear();
            }
            foreach (var actionInstance in RGActionManager.GetValidActions())
            {
                IList<RGActionInput> inputList;
                if (validInputsBufIdx >= _validInputsBuf.Count)
                {
                    inputList = new List<RGActionInput>();
                    _validInputsBuf.Add(inputList);
                }
                else
                {
                    inputList = _validInputsBuf[validInputsBufIdx];
                }

                foreach (var inp in actionInstance.GetInputs(actionInstance.BaseAction.ParameterRange.RandomSample()))
                {
                    inputList.Add(inp);
                }
                
                ++validInputsBufIdx;
            }
            
            // Randomly perform inputs from the list 
            _performedInputsBuf.Clear();
            for (;;)
            {
                _remainingInputsBuf.Clear();

                // Compute the set of remaining inputs in the list that do not overlap with the ones that have been performed
                var remainingInputs = _validInputsBuf.Where(inputList =>
                    !inputList.Any(inp => _performedInputsBuf.Any(perfInp => perfInp.Overlaps(inp))));
                foreach (var inputList in remainingInputs)
                {
                    _remainingInputsBuf.Add(inputList);
                }

                if (_remainingInputsBuf.Count > 0)
                {
                    var inputList = _remainingInputsBuf[Random.Range(0, _remainingInputsBuf.Count)];
                    foreach (var inp in inputList)
                    {
                        inp.Perform();
                        _performedInputsBuf.Add(inp);
                    }
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