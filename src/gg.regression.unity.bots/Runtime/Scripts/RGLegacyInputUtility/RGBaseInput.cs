using UnityEngine;
using UnityEngine.EventSystems;

namespace RegressionGames.RGLegacyInputUtility
{
    /**
     * Any new Input wrappers added to RGLegacyInputWrapper should also be reflected here
     * by overriding the appropriate method in BaseInput.
     */
    public class RGBaseInput : BaseInput
    {
        public override bool mousePresent => RGLegacyInputWrapper.mousePresent;

        public override Vector2 mousePosition => RGLegacyInputWrapper.mousePosition;

        public override Vector2 mouseScrollDelta => RGLegacyInputWrapper.mouseScrollDelta;

        public override bool GetMouseButtonDown(int button)
        {
            return RGLegacyInputWrapper.GetMouseButtonDown(button);
        }

        public override bool GetMouseButtonUp(int button)
        {
            return RGLegacyInputWrapper.GetMouseButtonUp(button);
        }

        public override bool GetMouseButton(int button)
        {
            return RGLegacyInputWrapper.GetMouseButton(button);
        }

        public override float GetAxisRaw(string axisName)
        {
            return RGLegacyInputWrapper.GetAxisRaw(axisName);
        }

        public override bool GetButtonDown(string buttonName)
        {
            return RGLegacyInputWrapper.GetButtonDown(buttonName);
        }
    }
}