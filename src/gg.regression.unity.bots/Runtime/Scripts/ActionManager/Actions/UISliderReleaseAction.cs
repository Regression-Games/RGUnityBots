﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    /// <summary>
    /// Action to release a Unity UI slider in the desired position,
    /// given that it is held on the current frame.
    /// </summary>
    public class UISliderReleaseAction : RGGameAction
    {
        public string NormalizedGameObjectName;
        
        public UISliderReleaseAction(string[] path, Type objectType, string normalizedGameObjectName) : 
            base(path, objectType, new RGFloatRange(0.0f, 1.0f))
        {
            Debug.Assert(typeof(Slider).IsAssignableFrom(objectType));
            NormalizedGameObjectName = normalizedGameObjectName;
        }

        public UISliderReleaseAction(JObject serializedAction) : base(serializedAction)
        {
            NormalizedGameObjectName = serializedAction["normalizedGameObjectName"].ToString();
        }

        public override string DisplayName => $"Release {NormalizedGameObjectName}";

        public override bool IsValidForObject(Object obj)
        {
            Slider slider = (Slider)obj;
            if (!RGActionManagerUtils.IsUIObjectInteractable(slider))
            {
                return false;
            }

            string normName = UIObjectPressAction.GetNormalizedGameObjectName(slider.gameObject.name);
            if (normName != NormalizedGameObjectName)
            {
                return false;
            }
            
            // at least one selectable on the slider must be pressed for us to be able to release the slider
            if (!slider.transform.GetComponentsInChildren<Selectable>().Any(RGActionManagerUtils.IsUIObjectPressed))
            {
                return false;
            }
            
            return true;
        }

        public override bool IsEquivalentTo(RGGameAction other)
        {
            if (base.IsEquivalentTo(other) && other is UISliderReleaseAction action)
            {
                return NormalizedGameObjectName == action.NormalizedGameObjectName;
            }
            return false;
        }

        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new UISliderReleaseInstance(this, obj);
        }

        protected override void WriteParametersToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append(",\n\"normalizedGameObjectName\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, NormalizedGameObjectName);
        }
        
        public override IEnumerable<(string, string)> GetDisplayActionAttributes()
        {
            foreach (var attr in base.GetDisplayActionAttributes())
                yield return attr;

            yield return ("Target Game Object", NormalizedGameObjectName);
        }
    }

    public class UISliderReleaseInstance : RGGameActionInstance<UISliderReleaseAction, float>
    {
        public UISliderReleaseInstance(UISliderReleaseAction action, Object targetObject) : base(action, targetObject)
        {
        }
        
        public static Image FindSliderBackground(Slider slider)
        {
            return slider.gameObject.transform.GetComponentInChildren<Image>();
        }

        private Vector2? GetMousePosForParam(float param)
        {
            Slider slider = (Slider)TargetObject;
            Image sliderBg = FindSliderBackground(slider);
            if (sliderBg == null)
                return null;
            var ssBounds = RGActionManagerUtils.GetUIScreenSpaceBounds(sliderBg.gameObject);
            if (!ssBounds.HasValue)
                return null;
            Vector2 min = ssBounds.Value.min;
            Vector2 max = ssBounds.Value.max;
            Vector2 center = ssBounds.Value.center;
            switch (slider.direction)
            {
                case Slider.Direction.LeftToRight:
                    return new Vector2(Mathf.Lerp(min.x, max.x, param), center.y);
                case Slider.Direction.RightToLeft:
                    return new Vector2(Mathf.Lerp(max.x, min.x, param), center.y);
                case Slider.Direction.BottomToTop:
                    return new Vector2(center.x, Mathf.Lerp(min.y, max.y, param));
                case Slider.Direction.TopToBottom:
                    return new Vector2(center.x, Mathf.Lerp(max.y, min.y, param));
                default:
                    throw new Exception("Unexpected slider direction " + slider.direction);
            }
        }

        protected override bool IsValidActionParameter(float param)
        {
            return true;
        }

        protected override IEnumerable<RGActionInput> GetActionInputs(float param)
        {
            Vector2? mousePos = GetMousePosForParam(param);
            if (mousePos.HasValue)
            {
                yield return new MousePositionInput(mousePos.Value);
                yield return new MouseButtonInput(MouseButtonInput.LEFT_MOUSE_BUTTON, false);
            }
        }
    }
}