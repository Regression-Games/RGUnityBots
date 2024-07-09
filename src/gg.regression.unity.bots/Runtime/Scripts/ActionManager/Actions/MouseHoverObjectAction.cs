using System;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using RegressionGames.StateRecorder.Models;

namespace RegressionGames.ActionManager.Actions
{
    /// <summary>
    /// This action is used to move the mouse to the center of an object's screen space bounding box (if true),
    /// or move the mouse outside the bounds of an object (if false).
    /// This is used to trigger events such as OnMouseOver(), OnMouseEnter(), and OnMouseExit().
    /// </summary>
    public class MouseHoverObjectAction : RGGameAction
    {
        public MouseHoverObjectAction(string[] path, Type objectType, int actionGroup) : base(path, objectType, actionGroup)
        {
        }

        public override IRGValueRange ParameterRange { get; } = new RGBoolRange();

        public override bool IsValidForObject(Object obj)
        {
            return MouseHoverObjectInstance.GetHoverScreenSpaceBounds(obj).HasValue;
        }

        public override IRGGameActionInstance GetInstance(Object obj)
        {
            return new MouseHoverObjectInstance(this, obj);
        }
    }

    public class MouseHoverObjectInstance : RGGameActionInstance<MouseHoverObjectAction, bool>
    {
        public MouseHoverObjectInstance(MouseHoverObjectAction action, Object targetObject) : base(action, targetObject)
        {
        }

        public static Bounds? GetHoverScreenSpaceBounds(Object targetObject)
        {
            Component c = (Component)targetObject;
            GameObject gameObject = c.gameObject;
            var instId = gameObject.transform.GetInstanceID();
            Bounds? ssBounds = null;
            ObjectStatus tStatus;
            if (RGActionManager.CurrentTransforms.TryGetValue(instId, out tStatus))
            {
                ssBounds = tStatus.screenSpaceBounds;
            }

            if (!ssBounds.HasValue)
            {
                var camera = Camera.main;
                if (camera != null)
                {
                    Bounds? colliderBounds = null;
                    if (gameObject.TryGetComponent(out Collider col3D))
                    {
                        colliderBounds = col3D.bounds;
                    } else if (gameObject.TryGetComponent(out Collider2D col2D))
                    {
                        colliderBounds = col2D.bounds;
                    }

                    if (colliderBounds.HasValue)
                    {
                        Vector3 min = colliderBounds.Value.min;
                        Vector3 max = colliderBounds.Value.max;
                        Vector3[] points = new Vector3[8]
                        {
                            new Vector3(min.x, min.y, min.z),
                            new Vector3(max.x, min.y, min.z),
                            new Vector3(min.x, max.y, min.z),
                            new Vector3(max.x, max.y, min.z),
                            new Vector3(min.x, min.y, max.z),
                            new Vector3(max.x, min.y, max.z),
                            new Vector3(min.x, max.y, max.z),
                            new Vector3(max.x, max.y, max.z)
                        };
                        Vector3 minScreen = new Vector3(float.PositiveInfinity, float.PositiveInfinity, 0.0f);
                        Vector3 maxScreen = new Vector3(float.NegativeInfinity, float.NegativeInfinity, 0.0f);
                        foreach (Vector3 pt in points)
                        {
                            Vector3 screenPt = camera.WorldToScreenPoint(pt);
                            screenPt.x = screenPt.x / camera.pixelWidth * Screen.width;
                            screenPt.y = screenPt.y / camera.pixelHeight * Screen.height;
                            minScreen = Vector3.Min(minScreen, screenPt);
                            maxScreen = Vector3.Max(maxScreen, screenPt);
                        }
                        ssBounds = new Bounds((minScreen + maxScreen) / 2.0f, maxScreen - minScreen);
                    }
                }
            }

            return ssBounds;
        }

        public static Vector2 GetPointOutsideBounds(Bounds ssBounds)
        {
            Bounds screenRect = new Bounds(
                new Vector3(Screen.width/2.0f, Screen.height/2.0f, 0.0f),
                      new Vector3(Screen.width, Screen.height, 0.0f));
            float extentScale = 1.5f;
            Vector2[] candidates = new Vector2[]
            {
                ssBounds.center + new Vector3(ssBounds.extents.x*extentScale, 0.0f, 0.0f),
                ssBounds.center + new Vector3(-ssBounds.extents.x*extentScale, 0.0f, 0.0f),
                ssBounds.center + new Vector3(0.0f, ssBounds.extents.y*extentScale, 0.0f),
                ssBounds.center + new Vector3(0.0f, -ssBounds.extents.y*extentScale, 0.0f)
            };
            // first try to find a point outside the bounds that are on the screen
            foreach (Vector2 pt in candidates)
            {
                if (screenRect.Contains(pt))
                {
                    return pt;
                }
            }
            // if no such point found, just return the first point anyways and have the mouse be off-screen
            return candidates[0];
        }

        protected override void PerformAction(bool param)
        {
            var ssBounds = GetHoverScreenSpaceBounds(TargetObject);
            if (ssBounds.HasValue)
            {
                if (param)
                {
                    RGActionManager.SimulateMouseMovement(ssBounds.Value.center);
                }
                else
                {
                    RGActionManager.SimulateMouseMovement(GetPointOutsideBounds(ssBounds.Value));
                }
            }
        }
    }
}
