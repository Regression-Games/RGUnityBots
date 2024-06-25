using System.Collections.Generic;
using RegressionGames.ActionManager;
using UnityEngine;

namespace RegressionGames.GenericBots
{
    public class RGMonkeyBot : MonoBehaviour, IRGBot
    {
        public float actionInterval = 0.0f; // every frame by default
            
        private Dictionary<int, List<IRGGameActionInstance>> _actionInstancesByGroup;
        private ISet<int> _validActionGroups;
        private IList<int> _validActionGroupsList;
        private float _lastActionTime;
    
        void Start()
        {
            if (!RGActionManager.IsAvailable)
            {
                RGDebug.LogError("Monkey bot is currently unavailable");
                Destroy(this);
                return;
            }
            RGActionManager.StartSession(this);
            _actionInstancesByGroup = new Dictionary<int, List<IRGGameActionInstance>>();
            _validActionGroups = new HashSet<int>();
            _validActionGroupsList = new List<int>();
            _lastActionTime = Time.unscaledTime;
            foreach (RGGameAction action in RGActionManager.Actions)
            {
                if (!_actionInstancesByGroup.TryGetValue(action.ActionGroup, out _))
                {
                    _actionInstancesByGroup[action.ActionGroup] = new List<IRGGameActionInstance>();
                }
            }
            DontDestroyOnLoad(this);
        }
    
        void Update()
        {
            if (GameObject.Find("Selection Panel") != null)
            {
                // pause sending events while the overlay panel is open
                return;
            }

            float currentTimeUnscaled = Time.unscaledTime;
            if (currentTimeUnscaled - _lastActionTime < actionInterval)
            {
                return;
            }
            _lastActionTime = currentTimeUnscaled;
            
            foreach (var p in _actionInstancesByGroup)
            {
                p.Value.Clear();
            }
            _validActionGroups.Clear();
            _validActionGroupsList.Clear();
            foreach (var actionInst in RGActionManager.GetValidActions())
            {
                _actionInstancesByGroup[actionInst.BaseAction.ActionGroup].Add(actionInst);
                _validActionGroups.Add(actionInst.BaseAction.ActionGroup);
            }
            foreach (int grp in _validActionGroups)
            {
                _validActionGroupsList.Add(grp);
            }

            if (_validActionGroupsList.Count > 0)
            {
                int grp = _validActionGroupsList[Random.Range(0, _validActionGroupsList.Count)];
                foreach (var actionInst in _actionInstancesByGroup[grp])
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