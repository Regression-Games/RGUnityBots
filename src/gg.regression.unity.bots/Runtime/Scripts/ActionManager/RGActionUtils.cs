
using System;
using RegressionGames.RGLegacyInputUtility;
using UnityEngine;

namespace RegressionGames.ActionManager
{
    public static class RGActionUtils
    {
        #if ENABLE_LEGACY_INPUT_MANAGER
        public static void SimulateLegacyKeyState(KeyCode keyCode, bool isPressed)
        {
            if (isPressed)
            {
                RGLegacyInputWrapper.SimulateKeyPress(keyCode);
            }
            else
            {
                RGLegacyInputWrapper.SimulateKeyRelease(keyCode);
            }
            
            #if ENABLE_INPUT_SYSTEM
            // TODO
            throw new NotImplementedException();
            #endif
        }
        #endif
    }
}