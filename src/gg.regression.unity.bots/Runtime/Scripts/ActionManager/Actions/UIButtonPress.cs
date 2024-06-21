using System;
using System.Linq;
using RegressionGames.RGLegacyInputUtility;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.Models;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    public class UIButtonPress : RGGameAction
    {
        public UIButtonPress(string path, Type objectType) : base(path, objectType)
        {
            Debug.Assert(typeof(Button).IsAssignableFrom(objectType));
        }

        public override IRGValueRange ParameterRange { get; } = new RGVoidRange();
        
        public override bool IsValidForObject(Object obj)
        {
            if (RGLegacyInputWrapper.GetKey(KeyCode.Mouse0))
            {
                return false;
            }
            Button btn = (Button)obj;
            GameObject gameObject = btn.gameObject;
            TransformStatus tStatus = TransformStatus.GetOrCreateTransformStatus(gameObject.transform);
            return Path == tStatus.NormalizedPath + "/Press";
        }

        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new UIButtonClickInstance(this, obj);
        }
    }

    public class UIButtonClickInstance : RGGameActionInstance<UIButtonPress, object>
    {
        private static Vector3[] _worldCorners = new Vector3[4];
        
        public UIButtonClickInstance(UIButtonPress action, Object targetObject) : base(action, targetObject)
        {
        }

        protected override void PerformAction(object param)
        {
            Button targetBtn = (Button)TargetObject;
            Canvas canvas = targetBtn.gameObject.GetComponentInParent<Canvas>();
            Camera canvasCamera = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            var instId = targetBtn.transform.GetInstanceID();
            if (RGActionManager.CurrentUITransforms.TryGetValue(instId, out TransformStatus tStatus))
            {
                Bounds? bounds = tStatus.screenSpaceBounds;
                if (bounds.HasValue)
                {
                    Bounds boundsVal = bounds.Value;
                    RGActionManager.SimulateMouseMovement(boundsVal.center);
                    RGActionManager.SimulateLeftMouseButton(true);
                }
            }
        }
    }
}