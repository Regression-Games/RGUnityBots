﻿using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    /// <summary>
    /// Action to press down on a Unity UI slider or scrollbar.
    /// This action is only for pressing down on the slider, and can be
    /// followed by a UISliderReleaseAction on the next frame to release the slider.
    ///
    /// The position of the mouse over the slider is defined by the float parameter
    /// to the action ranging from 0 to 1.
    /// </summary>
    public class UISliderPressAction : RGGameAction
    {
        [RGActionProperty("Target Game Object", false)]
        public string NormalizedGameObjectName;
        
        public UISliderPressAction(string[] path, Type objectType, string normalizedGameObjectName) : 
            base(path, objectType, new RGFloatRange(0.0f, 1.0f))
        {
            if (!(typeof(Slider).IsAssignableFrom(objectType) 
                  || typeof(Scrollbar).IsAssignableFrom(objectType)))
            {
                throw new ArgumentException($"Expected Slider or Scrollbar, received {objectType}");
            }
            NormalizedGameObjectName = normalizedGameObjectName;
        }

        public UISliderPressAction(JObject serializedAction) : base(serializedAction)
        {
            NormalizedGameObjectName = serializedAction["normalizedGameObjectName"].ToString();
        }

        public static IEnumerable<CanvasRenderer> FindSliderRenderables(Selectable slider)
        {
            foreach (var canvasRenderer in slider.gameObject.transform.GetComponentsInChildren<CanvasRenderer>())
            {
                yield return canvasRenderer;
            }
        }

        public override string DisplayName => $"Press {NormalizedGameObjectName}";

        public override bool IsValidForObject(Object obj)
        {
            Selectable slider = (Selectable)obj;
            
            if (!RGActionManagerUtils.IsUIObjectInteractable(slider))
                return false;

            string normName = UIObjectPressAction.GetNormalizedGameObjectName(slider.gameObject.name);
            if (normName != NormalizedGameObjectName)
                return false;

            // if the slider is already pressed, we can't press it again until it is released
            if (RGActionManagerUtils.IsUIObjectPressed(slider))
                return false;

            bool haveMousePos = false;
            foreach (var renderer in FindSliderRenderables(slider))
            {
                // check all child selectables for whether they are pressed as well
                foreach (var selectable in renderer.gameObject.GetComponents<Selectable>())
                {
                    if (RGActionManagerUtils.IsUIObjectPressed(selectable))
                        return false;
                }
                    
                haveMousePos = RGActionManagerUtils.GetUIMouseHitPosition(renderer.gameObject, out _);
                if (haveMousePos)
                    break;
            }

            return haveMousePos;
        }

        public override bool IsEquivalentTo(RGGameAction other)
        {
            if (base.IsEquivalentTo(other) && other is UISliderPressAction action)
            {
                return NormalizedGameObjectName == action.NormalizedGameObjectName;
            }
            return false;
        }

        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new UISliderPressInstance(this, obj);
        }

        protected override void WriteParametersToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append(",\n\"normalizedGameObjectName\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, NormalizedGameObjectName);
        }
    }

    public class UISliderPressInstance : RGGameActionInstance<UISliderPressAction, float>
    {
        public UISliderPressInstance(UISliderPressAction action, Object targetObject) : base(action, targetObject)
        {
        }

        protected override bool IsValidActionParameter(float param)
        {
            return true;
        }

        protected override IEnumerable<RGActionInput> GetActionInputs(float param)
        {
            Selectable slider = (Selectable)TargetObject;
            Vector2? mousePos = UISliderReleaseInstance.GetMousePosForParam(param, slider);
            if (mousePos.HasValue)
            {
                yield return new MousePositionInput(mousePos.Value);
                yield return new MouseButtonInput(MouseButtonInput.LEFT_MOUSE_BUTTON, true);
            }
        }
    }
}