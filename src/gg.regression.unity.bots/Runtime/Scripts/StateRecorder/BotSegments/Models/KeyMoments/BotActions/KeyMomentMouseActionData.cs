﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.Models;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Random = UnityEngine.Random;

// ReSharper disable InconsistentNaming

namespace RegressionGames.StateRecorder.BotSegments.Models.KeyMoments.BotActions
{
    [Serializable]
    public class PreconditionNormalizedPathData
    {
        public readonly string normalizedPath;
        public readonly List<string[]> tokenData;

        public PreconditionNormalizedPathData(string normalizedPath)
        {
            this.normalizedPath = normalizedPath;
            this.tokenData = TokenizeObjectPath(normalizedPath);
        }

        public static List<string[]> TokenizeObjectPath(string path)
        {
            var segments = path.Split("/");

            var segmentsLength = segments.Length;
            var tokenData = new List<string[]>(segmentsLength);
            for (var i = 0; i < segmentsLength; i++)
            {
                var segment = segments[i];
                // split out based on any special character commonly used to separate data in paths/names
                segment = segment.Replace('\'', ' ').Replace('\"', ' ').Replace(',', ' ').Replace('.', ' ').Replace(';', ' ').Replace(':', ' ').Replace('=', ' ').Replace('+', ' ').Replace('|', ' ').Replace('[', ' ').Replace(']', ' ').Replace('{', ' ').Replace('}', ' ').Replace('<', ' ').Replace('>', ' ').Replace('\\', ' ').Replace('-', ' ').Replace('_', ' ').Replace('(', ' ').Replace(')', ' ');
                string segmentBefore;
                do
                {
                    // prune any double spaces before splitting
                    segmentBefore = segment;
                    segment = segment.Replace("  ", " ");
                } while (segmentBefore.Length != segment.Length);

                var segmentParts = segment.Split(' ');
                tokenData.Add(segmentParts);
            }

            return tokenData;
        }
    }

    /**
     * <summary>Data for clicking on a key moment object in the frame</summary>
     */
    [Serializable]
    public class KeyMomentMouseActionData : IBotActionData, IKeyMomentExploration, IStringBuilderWriteable, IKeyMomentStringBuilderWriteable
    {
        // api version for this object, update if object format changes
        public int apiVersion = SdkApiVersion.VERSION_28;

        [NonSerialized]
        public static readonly BotActionType Type = BotActionType.KeyMoment_MouseAction;

        public List<MouseInputActionData> mouseActions = new();

        [NonSerialized]
        private readonly List<List<PreconditionNormalizedPathData>> _preconditionNormalizedPaths = new();

        private bool _isStopped;

        // wait one frame after finishing to let the game process the action before moving to the next segment
        // this is important as we don't want the next segment to run on this same frame until the result of the mouse click action has processed in the game engine
        private bool _isDoneWaitOneFrame = false;

        public bool IsCompleted()
        {
            return _isStopped;
        }

        public void ReplayReset()
        {
            _isStopped = false;
            _isDoneWaitOneFrame = false;
        }

        public void ActivateExploration()
        {
            // start the exploration process
        }

        public void StartAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            if (!_isStopped)
            {
                // setup re-usable data structures to speed up processing passes
                // Example .. we need to encode this so that objects with the same root, and same path depth, with similar tokens at each path level will match
                // : "CombatUICanvas/HandController/Cards/Card","CombatUICanvas/HandController/Cards/Card/CardBorder/ImageBorder","CombatUICanvas/FadeScreen","CombatUICanvas/HandController/Cards/Card/CardBorder/ImageBorder/SkillIcon","CombatUICanvas/HandController/Cards/Card/TouchArea","CombatUICanvas/HandController/Cards/Card/CardBorder","HeroesObjects/Unit_Hero_Gunner/Spine Mecanim GameObject (unit_hero_gunner) RenderTexture","Environment_TrainInterior/Background/bg1_foreground","Environment_TrainInterior/Background/bg2_building"
                // using this example ... "HeroesObjects/Unit_Hero_Gunner/Spine Mecanim GameObject (unit_hero_gunner) RenderTexture"
                // should tokenize to something like [HeroesObjects],[Unit,Hero,Gunner],[Spine,Mecanim,GameObject,unit,hero,gunner,RenderTexture]
                // thus any of the following examples... preference given to the one with the highest tokens matched.. note that tokens will ONLY match when IN ORDER
                // MATCH[1,3,7] (perfect) - "HeroesObjects/Unit_Hero_Gunner/Spine Mecanim GameObject (unit_hero_gunner) RenderTexture"
                //  -- these next two are 'equal' matches.. chooses first one encountered
                // MATCH[1,3,6] - "HeroesObjects/Unit_Hero_Gunner/Foot Mecanim GameObject (unit_hero_gunner) RenderTexture"
                // MATCH[1,3,6] - "HeroesObjects/Unit_Hero_Gunner/Spleen Mecanim GameObject (unit_hero_gunner) RenderTexture"
                //  -- these next two are more complicated as their token count matches are even.. but we should pick the one with the leftmost token matches
                // to do this, we need to re-think the token structure to be by position
                // MATCH[1,2,4] [[1],[1,1,0],[1,0,0,1,1,0,1]] (pick this one because it was 'Hero', had more 1s earlier) - "HeroesObjects/Unit_Hero_Tank/Spine (unit_hero_tank) RenderTexture"
                // MATCH[1,2,4] [[1],[1,0,1],[1,0,0,1,0,1,1]] - "HeroesObjects/Unit_Enemy_Gunner/Spine (unit_enemy_gunner) RenderTexture"
                //  -- now compare 2 where the shorter path should win
                // MATCH[1,2,3] [[1],[1,1,0],[1,0,0,1,1,0]] (pick this one because it was 'Hero', had more 1s earlier) - "HeroesObjects/Unit_Hero_Tank/Spine (unit_hero_tank)"
                // MATCH[1,2,4] [[1],[1,0,1],[1,0,0,1,0,1,1]] - "HeroesObjects/Unit_Enemy_Gunner/Spine (unit_enemy_gunner) RenderTexture"
                // -- now compare 2 cases where they have extra tokens
                // MATCH[1,3,7] [[1],[1,1,1],[1,1,1,1,1,1,0,0,1]] (pick this one because its 7 1s were more left than the other) - "HeroesObjects/Unit_Hero_Gunner/Spine Mecanim GameObject (unit_hero_gunner) Top-Knot RenderTexture"
                // MATCH[1,3,7] [[1],[1,1,1],[1,1,0,1,1,1,1,1]] - "HeroesObjects/Unit_Hero_Gunner/Spine Mecanim-Bubble GameObject (unit_hero_gunner) RenderTexture"
                // ... the algorithm becomes order the potential matches by their indexing matches such that the higher counts of matches for each segment come first, then order such that the 1s are leftmost

                // but.. since we have a list of possibilities, we also need to select which objects we really care about.. we do this by prioritizing the left most entries in the list
                // : "CombatUICanvas/HandController/Cards/Card","CombatUICanvas/HandController/Cards/Card/CardBorder/ImageBorder","CombatUICanvas/FadeScreen","CombatUICanvas/HandController/Cards/Card/CardBorder/ImageBorder/SkillIcon","CombatUICanvas/HandController/Cards/Card/TouchArea","CombatUICanvas/HandController/Cards/Card/CardBorder","HeroesObjects/Unit_Hero_Gunner/Spine Mecanim GameObject (unit_hero_gunner) RenderTexture","Environment_TrainInterior/Background/bg1_foreground","Environment_TrainInterior/Background/bg2_building"

                // ultimately.. the evaluation logic will then filter down the potential click location

                // 1. Tokenize out each entry in the list
                // 2. We need to have the leftmost candidate object from the list to do our action on.. or at least something with token matches in all 3 parts worst case
                // 3. For each other candidate object in the list, filter down the action location to a smaller screen space bounds if possible

                foreach (var mouseAction in mouseActions)
                {
                    var mouseClickedPaths = mouseAction.clickedObjectNormalizedPaths;
                    var pathList = new List<PreconditionNormalizedPathData>();
                    _preconditionNormalizedPaths.Add(pathList);
                    foreach (var mouseClickedPath in mouseClickedPaths)
                    {
                        pathList.Add(new PreconditionNormalizedPathData(mouseClickedPath));
                    }
                }
            }
        }

        /**
         * <summary>Return a tuple of (first non-matched token index by segment, token match count by segment)</summary>
         */
        private (int[],int[]) EvaluateTokenMatches(IReadOnlyList<string[]> toMatch, IReadOnlyList<string[]> candidate)
        {
            if (toMatch == null || candidate == null || toMatch.Count != candidate.Count)
            {
                // not the correct path length
                return (null,null);
            }

            var valid = true;

            var segmentCount = toMatch.Count;

            var result = (new int[segmentCount], new int[segmentCount]);

            for (var i = 0; i < segmentCount; i++)
            {
                var toMatchSegment = toMatch[i];
                var candidateSegment = candidate[i];

                var toMatchIndex = 0;
                var candidateIndex = 0;

                var first0Index = -1;
                var tokenMatchCount = 0;

                // handle candidate having extra tokens
                if (candidateSegment.Length > toMatchSegment.Length)
                {
                    var foundMatch = true;
                    for (; foundMatch && candidateIndex < candidateSegment.Length;)
                    {
                        foundMatch = false;
                        var candidateSegmentEntry = candidateSegment[candidateIndex];
                        for (int j = toMatchIndex; j < toMatchSegment.Length; j++)
                        {
                            if (string.CompareOrdinal(candidateSegmentEntry, toMatchSegment[j] ) == 0)
                            {
                                foundMatch = true;
                                toMatchIndex = j;
                                ++tokenMatchCount;
                                break; // inner for loop
                            }
                        }

                        if (!foundMatch && first0Index < 0)
                        {
                            first0Index = candidateIndex;
                        }
                        candidateIndex++;
                    }
                }
                else
                {
                    var foundMatch = true;
                    for (; foundMatch && toMatchIndex < toMatchSegment.Length;)
                    {
                        foundMatch = false;
                        var toMatchSegmentEntry = toMatchSegment[toMatchIndex];
                        for (int j = candidateIndex; j < candidateSegment.Length; j++)
                        {
                            if (string.CompareOrdinal(toMatchSegmentEntry, candidateSegment[j]) == 0)
                            {
                                foundMatch = true;
                                candidateIndex = j;
                                ++tokenMatchCount;
                                break; // inner for loop
                            }
                        }
                        if (!foundMatch && first0Index < 0)
                        {
                            first0Index = toMatchIndex;
                        }
                        toMatchIndex++;
                    }
                }
                // shortcut - leave as this wasn't valid
                if (tokenMatchCount < 1)
                {
                    return (null, null);
                }

                result.Item1[i] = first0Index;
                result.Item2[i] = tokenMatchCount;

            }

            return result;
        }

        // we queue up all the actions to do before doing them in case we can't do one based on paths.. we don't want a partial operation
        private readonly List<Action> _mouseActionsToDo = new();

        public bool ProcessAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities, out string error)
        {
            if (_isDoneWaitOneFrame)
            {
                _isStopped = true;
            }

            _mouseActionsToDo.Clear();

            if (!_isStopped)
            {
                var mouseActionsCount = mouseActions.Count;

                MouseInputActionData pendingAction = null;

                Vector2? lastClickPosition = null;

                (ObjectStatus, (int[],int[]))[] previousPreconditionMatches = null;

                for (var m = 0; m < mouseActionsCount; m++)
                {
                    var mouseAction = mouseActions[m];
                    var preConditionNormalizedPaths = _preconditionNormalizedPaths[m];

                    if (preConditionNormalizedPaths.Count == 0)
                    {
                        pendingAction = mouseAction;
                        continue; // the for - basically.. this is a mouse positional update, but we need the next click to know which position to put it in
                    }

                    var possibleTransformsToClick = currentTransforms.Values.Where(a => a.screenSpaceBounds.HasValue).ToList();

                    var preconditionsLength = preConditionNormalizedPaths.Count;

                    var preconditionMatches = new (ObjectStatus, (int[],int[]))[preconditionsLength];
                    for (var i = 0; i < preconditionsLength; i++)
                    {
                        preconditionMatches[i] = (null, (null,null));
                    }

                    foreach (var possibleTransformToClick in possibleTransformsToClick)
                    {
                        possibleTransformToClick.TokenizedObjectPath ??= PreconditionNormalizedPathData.TokenizeObjectPath(possibleTransformToClick.NormalizedPath);

                        var breakoutCount = 0;// optimization for when we've found exact matches for all preconditions
                        // this should nearly always be smaller than the # of possibleTransformToClick
                        for (var j = 0; j < preconditionsLength; j++)
                        {
                            // optimization - only keep processing this precondition when we didn't have an exact match for it already
                            if (preconditionMatches[j].Item1 == null || preconditionMatches[j].Item2.Item1 != null)
                            {
                                var precondition = preConditionNormalizedPaths[j];

                                // prefer exact path match (which we don't have available here... yet), then normalized path match, then tokenized path matching logic
                                if (precondition.normalizedPath == possibleTransformToClick.NormalizedPath)
                                {
                                    // normalized matching logic
                                    preconditionMatches[j] = (possibleTransformToClick, (null, null));
                                    break; // the for
                                }
                                // else
                                {
                                    // tokenized matching logic
                                    var tokenMatches = EvaluateTokenMatches(precondition.tokenData, possibleTransformToClick.TokenizedObjectPath);
                                    if (tokenMatches.Item1 != null)
                                    {
                                        // got something of the same path length with some token matches in each part
                                        if (preconditionMatches[j].Item1 != null)
                                        {
                                            // compare
                                            if (IsNewTokenMatchesBetter(preconditionMatches[j].Item2, tokenMatches))
                                            {
                                                preconditionMatches[j] = (possibleTransformToClick, tokenMatches);
                                            }
                                        }
                                        else
                                        {
                                            //set
                                            preconditionMatches[j] = (possibleTransformToClick, tokenMatches);
                                        }

                                        break; // the for
                                    }
                                }
                            } else
                            {
                                ++breakoutCount;
                            }
                        }

                        if (breakoutCount == preconditionsLength)
                        {
                            break; // the foreach - we found exact matches for everything
                        }
                    }

                    // now that we have all the precondition matches mapped out, let's see if we have the leftmost object..

                    // if the leftmost object is a UI object, then it should already be the smallest / most precise UI thing to click as we solve this by sorting
                    // the UI elements in mouseinputactionobserver.FindObjectsAtPosition based on the smallest screen-space bounds
                    if (preconditionMatches[0].Item1 != null)
                    {
                        // yay.. we found something.. first let's make sure it's ready to be clicked
                        // visible (already true by the time we get here)/active-enabled(we already know that from a canvas perspective this is visible, but need to check UI component info)

                        var oStatus = preconditionMatches[0].Item1;

                        var isInteractable = true;

                        if (oStatus is TransformStatus tStatus)
                        {
                            var theTransform = tStatus.Transform;
                            if (theTransform is RectTransform)
                            {
                                // ui object
                                var selectables = theTransform.GetComponents<Selectable>();
                                // make sure 1 is interactable
                                isInteractable = selectables.Any(a => a.interactable);
                            }
                        }

                        if (isInteractable)
                        {
                            Vector2? clickPosition = null;

                            // ReSharper disable once PossibleInvalidOperationException - already filtered at the top of the method to only have entries with valid visible bounds
                            var smallestBounds = preconditionMatches[0].Item1.screenSpaceBounds.Value;
                            var minX = smallestBounds.min.x;
                            var minY = smallestBounds.min.y;
                            var maxX = smallestBounds.max.x;
                            var maxY = smallestBounds.max.y;

                            if (lastClickPosition.HasValue)
                            {
                                if (lastClickPosition.Value.x >= minX
                                    && lastClickPosition.Value.x <= maxX
                                    && lastClickPosition.Value.y >= minY
                                    && lastClickPosition.Value.y <= maxY)
                                {
                                    RGDebug.LogDebug($"Leaving mouse action index {m} at previous action position based on bounds overlaps to original path: {preconditionMatches[0].Item1.NormalizedPath}");
                                    // if the click position was within the bounds of the un-click object, just use that same one... this is critical for cases where the
                                    // clicked button is no longer present in the normalizedPaths list for the un-click... which happens based on how observation of the un-click occurs on a future frame
                                    clickPosition = lastClickPosition.Value;
                                }
                                else if (previousPreconditionMatches != null)
                                {
                                    // an even more special case... even though the bounds don't align.. the resolution could have changed (this happens a lot for bossroom menus when you resize and the 3d game objects scale differently than the UI)
                                    // but on un-click.. the ui element isn't in the path list anymore so it tries to un-click on the door or some other background game object instead and misses the button
                                    // so if we had this same object listed in the conditions for the prior click calculation.. then the original click already considered this and the scaling factor of the screen
                                    // isn't important.. we want to un-click exactly where we clicked
                                    if (previousPreconditionMatches.Any(a => a.Item1 == preconditionMatches[0].Item1))
                                    {
                                        RGDebug.LogDebug($"Leaving mouse action index {m} at previous action position based on object path existing at time of click: {preconditionMatches[0].Item1.NormalizedPath}");
                                        clickPosition = lastClickPosition.Value;
                                    }
                                }
                            }
                            if (!clickPosition.HasValue)
                            {
                                RGDebug.LogDebug($"Starting with bounds: ({(int)minX}, {(int)minY}),({(int)maxX}, {(int)maxY}) for object path: {mouseAction.clickedObjectNormalizedPaths[0]} , target object: {preconditionMatches[0].Item1.NormalizedPath}");
                                // now let's narrow down the screen space bounds more precisely based on all our preconditions
                                for (var i = 1; i < preconditionMatches.Length; i++)
                                {
                                    var preconditionMatch = preconditionMatches[i];
                                    if (preconditionMatch.Item1 != null)
                                    {
                                        //  not the same logic as MouseEventSender.FindBestClickObject.. this version narrows in on the smallest bounding area

                                        // ReSharper disable once PossibleInvalidOperationException - already filtered at the top of the method to only have entries with valid visible bounds
                                        var newBounds = preconditionMatch.Item1.screenSpaceBounds.Value;
                                        var newMinX = newBounds.min.x;
                                        var newMinY = newBounds.min.y;
                                        var newMaxX = newBounds.max.x;
                                        var newMaxY = newBounds.max.y;

                                        if (newMinX > minX && newMinX < maxX)
                                        {
                                            minX = newMinX;
                                        }

                                        if (newMaxX > minX && newMaxX < maxX)
                                        {
                                            maxX = newMaxX;
                                        }

                                        if (newMinY > minY && newMinY < maxY)
                                        {
                                            minY = newMinY;
                                        }

                                        if (newMaxY > minY && newMaxY < maxY)
                                        {
                                            maxY = newMaxY;
                                        }
                                        RGDebug.LogDebug($"Narrowed bounds: ({(int)minX}, {(int)minY}),({(int)maxX}, {(int)maxY}) for object path: {mouseAction.clickedObjectNormalizedPaths[0]} for overlap with object path: {preconditionMatch.Item1.NormalizedPath}");
                                    }
                                }

                                // we started with clicking the center.. but this was limiting
                                //clickPosition = new Vector2(minX + (maxX - minX) / 2, minY + (maxY - minY) / 2);
                                // instead... make the click-position a random position within the bounds
                                // 1. for more variability in testing
                                // 2. to get around cases where we were say clicking on a floor tile, but there is something on that floor tile and we wanted to click on the open space of the floor tile
                                //     on the next attempt, it picks a new position to try thus giving us a better chance of passing
                                // TODO: Future: Can we capture the relativistic offset click position where we hit a world space object so that we can try to re-click on that same offset given its new world position ???
                                // This would allow us to know that we clicked about X far into this floor tile and replicate that positioning regardless of the actual worldspace positioning in the replay...
                                clickPosition = new Vector2(Random.Range(minX+0.00001f, maxX-0.00001f), Random.Range(minY+0.00001f, maxY-0.00001f));
                            }

                            var myClickPosition = clickPosition.Value; // we fill 'clickPosition' in for all code paths.. if you get a null here, something above has bad logic as this should be filled in always here
                            lastClickPosition = myClickPosition;

                            if (pendingAction != null)
                            {
                                // we are on the 2nd action, which is the 'click'

                                // cross check that the top level element we want to click is actually on top at the click position.. .this is to handle things like scene transitions with loading screens, or temporary popups on the screen over
                                // our intended click item (iow.. it's there/ready, but obstructed) ... we only sort by zDepth(not UI bounds) here so we can find proper obstructions

                                if (oStatus is TransformStatus transformStatus)
                                {

                                    if (transformStatus.worldSpaceBounds == null)
                                    {
                                        // UI element.. make sure it is the highest UI thing hit

                                        var eventSystemHits = new List<RaycastResult>();
                                        // ray-cast into the scene and see if this object is the first thing hit
                                        var pointerEventData = new PointerEventData(EventSystem.current)
                                        {
                                            button = PointerEventData.InputButton.Left,
                                            position = myClickPosition
                                        };
                                        EventSystem.current.RaycastAll(pointerEventData, eventSystemHits);

                                        if (eventSystemHits.Count > 0)
                                        {
                                            eventSystemHits.Sort((a,b) =>
                                            {
                                                if (a.distance < b.distance)
                                                {
                                                    return -1;
                                                }

                                                if (a.distance > b.distance)
                                                {
                                                    return 1;
                                                }

                                                // higher depth == closer to camera.. don't even get me started
                                                return b.depth - a.depth;
                                            });
                                        }
                                        if (eventSystemHits.Count <= 0)
                                        {
                                            error = $"Unable to perform Key Moment Mouse Action at position:\n({(int)myClickPosition.x}, {(int)myClickPosition.y})\non object path:\n{mouseAction.clickedObjectNormalizedPaths[0]}\nno selectable UI objects detected at point";
                                            _mouseActionsToDo.Clear();
                                            return false;
                                        }
                                        else
                                        {
                                            var hitNormalizedPath = TransformStatus.GetOrCreateTransformStatus(eventSystemHits[0].gameObject.transform).NormalizedPath;
                                            if (!hitNormalizedPath.StartsWith(mouseAction.clickedObjectNormalizedPaths[0]))
                                            {
                                                // if this isn't the exact object or a child object (like a button text of the button we're clicking).. then error out
                                                error = $"Unable to perform Key Moment Mouse Action at position:\n({(int)myClickPosition.x}, {(int)myClickPosition.y})\non object path:\n{mouseAction.clickedObjectNormalizedPaths[0]}\ntarget object is obstructed by path:\n{hitNormalizedPath}";
                                                _mouseActionsToDo.Clear();
                                                return false;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // Handle world space obstructions... this is easier, since any overlay UI element will be in the path list over world objects, we just need to make sure our
                                        // object is at the front of the list and thus at the closest Z depth at that point

                                        // cross check that the top level element we want to click is actually on top at the click position.. .this is to handle things like scene transitions with loading screens, or temporary popups on the screen over
                                        // our intended click item (iow.. it's there/ready, but obstructed) ...
                                        // we have to be careful though, because the 'zDepth' computed for the object may NOT be the zDepth at this exact click point on said object...
                                        //var objectsAtClickPosition = MouseInputActionObserver.FindObjectsAtPosition(myClickPosition, currentTransforms.Values, out _);

                                        var mainCamera = Camera.main;
                                        if (mainCamera != null)
                                        {
                                            var ray = mainCamera.ScreenPointToRay(myClickPosition);

                                            var didHit = Physics.Raycast(ray, out var raycastHit);

                                            if (!didHit)
                                            {
                                                error = $"Unable to perform Key Moment Mouse Action at position:\n({(int)myClickPosition.x}, {(int)myClickPosition.y})\nno objects at position";
                                                _mouseActionsToDo.Clear();
                                                return false;
                                            }

                                            var normalizedHitPath = TransformStatus.GetOrCreateTransformStatus(raycastHit.transform).NormalizedPath;

                                            // this handles cases like in bossroom where the collider is on EntranceStaticNetworkObjects/BreakablePot , but the renderer is on EntranceStaticNetworkObjects/BreakablePot/pot
                                            if (!mouseAction.clickedObjectNormalizedPaths[0].StartsWith(normalizedHitPath))
                                            {
                                                error = $"Unable to perform Key Moment Mouse Action at position:\n({(int)myClickPosition.x}, {(int)myClickPosition.y})\non object path:\n{mouseAction.clickedObjectNormalizedPaths[0]}\ntarget object is obstructed by path:\n{normalizedHitPath}";
                                                _mouseActionsToDo.Clear();
                                                return false;
                                            }
                                        }
                                    }

                                }

                                var myPendingAction = pendingAction;
                                _mouseActionsToDo.Add(() =>
                                {
                                    RGDebug.LogInfo($"KeyMoment - Mouse Pending Action applied at position: ({(int)myClickPosition.x}, {(int)myClickPosition.y}) on object path: {mouseAction.clickedObjectNormalizedPaths[0]}");
                                    // perform the mouse action at the center of our new smallest bounds
                                    MouseEventSender.SendRawPositionMouseEvent(segmentNumber, myClickPosition, myPendingAction.leftButton, myPendingAction.middleButton, myPendingAction.rightButton, myPendingAction.forwardButton, myPendingAction.backButton, myPendingAction.scroll);
                                });
                            }

                            _mouseActionsToDo.Add(() =>
                            {
                                RGDebug.LogInfo($"KeyMoment - Mouse Action at position: ({(int)myClickPosition.x}, {(int)myClickPosition.y}) on object path: {mouseAction.clickedObjectNormalizedPaths[0]}");
                                // perform the mouse action at the center of our new smallest bounds
                                MouseEventSender.SendRawPositionMouseEvent(segmentNumber, myClickPosition, mouseAction.leftButton, mouseAction.middleButton, mouseAction.rightButton, mouseAction.forwardButton, mouseAction.backButton, mouseAction.scroll);
                            });
                            pendingAction = null;
                        }
                        else
                        {
                            _mouseActionsToDo.Clear();
                            // didn't find it.. this is where 'exploration' is going to start happening based on our result
                            error = $"No valid mouse action object found for paths:\n{string.Join("\n", preConditionNormalizedPaths.Select(a => a.normalizedPath))}";
                            return false;
                        }
                    }
                    else
                    {
                        _mouseActionsToDo.Clear();
                        // didn't find it.. this is where 'exploration' is going to start happening based on our result
                        error = $"No valid mouse action object found for path:\n{preConditionNormalizedPaths[0].normalizedPath}";
                        return false;
                    }
                }
            }

            if (_mouseActionsToDo.Count > 0)
            {
                foreach (var action in _mouseActionsToDo)
                {
                   action.Invoke();
                }
                _isDoneWaitOneFrame = true;
                error = null;
                return true;
            }

            error = null;
            return false;
        }

        private bool IsNewTokenMatchesBetter((int[], int[]) oldMatches, (int[], int[]) newMatches)
        {
            if (oldMatches.Item1 == null)
            {
                // we already had an exact match report
                return false;
            }
            // all 4 lengths are the same at this point
            var length = oldMatches.Item2.Length;
            for (int i = 0; i < length; i++)
            {
                if (oldMatches.Item1[i] < newMatches.Item1[i])
                {
                    // first 0 index for segment is later
                    return true;
                }

                if (oldMatches.Item2[i] < newMatches.Item2[i])
                {
                    // num token matches in segment is higher
                    return true;
                }

                // else move onto next segment
            }

            return false;
        }

        public void AbortAction(int segmentNumber)
        {
            _isStopped = true;
            _isDoneWaitOneFrame = true;
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"mouseActions\":[\n");
            var mouseActionsCount = mouseActions.Count;
            for (var i = 0; i < mouseActionsCount; i++)
            {
                var mouseAction = mouseActions[i];
                mouseAction.WriteToStringBuilder(stringBuilder);
                if (i + 1 < mouseActionsCount)
                {
                    stringBuilder.Append(",\n");
                }
            }
            stringBuilder.Append("\n]}");
        }

        public void WriteKeyMomentToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"mouseActions\":[\n");
            var mouseActionsCount = mouseActions.Count;
            for (var i = 0; i < mouseActionsCount; i++)
            {
                var mouseAction = mouseActions[i];
                mouseAction.WriteKeyMomentToStringBuilder(stringBuilder);
                if (i + 1 < mouseActionsCount)
                {
                    stringBuilder.Append(",\n");
                }
            }
            stringBuilder.Append("\n]}");
        }

        public int EffectiveApiVersion()
        {
            return apiVersion;
        }
    }
}
