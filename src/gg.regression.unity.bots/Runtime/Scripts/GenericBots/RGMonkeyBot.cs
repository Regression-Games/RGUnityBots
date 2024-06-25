using System.Collections.Generic;
using RegressionGames.ActionManager;
using UnityEngine;

namespace RegressionGames.GenericBots
{
    public class RGMonkeyBot : MonoBehaviour, IRGBot
    {
        public float actionInterval = 0.0f; // every frame by default
            
        private Dictionary<int, Dictionary<RGGameAction, List<IRGGameActionInstance>>> _actionsByGroup;
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
            
            // Initialize buffers
            _actionsByGroup = new Dictionary<int, Dictionary<RGGameAction, List<IRGGameActionInstance>>>();
            _validActionGroups = new HashSet<int>();
            _validActionGroupsList = new List<int>();
            foreach (RGGameAction action in RGActionManager.Actions)
            {
                if (!_actionsByGroup.TryGetValue(action.ActionGroup, out var groupActions))
                {
                    groupActions = new Dictionary<RGGameAction, List<IRGGameActionInstance>>();
                    _actionsByGroup[action.ActionGroup] = groupActions;
                }
                groupActions.Add(action, new List<IRGGameActionInstance>());
            }
            
            _lastActionTime = Time.unscaledTime;
            
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
            
            // Clear buffers
            foreach (var p in _actionsByGroup)
            {
                foreach (var entry in p.Value)
                {
                    entry.Value.Clear();
                }
            }
            _validActionGroups.Clear();
            _validActionGroupsList.Clear();

            // Gather all valid actions in a mapping from group number -> action -> action instances
            foreach (var entry in RGActionManager.GetValidActions())
            {
                RGGameAction action = entry.Key;
                var instances = entry.Value;
                if (instances.Count > 0)
                {
                    _validActionGroups.Add(action.ActionGroup);
                    var instBuf = _actionsByGroup[action.ActionGroup][action];
                    foreach (var inst in instances)
                    {
                        instBuf.Add(inst);
                    }
                }
            }
            foreach (int grp in _validActionGroups)
            {
                _validActionGroupsList.Add(grp);
            }
            
            // 1. Randomly choose an action group
            // 2. Choose one action instance to perform from each action in the group
            if (_validActionGroupsList.Count > 0)
            {
                int grp = _validActionGroupsList[Random.Range(0, _validActionGroupsList.Count)];
                foreach (var entry in _actionsByGroup[grp])
                {
                    RGGameAction action = entry.Key;
                    var instances = entry.Value;
                    if (instances.Count > 0)
                    {
                        var chosenInst = instances[Random.Range(0, instances.Count)];
                        chosenInst.Perform(action.ParameterRange.RandomSample());
                    }
                }
            }
        }

        void OnDestroy()
        {
            RGActionManager.StopSession();
        }
    }
}