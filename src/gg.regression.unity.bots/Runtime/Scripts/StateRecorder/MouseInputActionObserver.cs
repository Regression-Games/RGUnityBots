using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RegressionGames.StateRecorder
{

    public class MouseInputActionDataJsonConverter : JsonConverter<MouseInputActionData>
    {
        public override void WriteJson(JsonWriter writer, MouseInputActionData value, JsonSerializer serializer)
        {
            writer.WriteRawValue(value.ToJsonString());
        }

        public override bool CanRead => false;

        public override MouseInputActionData ReadJson(JsonReader reader, Type objectType, MouseInputActionData existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [JsonConverter(typeof(MouseInputActionDataJsonConverter))]
    public class MouseInputActionData
    {
        public double startTime;

        public Vector2Int position;

        public Vector3? worldPosition;

        // non-fractional pixel accuracy
        //main 5 buttons
        public bool leftButton;
        public bool middleButton;
        public bool rightButton;
        public bool forwardButton;
        public bool backButton;

        // scroll wheel
        public Vector2 scroll;

        public int[] clickedObjectIds;

        public bool IsButtonClicked => leftButton || middleButton || rightButton || forwardButton || backButton || Math.Abs(scroll.y) > 0.1f || Math.Abs(scroll.x) > 0.1f;

        public bool PositionsEqual(object obj)
        {
            if (obj is MouseInputActionData previous)
            {
                return (previous.position.x) == (this.position.x)
                       && (previous.position.y) == (this.position.y);
            }

            return false;
        }

        public bool ButtonStatesEqual(object obj)
        {
            if (obj is MouseInputActionData previous)
            {
                return previous.leftButton == this.leftButton
                       && previous.middleButton == this.middleButton
                       && previous.rightButton == this.rightButton
                       && previous.forwardButton == this.forwardButton
                       && previous.backButton == this.backButton
                       && Math.Abs(previous.scroll.y - this.scroll.y) < 0.1f
                       && Math.Abs(previous.scroll.x - this.scroll.x) < 0.1f;
            }

            return false;
        }

        // re-usable and large enough to fit ball sizes
        private static readonly StringBuilder _stringBuilder = new StringBuilder(5_000);

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"startTime\":");
            DoubleJsonConverter.WriteToStringBuilder(stringBuilder, startTime);
            stringBuilder.Append(",\"position\":");
            VectorIntJsonConverter.WriteToStringBuilder(stringBuilder, position);
            stringBuilder.Append(",\"worldPosition\":");
            VectorJsonConverter.WriteToStringBuilderVector3Nullable(stringBuilder, worldPosition);
            stringBuilder.Append(",\"leftButton\":");
            stringBuilder.Append(leftButton ? "true" : "false");
            stringBuilder.Append(",\"middleButton\":");
            stringBuilder.Append(middleButton ? "true" : "false");
            stringBuilder.Append(",\"rightButton\":");
            stringBuilder.Append( (rightButton ? "true" : "false"));
            stringBuilder.Append(",\"forwardButton\":");
            stringBuilder.Append( (forwardButton ? "true" : "false"));
            stringBuilder.Append(",\"backButton\":");
            stringBuilder.Append( (backButton ? "true" : "false"));
            stringBuilder.Append(",\"scroll\":");
            VectorJsonConverter.WriteToStringBuilderVector2(stringBuilder, scroll);
            stringBuilder.Append(",\"clickedObjectIds\":[");
            var clickedObjectIdsLength = clickedObjectIds.Length;
            for (var i = 0; i < clickedObjectIdsLength; i++)
            {
                IntJsonConverter.WriteToStringBuilder(stringBuilder, clickedObjectIds[i]);
                if (i + 1 < clickedObjectIdsLength)
                {
                    stringBuilder.Append(",");
                }
            }
            stringBuilder.Append("]}");
        }

        internal string ToJsonString()
        {
            _stringBuilder.Clear();
            WriteToStringBuilder(_stringBuilder);
            return _stringBuilder.ToString();
        }
    }

    public class MouseInputActionObserver : MonoBehaviour
    {
        private MouseInputActionData _priorMouseState;

        // limit this to 5 hits... any hit should be good enough to guess the proper screen x,y based on precise world x,y,z .. but sometimes
        // we get slight positional variances in games from run to run and need to account for edge cases where objects get hidden by other objects
        // based on a few pixel shift in relative camera position
        private readonly RaycastHit[] _cachedRaycastHits = new RaycastHit[5];

        public void ObserveMouse(List<RecordedGameObjectState> statefulObjects)
        {
            var mousePosition = Mouse.current.position.ReadValue();
            var newMouseState = GetCurrentMouseState(mousePosition);
            if (newMouseState != null)
            {
                if (_priorMouseState == null && newMouseState.IsButtonClicked || _priorMouseState != null && !_priorMouseState.ButtonStatesEqual(newMouseState))
                {
                    Vector3? worldPosition = null;
                    var clickedOnObjects = FindObjectsAtPosition(newMouseState.position, statefulObjects, out var maxZDepth);

                    var mouseRayHits = 0;


                    var ray = Camera.main.ScreenPointToRay(mousePosition);
                    mouseRayHits = Physics.RaycastNonAlloc(ray,
                        _cachedRaycastHits,
                        maxZDepth+0.1f);


                    var comparer = Comparer<RaycastHit>.Create(
                        (x1, x2) =>
                        {
                            var d = x1.distance - x2.distance;
                            return (d > 0) ? 1 : (d < 0) ? -1 : 0;
                        } );

                    if (mouseRayHits > 0)
                    {
                        // order by distance from camera
                        Array.Sort(_cachedRaycastHits, 0, mouseRayHits, comparer);
                    }

                    List<int> clickedOnObjectIds = new();
                    var bestIndex = int.MaxValue;
                    if (mouseRayHits > 0)
                    {
                        foreach (var recordedGameObjectState in clickedOnObjects)
                        {
                            clickedOnObjectIds.Add(recordedGameObjectState.id);
                            if (recordedGameObjectState.worldSpaceBounds != null)
                            {
                                // compare to any raycast hits and pick the one closest to the camera
                                if (bestIndex > 0)
                                {
                                    for (var i = 0; i < mouseRayHits; i++)
                                    {
                                        var rayHit = _cachedRaycastHits[i];
                                        try
                                        {
                                            if (_cachedRaycastHits[i].transform.GetInstanceID() == recordedGameObjectState.id)
                                            {
                                                if (i < bestIndex)
                                                {
                                                    worldPosition = _cachedRaycastHits[i].point;
                                                    bestIndex = i;
                                                }

                                                break;
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            RGDebug.LogException(e, "Exception handling raycast hits for in game object click");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // without a collider hit, we can't set a worldPosition
                        foreach (var recordedGameObjectState in clickedOnObjects)
                        {
                            clickedOnObjectIds.Add(recordedGameObjectState.id);
                        }
                    }

                    newMouseState.worldPosition = worldPosition;
                    newMouseState.clickedObjectIds = clickedOnObjectIds.ToArray();
                    _mouseInputActions.Enqueue(newMouseState);
                }
                else if (_priorMouseState?.PositionsEqual(newMouseState) != true)
                {
                    _mouseInputActions.Enqueue(newMouseState);
                }

                _priorMouseState = newMouseState;
            }
        }

        public MouseInputActionData GetCurrentMouseState(Vector2? position = null)
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                position ??= mouse.position.ReadValue();
                var newMouseState = new MouseInputActionData()
                {
                    startTime = Time.unscaledTimeAsDouble,
                    position = new Vector2Int((int)position.Value.x, (int)position.Value.y),
                    leftButton = mouse.leftButton.isPressed,
                    middleButton = mouse.middleButton.isPressed,
                    rightButton = mouse.rightButton.isPressed,
                    forwardButton = mouse.forwardButton.isPressed,
                    backButton = mouse.backButton.isPressed,
                    scroll = mouse.scroll.ReadValue(),
                    clickedObjectIds = Array.Empty<int>(),
                    worldPosition = null
                };
                return newMouseState;
            }

            return null;
        }

        private readonly ConcurrentQueue<MouseInputActionData> _mouseInputActions = new();

        public void ClearBuffer()
        {
            _mouseInputActions.Clear();
        }

        public List<MouseInputActionData> FlushInputDataBuffer(bool ensureOne = false)
        {
            List<MouseInputActionData> result = new();
            while (_mouseInputActions.TryPeek(out var action))
            {
                _mouseInputActions.TryDequeue(out _);
                result.Add(action);
            }

            if (ensureOne && result.Count == 0 && _priorMouseState != null)
            {
                // or.. we need at least 1 mouse observation per tick, otherwise hover over effects/etc don't function correctly
                result.Add(_priorMouseState);
            }

            return result;
        }

        private IEnumerable<RecordedGameObjectState> FindObjectsAtPosition(Vector2 position, List<RecordedGameObjectState> state, out float maxZDepth)
        {
            // make sure screen space position Z is around 0
            var vec3Position = new Vector3(position.x, position.y, 0);
            List<RecordedGameObjectState> result = new();
            maxZDepth = 0f;
            var hitUIElement = false;
            foreach (var recordedGameObjectState in state)
            {
                if (recordedGameObjectState.screenSpaceBounds.Contains(vec3Position))
                {
                    if (!hitUIElement && recordedGameObjectState.worldSpaceBounds != null )
                    {
                        if (recordedGameObjectState.screenSpaceZOffset > maxZDepth)
                        {
                            maxZDepth = recordedGameObjectState.screenSpaceZOffset;
                        }
                        result.Add(recordedGameObjectState);
                    }
                    else
                    {
                        maxZDepth = 0f;
                        hitUIElement = true;
                        result.Add(recordedGameObjectState);
                    }
                }
            }

            if (hitUIElement)
            {
                // if we hit a UI element, ignore the in game elements
                result.RemoveAll(a => a.worldSpaceBounds != null);
            }

            return result;
        }
    }
}
