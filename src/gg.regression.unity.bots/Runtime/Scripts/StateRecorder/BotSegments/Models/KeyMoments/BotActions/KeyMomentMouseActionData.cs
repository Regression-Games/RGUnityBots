using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.Models;
using UnityEngine;
using UnityEngine.UI;

// ReSharper disable InconsistentNaming

namespace RegressionGames.StateRecorder.BotSegments.Models.KeyMoments.BotActions
{
    [Serializable]
    public class PreconditionNormalizedPathData
    {
        public readonly string path;
        public readonly List<string[]> tokenData;

        public PreconditionNormalizedPathData(string path)
        {
            this.path = path;
            this.tokenData = TokenizeObjectPath(path);
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
    public class KeyMomentMouseActionData : IBotActionData
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

                        // this should nearly always be smaller than the # of possibleTransformToClick
                        for (var j = 0; j < preconditionsLength; j++)
                        {
                            var precondition = preConditionNormalizedPaths[j];
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
                    }

                    // now that we have all the precondition matches mapped out, let's see if we have the leftmost object..

                    // if the leftmost object is a UI object, then it should already be the smallest / most precise UI thing to click as we solve this by sorting
                    // the UI elements in mouseinputactionobserver.FindObjectsAtPosition based on the smallest screenspace bounds
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

                            // ReSharper disable once PossibleInvalidOperationException - already filtered at the top of the method to only have entries with valid visible bounds
                            var smallestBounds = preconditionMatches[0].Item1.screenSpaceBounds.Value;
                            var minX = smallestBounds.min.x;
                            var minY = smallestBounds.min.y;
                            var maxX = smallestBounds.max.x;
                            var maxY = smallestBounds.max.y;
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
                                }
                            }

                            var position = new Vector2(minX + (maxX - minX) / 2, minY + (maxY - minY) / 2);

                            if (pendingAction != null)
                            {
                                var myPendingAction = pendingAction;
                                _mouseActionsToDo.Add(() =>
                                {
                                    RGDebug.LogInfo($"KeyMoment - Mouse Pending Action applied at position: ({(int)position.x}, {(int)position.y}) on object path: {mouseAction.clickedObjectNormalizedPaths[0]}");

                                    // perform the mouse action at the center of our new smallest bounds
                                    MouseEventSender.SendRawPositionMouseEvent(segmentNumber, position, myPendingAction.leftButton, myPendingAction.middleButton, myPendingAction.rightButton, myPendingAction.forwardButton, myPendingAction.backButton, myPendingAction.scroll);
                                });
                            }

                            _mouseActionsToDo.Add(() =>
                            {
                                RGDebug.LogInfo($"KeyMoment - Mouse Action at position: ({(int)position.x}, {(int)position.y}) on object path: {mouseAction.clickedObjectNormalizedPaths[0]}");

                                // perform the mouse action at the center of our new smallest bounds
                                MouseEventSender.SendRawPositionMouseEvent(segmentNumber, position, mouseAction.leftButton, mouseAction.middleButton, mouseAction.rightButton, mouseAction.forwardButton, mouseAction.backButton, mouseAction.scroll);
                            });
                            pendingAction = null;
                        }
                        else
                        {
                            // didn't find it.. this is where 'exploration' is going to start happening based on our result
                            error = $"No valid mouse action object found for paths:\n{string.Join("\n", preConditionNormalizedPaths.Select(a => a.path))}\n Exploring to find a match ...";
                            return false;
                        }
                    }
                    else
                    {
                        // didn't find it.. this is where 'exploration' is going to start happening based on our result
                        error = $"No valid mouse action object found for path:\n{preConditionNormalizedPaths[0].path}\n Exploring to find a match ...";
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

        public int EffectiveApiVersion()
        {
            return apiVersion;
        }
    }
}
