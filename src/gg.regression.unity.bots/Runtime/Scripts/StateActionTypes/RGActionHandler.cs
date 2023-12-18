using System;
using System.Collections.Generic;
using UnityEngine;

// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
namespace RegressionGames.StateActionTypes
{
    public sealed class RGActionHandler : MonoBehaviour
    {
        private readonly Dictionary<string, Delegate> _actionRequestDelegateMap = new ();
        
        public void Start()
        {
            // when this object is started / attached to the gameObject... populate all the available methods on that game Object
            
            // find all the MonoBehaviour and load their actions
            var monoBehaviours = GetComponents<MonoBehaviour>();

            foreach (var monoBehaviour in monoBehaviours)
            {
                var actionTypes = BehavioursWithStateOrActions.GetRGActionsMappingForBehaviour(monoBehaviour);
                foreach (var (actionName, dDelegate) in actionTypes.ActionRequestDelegates)
                {
                    if (_actionRequestDelegateMap.ContainsKey(actionName))
                    {
                        //TODO (REG-1420): It would be nice to give them a context log to the correct gameObject
                        RGDebug.LogError($"Error: GameObject with MonoBehaviour: {monoBehaviour.GetType().Name} has multiple MonoBehaviours that define an action with name: {actionName}");
                    }

                    _actionRequestDelegateMap[actionName] = dDelegate;
                }
            }
            
            // load any custom actionbehaviours on this gameObject
            var customActions = GetComponents<RGActionBehaviour>();
            foreach (var customAction in customActions)
            {
                var actionName = customAction.GetActionName();
                if (_actionRequestDelegateMap.ContainsKey(actionName))
                {
                    //TODO (REG-1420): It would be nice to give them a context log to the correct gameObject
                    RGDebug.LogError($"Error: GameObject with MonoBehaviour: {customAction.GetType().Name} has multiple MonoBehaviours that define an action with name: {actionName}");
                }

                _actionRequestDelegateMap[actionName] = new Action<RGActionRequest>(customAction.Invoke);
            }
        }

        public void Invoke(RGActionRequest request)
        {
            if (_actionRequestDelegateMap.TryGetValue(request.Action, out var dDelegate))
            {
                dDelegate.DynamicInvoke(gameObject, request);
            }
            else
            {
                //TODO (REG-1420): It would be nice to give them a context log to the correct gameObject
                RGDebug.LogWarning($"Warning: Attempted to perform action name: {request.Action} on a GameObject with no MonoBehaviour that provides that action.");
            }
        }

    }
}
