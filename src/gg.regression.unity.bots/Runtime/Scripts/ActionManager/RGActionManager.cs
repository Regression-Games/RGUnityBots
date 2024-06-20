using System;
using System.Collections.Generic;
using RegressionGames.RGLegacyInputUtility;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RegressionGames.ActionManager
{
    public static class RGActionManager
    {
        private static MonoBehaviour _context;
        private static IRGActionProvider _actionProvider;

        public static IEnumerable<RGGameAction> Actions => _actionProvider.Actions;

        public static void SetActionProvider(IRGActionProvider actionProvider)
        {
            _actionProvider = actionProvider;
        }

        public static void StartSession(MonoBehaviour context)
        {
            if (_actionProvider == null)
            {
                throw new Exception("Must set an action provider before starting a session");
            }
            if (_context != null)
            {
                throw new Exception($"Session is already active with context {_context}");
            }
            _context = context;
            RGLegacyInputWrapper.StartSimulation(_context);
            SceneManager.sceneLoaded += OnSceneLoad;
            RGUtils.SetupEventSystem();
        }

        public static void StopSession()
        {
            if (_context != null)
            {
                SceneManager.sceneLoaded -= OnSceneLoad;
                RGLegacyInputWrapper.StopSimulation();
                _context = null;
            }
        }
        
        public static IEnumerable<IRGGameActionInstance> GetValidActions()
        {
            foreach (RGGameAction action in Actions)
            {
                UnityEngine.Object[] objects = UnityEngine.Object.FindObjectsOfType(action.ObjectType);
                foreach (var obj in objects)
                {
                    if (action.IsValidForObject(obj))
                    {
                        yield return action.GetInstance(obj);
                    }
                }
            }
        }

        private static void OnSceneLoad(Scene s, LoadSceneMode m)
        {
            // configure the event system for simulated input whenever a new scene is loaded
            RGUtils.SetupEventSystem();
        }
    }
}