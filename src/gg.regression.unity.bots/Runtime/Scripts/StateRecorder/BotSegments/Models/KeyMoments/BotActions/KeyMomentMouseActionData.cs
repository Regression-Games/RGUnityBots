using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.Models;
using UnityEngine;
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
                //  -- but what if there are 2 or more 'perfect' matches.. then we need to narrow down based on the # of other path elements that overlap each of these
                // MATCH[1,3,7] (perfect) - "HeroesObjects/Unit_Hero_Gunner/Spine Mecanim GameObject (unit_hero_gunner) RenderTexture"
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
        private readonly List<(Action, double)> _mouseActionsToDo = new();

        private double _clickDownTime = 0d;

        public bool ProcessAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities, out string error)
        {
            if (_isDoneWaitOneFrame)
            {
                _isDoneWaitOneFrame = false;
                _isStopped = true;
                // need to return true so the bot doesn't start exploring.. it needs to think this action is processing as intended since that wait was intended
                error = null;
                return true;
            }

            if (_mouseActionsToDo.Count > 0)
            {
                // wait until the right time, then do the un-click mouse action
                if (Time.unscaledTimeAsDouble >= _mouseActionsToDo[0].Item2)
                {
                    _mouseActionsToDo[0].Item1.Invoke();
                    _mouseActionsToDo.Clear();
                    _isDoneWaitOneFrame = true;

                    error = null;
                    return true;
                }
                else
                {
                    error = null;
                    // need to return true so the bot doesn't start exploring.. it needs to think this action is processing as intended while we wait
                    return true;
                }
            }

            _mouseActionsToDo.Clear();

            if (!_isStopped)
            {
                MouseInputActionData pendingAction = null;

                Vector2? lastClickPosition = null;

                List<ObjectStatus> previousOverlappingObjects = new();

                var mouseActionsCount = mouseActions.Count;
                for (var m = 0; m < mouseActionsCount; m++)
                {
                    List<ObjectStatus> overlappingObjects = new();
                    var mouseAction = mouseActions[m];
                    var preConditionNormalizedPaths = _preconditionNormalizedPaths[m];

                    if (preConditionNormalizedPaths.Count == 0)
                    {
                        pendingAction = mouseAction;
                        continue; // the for - basically.. this is a mouse positional update, but we need the next click to know which position to put it in
                    }

                    var preconditionMatches = BuildPreconditions(segmentNumber, currentTransforms, currentEntities, preConditionNormalizedPaths);

                    // now that we have all the precondition matches mapped out, let's see if we have the leftmost object..

                    // if the leftmost object is a UI object, then it should already be the smallest / most precise UI thing to click as we solve this by sorting
                    // the UI elements in mouseinputactionobserver.FindObjectsAtPosition based on the smallest screen-space bounds
                    if (preconditionMatches[0].Count > 0)
                    {
                        // yay.. we found something(s).. figure out which one of them is the 'best' based on number of overlapping matches

                        // for each of the left most preconditionMatches[0] .. go through each of other precondition matches and count how many overlap.. can only count 0 or 1 per each index
                        // we will also compute the 'smallest' click bounds for these overlaps as we go
                        var matchResults = new List<(ObjectStatus, int, (int,int,int,int))>(preconditionMatches[0].Count);

                        foreach (var preconditionMatch0 in preconditionMatches[0])
                        {
                            var smallestBounds = preconditionMatch0.Item1.screenSpaceBounds.Value;
                            // adjust in for the min to ensure we don't miss a click
                            var minX = (int)(smallestBounds.min.x + 0.51f);
                            var minY = (int)(smallestBounds.min.y + 0.51f);
                            // adjust in for the max to ensure we don't miss a click
                            var maxX = (int)(smallestBounds.max.x - 0.51f);
                            var maxY = (int)(smallestBounds.max.y - 0.51f);

                            RGDebug.LogDebug($"Starting with bounds: ({minX}, {minY}),({maxX}, {maxY}) for object path: {mouseAction.clickedObjectNormalizedPaths[0]} , target object: {preconditionMatch0.Item1.NormalizedPath}");
                            // now let's narrow down the screen space bounds more precisely based on all our preconditions
                            // count the number of indexes where we had an overlap while doing it
                            var overlapCount = 0;
                            for (var i = 1; i < preconditionMatches.Length; i++)
                            {
                                var preconditionMatchesI = preconditionMatches[i];
                                if (preconditionMatchesI.Count > 0)
                                {
                                    var didOverlap = false;
                                    foreach (var preconditionMatchI in preconditionMatchesI)
                                    {
                                        //  not the same logic as MouseEventSender.FindBestClickObject.. this version narrows in on the smallest bounding area

                                        // ReSharper disable once PossibleInvalidOperationException - already filtered at the top of the method to only have entries with valid visible bounds
                                        var newBounds = preconditionMatchI.Item1.screenSpaceBounds.Value;
                                        // adjust in for the min to ensure we don't miss a click
                                        var newMinX = (int)(newBounds.min.x + 0.51f);
                                        var newMinY = (int)(newBounds.min.y + 0.51f);
                                        // adjust in for the max to ensure we don't miss a click
                                        var newMaxX = (int)(newBounds.max.x - 0.51f);
                                        var newMaxY = (int)(newBounds.max.y - 0.51f);

                                        // since these are pixel bounds... we just do (int) cast
                                        var startingMinX = minX;
                                        var startingMinY = minY;
                                        var startingMaxX = maxX;
                                        var startingMaxY = maxY;

                                        if (newMinX > minX && newMinX < maxX)
                                        {
                                            minX = newMinX;
                                        }

                                        if (newMinY > minY && newMinY < maxY)
                                        {
                                            minY = newMinY;
                                        }

                                        if (newMaxX > minX && newMaxX < maxX)
                                        {
                                            maxX = newMaxX;
                                        }

                                        if (newMaxY > minY && newMaxY < maxY)
                                        {
                                            maxY = newMaxY;
                                        }

                                        if (minX != startingMinX || minY != startingMinY || maxX != startingMaxX || maxY != startingMaxY)
                                        {
                                            overlappingObjects.Add(preconditionMatchI.Item1);
                                            didOverlap = true;
                                            RGDebug.LogDebug($"Narrowed bounds: ({minX}, {minY}),({maxX}, {maxY}) for object path: {mouseAction.clickedObjectNormalizedPaths[0]} for overlap with object path [{i}]: {preconditionMatchI.Item1.NormalizedPath}");
                                        }
                                    }

                                    if (didOverlap)
                                    {
                                        ++overlapCount;
                                    }
                                }
                            }

                            matchResults.Add((preconditionMatch0.Item1, overlapCount, (minX,minY,maxX,maxY)));

                        }

                        matchResults.Sort((a, b) =>
                        {
                            if (a.Item2 < b.Item2)
                            {
                                // sort lower match counts to the end
                                return 1;
                            }

                            if (a.Item2 > b.Item2)
                            {
                                // sort highest match counts to the front
                                return -1;
                            }

                            // else sort by smallest bounds
                            var aArea = (a.Item3.Item3 - a.Item3.Item1) * (a.Item3.Item4 - a.Item3.Item2);
                            var bArea = (b.Item3.Item3 - b.Item3.Item1) * (b.Item3.Item4 - b.Item3.Item2);

                            if (aArea < bArea)
                            {
                                return -1;
                            }

                            // floating point math.. don't really care about equality in the zillionth of a percent chance that happens here
                            return 1;
                        });

                        var bestObjectStatus = matchResults[0].Item1;
                        // we started with clicking the center.. but this was limiting
                        //clickPosition = new Vector2(minX + (maxX - minX) / 2, minY + (maxY - minY) / 2);
                        // instead... make the click-position a random position within the bounds
                        // 1. for more variability in testing
                        // 2. to get around cases where we were say clicking on a floor tile, but there is something on that floor tile and we wanted to click on the open space of the floor tile
                        //     on the next attempt, it picks a new position to try thus giving us a better chance of passing
                        // TODO: Future: Can we capture the relativistic offset click position where we hit a world space object so that we can try to re-click on that same offset given its new world position ???
                        // This would allow us to know that we clicked about X far into this floor tile and replicate that positioning regardless of the actual worldspace positioning in the replay...
                        // +1 because int range is max exclusive.. if these were floats.. remove the +1
                        Vector2 clickPosition = new Vector2(Random.Range(matchResults[0].Item3.Item1, matchResults[0].Item3.Item3+1), Random.Range(matchResults[0].Item3.Item2, matchResults[0].Item3.Item4+1));

                        var isInteractable = true;
                        // now that we have the 'best' match ... first let's make sure it's ready to be clicked
                        // visible (already true by the time we get here)/active-enabled(we already know that from a canvas perspective this is visible, but need to check UI component info)
                        if (bestObjectStatus is TransformStatus tStatus)
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

                        if (!isInteractable)
                        {
                            _mouseActionsToDo.Clear();
                            // didn't find it.. this is where 'exploration' is going to start happening based on our result
                            error = $"No valid mouse action object found for path:\n{preConditionNormalizedPaths[0].normalizedPath}";
                            return false;
                        }

                        // check if this is an un-click of a previous click.. if so we have some special cases to check to make sure we want to move the click position or not
                        // ReSharper disable once PossibleInvalidOperationException - already filtered at the top of the method to only have entries with valid visible bounds
                        if (lastClickPosition.HasValue)
                        {
                            if (lastClickPosition.Value.x >= matchResults[0].Item3.Item1
                                && lastClickPosition.Value.x <= matchResults[0].Item3.Item3
                                && lastClickPosition.Value.y >= matchResults[0].Item3.Item2
                                && lastClickPosition.Value.y <= matchResults[0].Item3.Item4)
                            {
                                RGDebug.LogDebug($"Leaving mouse action index {m} at previous action position based on bounds overlaps to original path: {bestObjectStatus.NormalizedPath}");
                                // if the click position was within the bounds of the un-click object, just use that same one... this is critical for cases where the
                                // clicked button is no longer present in the normalizedPaths list for the un-click... which happens based on how observation of the un-click occurs on a future frame
                                clickPosition = lastClickPosition.Value;
                            }
                            else if (previousOverlappingObjects.Count > 0)
                            {
                                // an even more special case... even though the bounds don't align.. the resolution could have changed (this happens a lot for bossroom menus when you resize and the 3d game objects scale differently than the UI)
                                // but on un-click.. the ui element isn't in the path list anymore so it tries to un-click on the door or some other background game object instead and misses the button
                                // so if we had this same object listed in the conditions for the prior click calculation.. then the original click already considered this and the scaling factor of the screen
                                // isn't important.. we want to un-click exactly where we clicked
                                if (previousOverlappingObjects.Any(a => a == bestObjectStatus))
                                {
                                    RGDebug.LogDebug($"Leaving mouse action index {m} at previous action position based on object path existing at time of click: {bestObjectStatus.NormalizedPath}");
                                    clickPosition = lastClickPosition.Value;
                                }
                            }
                        }

                        if (!QueueClickForObjectAtPosition(segmentNumber, pendingAction, mouseAction, bestObjectStatus, clickPosition, currentTransforms, out error))
                        {
                            _mouseActionsToDo.Clear();
                            return false;
                        }

                        if (m == 1)
                        {
                            previousOverlappingObjects = overlappingObjects;
                            // on the 'click'.. save the position
                            lastClickPosition = clickPosition;
                        }

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

                var _mouseActionsToDoCount = _mouseActionsToDo.Count;
                if (_mouseActionsToDoCount > 0)
                {
                    // only do the first 2 actions
                    for (var i = 0; i < 2; i++)
                    {
                        _mouseActionsToDo[i].Item1.Invoke();
                    }
                    // remove the first 2 things we already did
                    _mouseActionsToDo.RemoveRange(0,2);

                    error = null;
                    return true;
                }

            }

            error = null;
            return false;
        }

        private List<(ObjectStatus, (int[], int[]))>[] BuildPreconditions(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities, List<PreconditionNormalizedPathData> preConditionNormalizedPaths)
        {
            var possibleTransformsToClick = currentTransforms.Values.Where(a => a.screenSpaceBounds.HasValue).ToList();
            var preconditionsLength = preConditionNormalizedPaths.Count;
            var preconditionMatches = new List<(ObjectStatus, (int[],int[]))>[preconditionsLength];
            for (var i = 0; i < preconditionsLength; i++)
            {
                preconditionMatches[i] = new List<(ObjectStatus, (int[], int[]))>();
            }

            foreach (var possibleTransformToClick in possibleTransformsToClick)
            {
                possibleTransformToClick.TokenizedObjectPath ??= PreconditionNormalizedPathData.TokenizeObjectPath(possibleTransformToClick.NormalizedPath);

                var breakoutCount = 0;// optimization for when we've found exact matches for all preconditions
                // this should nearly always be smaller than the # of possibleTransformToClick
                for (var j = 0; j < preconditionsLength; j++)
                {
                    // optimization - only keep processing this precondition when we didn't have an exact match for it already
                    if (preconditionMatches[j].Count == 0 || preconditionMatches[j][0].Item2.Item1 != null)
                    {
                        var precondition = preConditionNormalizedPaths[j];

                        // prefer normalized path match, then tokenized path matching logic
                        if (precondition.normalizedPath == possibleTransformToClick.NormalizedPath)
                        {
                            // normalized matching logic
                            if (preconditionMatches[j].Count > 0 && preconditionMatches[j][0].Item2.Item1 != null)
                            {
                                // wipe out all the tokenized matches if we get an exact
                                preconditionMatches[j].Clear();
                            }
                            preconditionMatches[j].Add((possibleTransformToClick, (null, null)));
                            break; // the for
                        }
                        else
                        {
                            // tokenized matching logic
                            if (preconditionMatches[j].Count > 0 && preconditionMatches[j][0].Item2.Item1 == null)
                            {
                                // we already have exact matches.. ignore the tokenized ones
                                break; // the for
                            }
                            var tokenMatches = EvaluateTokenMatches(precondition.tokenData, possibleTransformToClick.TokenizedObjectPath);
                            if (tokenMatches.Item1 != null)
                            {
                                // got something of the same path length with some token matches in each part
                                if (preconditionMatches[j].Count > 0)
                                {
                                    var isBetter = AreNewTokenMatchesBetter(preconditionMatches[j][0].Item2, tokenMatches);
                                    // compare
                                    if (true == isBetter)
                                    {
                                        // better match.. clear the list
                                        preconditionMatches[j].Clear();
                                        preconditionMatches[j].Add((possibleTransformToClick, tokenMatches));
                                    }
                                    else if (isBetter == null)
                                    {
                                        // equal match.. add to the list
                                        preconditionMatches[j].Add((possibleTransformToClick, tokenMatches));
                                    }
                                    // else worse match.. leave it alone
                                }
                                else
                                {
                                    //set the first match
                                    preconditionMatches[j].Add((possibleTransformToClick, tokenMatches));
                                }

                                break; // the for
                            }
                        }
                    }
                    else
                    {
                        ++breakoutCount;
                    }
                }

                if (breakoutCount == preconditionsLength)
                {
                    break; // the foreach - we found exact matches for everything
                }
            }

            return preconditionMatches;
        }

        private bool QueueClickForObjectAtPosition(int segmentNumber, MouseInputActionData pendingAction, MouseInputActionData mouseAction, ObjectStatus bestObjectStatus, Vector2 myClickPosition, Dictionary<long, ObjectStatus> currentTransforms, out string error)
        {
            // we fill 'myClickPosition' in for all code paths.. if you get a null here, something above has bad logic as this should be filled in always here
            if (pendingAction != null)
            {
                // we are on the 2nd action, which is the 'click'
                if (bestObjectStatus is TransformStatus)
                {
                    // cross check that the top level element we want to click is actually on top at the click position.. .this is to handle things like scene transitions with loading screens, or temporary popups on the screen over
                    // our intended click item (iow.. it's there/ready, but obstructed) ...
                    // make sure our object is at the front of the list and thus at the closest Z depth at that point.. findobjectsatposition handles z depth sorting for us
                    // or that the item at the front of the list is on the same path tree as us (we might have clicked on the text part of the button instead of the button, but both still click the button)

                    var objectsAtClickPosition = MouseInputActionObserver.FindObjectsAtPosition(myClickPosition, currentTransforms.Values, out _);
                    // see if our object is 'first' or obstructed
                    if (objectsAtClickPosition.Count > 0)
                    {
                        if (!mouseAction.clickedObjectNormalizedPaths[0].StartsWith(objectsAtClickPosition[0].NormalizedPath))
                        {
                            error = $"Unable to perform Key Moment Mouse Action at position:\n({(int)myClickPosition.x}, {(int)myClickPosition.y})\non object path:\n{mouseAction.clickedObjectNormalizedPaths[0]}\na UI object is obstructing with path:\n{objectsAtClickPosition[0].NormalizedPath}";
                            return false;
                        }
                    }
                    else
                    {
                        error = $"Unable to perform Key Moment Mouse Action at position:\n({(int)myClickPosition.x}, {(int)myClickPosition.y})\non object path:\n{mouseAction.clickedObjectNormalizedPaths[0]}\nno objects at that position";
                        return false;
                    }
                }

                var myPendingAction = pendingAction;
                _mouseActionsToDo.Add((() =>
                {
                    RGDebug.LogInfo($"KeyMoment - Mouse Pending Action applied at position: ({(int)myClickPosition.x}, {(int)myClickPosition.y}) on object path: {mouseAction.clickedObjectNormalizedPaths[0]}");
                    // perform the mouse action at the center of our new smallest bounds
                    MouseEventSender.SendRawPositionMouseEvent(segmentNumber, myClickPosition, myPendingAction.leftButton, myPendingAction.middleButton, myPendingAction.rightButton, myPendingAction.forwardButton, myPendingAction.backButton, myPendingAction.scroll);
                }, 0d));

            }

            _mouseActionsToDo.Add((() =>
            {
                RGDebug.LogInfo($"KeyMoment - Mouse Action at position: ({(int)myClickPosition.x}, {(int)myClickPosition.y}) on object path: {mouseAction.clickedObjectNormalizedPaths[0]}");
                // perform the mouse action at the center of our new smallest bounds
                MouseEventSender.SendRawPositionMouseEvent(segmentNumber, myClickPosition, mouseAction.leftButton, mouseAction.middleButton, mouseAction.rightButton, mouseAction.forwardButton, mouseAction.backButton, mouseAction.scroll);
                // handle setting the delay time between the click down and up for the [2] index item
            }, _clickDownTime == 0d ? 0d : Time.unscaledTimeAsDouble + mouseAction.startTime - _clickDownTime));

            if (pendingAction != null)
            {
                // handle setting the delay time between the click down and up for the [1] index item
                _clickDownTime = mouseAction.startTime;
            }

            error = null;
            return true;
        }

        private bool? AreNewTokenMatchesBetter((int[], int[]) oldMatches, (int[], int[]) newMatches)
        {
            if (oldMatches.Item1 == null)
            {
                // we already had an exact match report
                return false;
            }
            // all 4 lengths are the same at this point
            var length = oldMatches.Item2.Length;
            var exactMatch = true;
            for (int i = 0; i < length; i++)
            {
                if (oldMatches.Item1[i] < newMatches.Item1[i])
                {
                    // first 0 index for segment is later
                    return true;
                }

                if (oldMatches.Item1[i] != newMatches.Item1[i])
                {
                    exactMatch = false;
                }

                if (oldMatches.Item2[i] < newMatches.Item2[i])
                {
                    // num token matches in segment is higher
                    return true;
                }

                if (oldMatches.Item2[i] != newMatches.Item2[i])
                {
                    exactMatch = false;
                }

                // else move onto next segment
            }

            if (exactMatch)
            {
                return null;
            }

            // oldMatch was better
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
