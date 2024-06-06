
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RegressionGames.RGLegacyInputUtility
{
    /**
     * This UI module combines input from the playback simulation and
     * user input to ensure that the user can continue to interact with
     * the Regression Games overlay while playback is active.
     */
    public class RGUIInputModule : BaseInputModule
    {
        private List<BaseInputModule> _modulesBuf;

        public RGUIInputModule()
        {
            _modulesBuf = new List<BaseInputModule>();
            Debug.Log("Created RGUIInputModule");
        }
        
        public override void UpdateModule()
        {
            foreach (var module in GetInputModulesToCombine())
            {
                module.UpdateModule();
            }
        }

        public override bool IsModuleSupported()
        {
            return GetInputModulesToCombine().Any(module => module.IsModuleSupported());
        }

        public override bool ShouldActivateModule()
        {
            return true;
        }

        public override void ActivateModule()
        {
            foreach (var module in GetInputModulesToCombine())
            {
                module.ActivateModule();
            }
        }

        public override void DeactivateModule()
        {
            foreach (var module in GetInputModulesToCombine())
            {
                module.DeactivateModule();
            }
        }
        
        public override void Process()
        {
            foreach (var module in GetInputModulesToCombine())
            {
                module.Process();
            }
        }

        private IEnumerable<BaseInputModule> GetInputModulesToCombine()
        {
            _modulesBuf.Clear();
            GetComponents(_modulesBuf);
            bool playbackActive = !RGLegacyInputWrapper.IsPassthrough;
            if (playbackActive)
            {
                foreach (BaseInputModule module in _modulesBuf)
                {
                    if (module != this && !module.isActiveAndEnabled)
                    {
                        yield return module;
                    }
                }
            }
            else
            {
                foreach (BaseInputModule module in _modulesBuf)
                {
                    if (module != this && module.inputOverride != null && module.inputOverride is RGBaseInput)
                    {
                        yield return module;
                    }
                }
            }
        }
    }
}