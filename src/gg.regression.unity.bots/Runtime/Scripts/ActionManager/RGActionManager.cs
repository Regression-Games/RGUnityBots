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

        public static IEnumerable<RGGameAction> Actions
        {
            get
            {
                // TODO
                yield break;
            }
        }

        public static IEnumerable<IRGGameActionInstance> GetValidActions()
        {
            // TODO
            yield break;
        }

        public static void StartSession(MonoBehaviour context)
        {
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

        private static void OnSceneLoad(Scene s, LoadSceneMode m)
        {
            // configure the event system for simulated input whenever a new scene is loaded
            RGUtils.SetupEventSystem();
        }
    }
}