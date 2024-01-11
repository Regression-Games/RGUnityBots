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
        private readonly Dictionary<string, Delegate> _actionRequestGameObjectDelegateMap = new ();

        private bool loaded;

        private bool isOverlayComponent;
        
        public void Start()
        {
            if (loaded)
            {
                return;
            }

            isOverlayComponent = GetComponents<RGBotServerListener>() != null;
            
            // when this object is started / attached to the gameObject... populate all the available methods on that game Object
            
            // find all the MonoBehaviour and load their actions
            var monoBehaviours = GetComponents<MonoBehaviour>();

            foreach (var monoBehaviour in monoBehaviours)
            {
                var actionTypes = BehavioursWithStateOrActions.GetRGActionsMappingForBehaviour(monoBehaviour);
                if (actionTypes == null)
                {
                    continue;
                }

                foreach (var (actionName, dDelegate) in actionTypes.ActionRequestDelegates)
                {
                    if (_actionRequestGameObjectDelegateMap.ContainsKey(actionName))
                    {
                        RGDebug.LogError(
                            $"Error: GameObject with MonoBehaviour: {monoBehaviour.GetType().Name} has multiple MonoBehaviours that define an action with name: {actionName}", gameObject);
                    }

                    _actionRequestGameObjectDelegateMap[actionName] = dDelegate;
                }
            }
            
            // load any custom action behaviours on this gameObject
            var customActions = GetComponents<RGActionBehaviour>();
            foreach (var customAction in customActions)
            {
                var actionName = customAction.GetActionName();
                if (_actionRequestDelegateMap.ContainsKey(actionName))
                {
                    RGDebug.LogError($"Error: GameObject with MonoBehaviour: {customAction.GetType().Name} has multiple MonoBehaviours that define an action with name: {actionName}", gameObject);
                }

                _actionRequestDelegateMap[actionName] = new Action<RGActionRequest>(customAction.Invoke);
            }
            loaded = true;
        }

        public void Invoke(RGActionRequest request)
        {
            if (_actionRequestGameObjectDelegateMap.TryGetValue(request.Action, out var goDelegate))
            {
                try
                {
                    goDelegate.DynamicInvoke(gameObject, request);
                }
                catch (Exception e)
                {
                    RGDebug.LogException(e, $"Exception invoking action name: {request.Action} of type: {request.GetType().FullName}");
                }
            }
            else if (_actionRequestDelegateMap.TryGetValue(request.Action, out var dDelegate))
            {
                try
                {
                    dDelegate.DynamicInvoke(request);
                }
                catch (Exception e)
                {
                    RGDebug.LogException(e, $"Exception invoking action name: {request.Action} of type: {request.GetType().FullName}");
                }
            }
            else
            {
                if (!isOverlayComponent)
                {
                    RGDebug.LogWarning(
                        $"Warning: Attempted to perform action name: {request.Action} on a GameObject with no MonoBehaviour that provides that action.", gameObject);
                }
            }
        }

    }
}
