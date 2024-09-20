#if ENABLE_LEGACY_INPUT_MANAGER
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

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
            return base.IsModuleSupported() && (GetPlaybackInputModule()?.IsModuleSupported() ?? true);
        }

        public override bool ShouldActivateModule()
        {
            return base.ShouldActivateModule() || (GetPlaybackInputModule()?.ShouldActivateModule() ?? true);
        }

        public override void ActivateModule()
        {
            base.ActivateModule();
            GetPlaybackInputModule()?.ActivateModule();
        }

        public override void DeactivateModule()
        {
            base.DeactivateModule();
            GetPlaybackInputModule()?.DeactivateModule();

        }
        public override void UpdateModule()
        {
            // When playback is NOT active, then the playback module is just reading input from the user as usual,
            // so we don't need this input module to be reading user input.
            if (IsPlaybackActive())
            {
                base.UpdateModule();
            }
            GetPlaybackInputModule()?.UpdateModule();
        }

        public override void Process()
        {
            if (IsPlaybackActive())
            {
                base.Process();
            }
            GetPlaybackInputModule()?.Process();
        }

        public override bool IsPointerOverGameObject(int pointerId)
        {
            if (pointerId == -1)
            {
                return RGLegacyInputWrapper.IsLeftMouseButtonPointerCurrentlyOverGameObject();
            }
            return base.IsPointerOverGameObject(pointerId);
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

            // during teardown, the object.destroy doesn't process immediately, so we have to be tolerant of this maybe being null for the rest of the frame
            return null;
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
#endif
