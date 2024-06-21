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
            foreach (var actionInst in RGActionManager.GetValidActions())
            {
                if (Random.Range(0, 2) == 1)
                {
                    actionInst.Perform(actionInst.BaseAction.ParameterRange.RandomSample());
                }
            }
        }

        void OnDestroy()
        {
            RGActionManager.StopSession();
        }
    }
}