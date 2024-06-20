using System.Collections.Generic;
using RegressionGames.ActionManager;
using UnityEngine;

namespace RegressionGames.GenericBots
{
    public class RGMonkeyBot : MonoBehaviour, IRGBot
    {
        private List<IRGGameActionInstance> _actionInstances;
    
        void Start()
        {
            RGActionManager.StartSession(this);
            _actionInstances = new List<IRGGameActionInstance>();
            DontDestroyOnLoad(this);
        }
    
        void Update()
        {
            _actionInstances.Clear();
            foreach (var actionInst in RGActionManager.GetValidActions())
            {
                _actionInstances.Add(actionInst);
            }

            if (_actionInstances.Count > 0)
            {
                int actionIndex = UnityEngine.Random.Range(0, _actionInstances.Count);
                var chosenActionInst = _actionInstances[actionIndex];
                chosenActionInst.Perform(chosenActionInst.BaseAction.ParameterRange.RandomSample());
            }
        }

        void OnDestroy()
        {
            RGActionManager.StopSession();
        }
    }
}