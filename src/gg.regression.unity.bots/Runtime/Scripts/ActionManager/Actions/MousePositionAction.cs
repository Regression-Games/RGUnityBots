using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager.Actions
{
    public enum MousePositionType
    {
        NON_UI,      // All positions on the screen that are not covered by a UI element
        COLLIDER_2D, // All positions on the screen that hit a 2D collider
        COLLIDER_3D  // All positions on the screen that hit a 3D collider
    }
    
    /// <summary>
    /// Action to move the mouse cursor over non-UI parts of the screen. This specifically targets
    /// mouse position read by game object components via Input.mousePosition or Mouse.current.position,
    /// so it avoids any parts of the screen being covered by a UI element.
    /// </summary>
    public class MousePositionAction : RGGameAction
    {
        public MousePositionType PositionType { get; }
        
        // Layer masks to use if position type is either COLLIDER_2D or COLLIDER_3D (Note: ContactFilter not supported currently)
        public List<RGActionParamFunc<int>> LayerMasks { get; }
        
        public MousePositionAction(string[] path, Type objectType) : 
            base(path, objectType, new RGVector2Range(Vector2.zero, Vector2.one))
        {
            PositionType = MousePositionType.NON_UI;
            LayerMasks = null;
        }
        
        public MousePositionAction(string[] path, MousePositionType positionType, List<RGActionParamFunc<int>> layerMasks, Type objectType) : 
            base(path, objectType, new RGVector2Range(Vector2.zero, Vector2.one))
        {
            PositionType = positionType;
            LayerMasks = layerMasks;
        }

        public MousePositionAction(JObject serializedAction) :
            base(serializedAction)
        {
            if (serializedAction.TryGetValue("positionType", out var positionType))
            {
                PositionType = Enum.Parse<MousePositionType>(positionType.ToString());
            }
            else
            {
                PositionType = MousePositionType.NON_UI;
            }
            if (serializedAction.TryGetValue("layerMasks", out var layerMasks))
            {
                JArray layerMasksArr = (JArray)layerMasks;
                LayerMasks = new List<RGActionParamFunc<int>>();
                foreach (var layerMask in layerMasksArr)
                {
                    LayerMasks.Add(RGActionParamFunc<int>.Deserialize(layerMask));
                }
            }
            else
            {
                LayerMasks = null;
            }
        }

        public override string DisplayName
        {
            get
            {
                string maskDisplay = LayerMasks != null ? " (Mask " + string.Join(" | ", LayerMasks) + ")" : "";
                switch (PositionType)
                {
                    case MousePositionType.NON_UI:
                        return "Mouse Position";
                    case MousePositionType.COLLIDER_2D:
                        return "Mouse Over 2D Collider" + maskDisplay;
                    case MousePositionType.COLLIDER_3D:
                        return "Mouse Over 3D Collider" + maskDisplay;
                    default:
                        throw new ArgumentException();
                }
            }
        }

        public override bool IsValidForObject(Object obj)
        {
            return true;
        }

        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new MousePositionInstance(this, obj);
        }
        
        public override bool IsEquivalentTo(RGGameAction other)
        {
            if (other is MousePositionAction mousePosAction && base.IsEquivalentTo(other))
            {
                if (mousePosAction.PositionType != PositionType)
                {
                    return false;
                }
                if ((mousePosAction.LayerMasks == null && LayerMasks != null) ||
                    (mousePosAction.LayerMasks != null && LayerMasks == null))
                {
                    return false;
                }
                if (LayerMasks != null && !LayerMasks.SequenceEqual(mousePosAction.LayerMasks))
                {
                    return false;
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        protected override void WriteParametersToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append(",\n\"positionType\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, PositionType.ToString());
            if (LayerMasks != null)
            {
                stringBuilder.Append(",\n\"layerMasks\":[\n");
                int layerMasksCount = LayerMasks.Count;
                for (int i = 0; i < layerMasksCount; ++i)
                {
                    LayerMasks[i].WriteToStringBuilder(stringBuilder);
                    if (i + 1 < layerMasksCount)
                    {
                        stringBuilder.Append(",\n");
                    }
                }
                stringBuilder.Append("\n]");
            }
        }
        
        public override IEnumerable<(string, string)> GetDisplayActionAttributes()
        {
            foreach (var attr in base.GetDisplayActionAttributes())
                yield return attr;

            yield return ("Position Type", PositionType.ToString());

            if (LayerMasks != null)
            {
                yield return ("Layer Mask", string.Join(" | ", LayerMasks));
            }
        }
    }

    public class MousePositionInstance : RGGameActionInstance<MousePositionAction, Vector2>
    {
        public MousePositionInstance(MousePositionAction action, Object targetObject) : base(action, targetObject)
        {
        }

        private int? GetLayerMask()
        {
            if (Action.LayerMasks != null)
            {
                return Action.LayerMasks.Aggregate(0, (mask, func) => mask | func.Invoke(TargetObject));
            }
            else
            {
                return null;
            }
        }

        protected override bool IsValidActionParameter(Vector2 param)
        {
            Vector2 mousePos = new Vector2(Screen.width * param.x, Screen.height * param.y);
            if (RGActionManagerUtils.IsMouseOverUI(mousePos, out _))
            {
                return false;
            }
            if (Action.PositionType == MousePositionType.COLLIDER_2D)
            {
                int layerMask = GetLayerMask() ?? Physics2D.DefaultRaycastLayers;
                return RGActionManagerUtils.IsMouseOverCollider2D(mousePos, layerMask);
            } else if (Action.PositionType == MousePositionType.COLLIDER_3D)
            {
                int layerMask = GetLayerMask() ?? Physics.DefaultRaycastLayers;
                return RGActionManagerUtils.IsMouseOverCollider3D(mousePos, layerMask);
            }
            else
            {
                return true;
            }
        }

        protected override IEnumerable<RGActionInput> GetActionInputs(Vector2 param)
        {
            Vector2 mousePos = new Vector2(Screen.width * param.x, Screen.height * param.y);
            yield return new MousePositionInput(mousePos);
        }
    }
}
