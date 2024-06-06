
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RegressionGames.RGLegacyInputUtility
{
    /**
     * This input module combines input from both the playback input module
     * and user device input, to ensure that the user can continue to
     * interact with the Regression Games overlay while playback is active.
     *
     * This module expects that the playback module is attached to the same game object,
     * has an RGBaseInput input override, and is disabled.
     */
    public class RGStandaloneInputModule : StandaloneInputModule
    {
        private List<BaseInputModule> _modulesBuf;

        public RGStandaloneInputModule()
        {
            _modulesBuf = new List<BaseInputModule>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SendMessageToPlaybackInputModule("OnEnable");
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            SendMessageToPlaybackInputModule("OnDisable");
        }
        
        public override bool IsModuleSupported()
        {
            return base.IsModuleSupported() && GetPlaybackInputModule().IsModuleSupported();
        }

        public override bool ShouldActivateModule()
        {
            return base.ShouldActivateModule() || GetPlaybackInputModule().ShouldActivateModule();
        }

        public override void ActivateModule()
        {
            base.ActivateModule();
            GetPlaybackInputModule().ActivateModule();
        }

        public override void DeactivateModule()
        {
            base.DeactivateModule();
            GetPlaybackInputModule().DeactivateModule();
            
        }
        public override void UpdateModule()
        {
            // When playback is NOT active, then the playback module is just reading input from the user as usual,
            // so we don't need this input module to be reading user input. 
            if (IsPlaybackActive())
            {
                base.UpdateModule();
            }
            GetPlaybackInputModule().UpdateModule();
        }
        
        public override void Process()
        {
            if (IsPlaybackActive())
            {
                base.Process();
            }
            GetPlaybackInputModule().Process();
        }

        private bool IsPlaybackActive()
        {
            return !RGLegacyInputWrapper.IsPassthrough;
        }

        private BaseInputModule GetPlaybackInputModule()
        {
            _modulesBuf.Clear();
            GetComponents(_modulesBuf);
            foreach (BaseInputModule module in _modulesBuf)
            {
                if (!module.isActiveAndEnabled && module.inputOverride != null && module.inputOverride is RGBaseInput)
                {
                    return module;
                }
            }
            throw new Exception("Missing playback input module (must be disabled and overridden with RGBaseInput)");
        }

        private void SendMessageToPlaybackInputModule(string methodName)
        {
            var module = GetPlaybackInputModule();
            var handlerMethod = module.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (handlerMethod != null)
            {
                handlerMethod.Invoke(module, Array.Empty<object>());
            }
        }
    }
}