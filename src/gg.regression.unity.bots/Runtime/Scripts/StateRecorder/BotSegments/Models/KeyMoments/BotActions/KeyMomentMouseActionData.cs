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
     * <summary>Data for clicking on a key moment object in the frame.  This is used to record key moment bot segments based on mouse actions.</summary>
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

        // These are all used to track the un-click work across multiple Update calls
        private double _unClickTime = 0d;
        private Vector2? _lastClickPosition = null;
        private List<ObjectStatus> _previousOverlappingObjects = new();
        private MouseInputActionData _unClickAction = null;

        public bool IsCompleted()
        {
            return _isStopped;
        }

        public void ReplayReset()
        {
            _unClickAction = null;
            _unClickTime = 0d;
            _lastClickPosition = null;
            _previousOverlappingObjects.Clear();

            _isStopped = false;
            _isDoneWaitOneFrame = false;
        }

        public void KeyMomentExplorationReset()
        {
            // get ready to do this all over again
            _unClickAction = null;
            _unClickTime = 0d;
            _lastClickPosition = null;
            _previousOverlappingObjects.Clear();

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
                //  -- but what if there are 2 or more 'perfect' matches.. then we need to narrow down based on the # of other path elements that overlap each of these... AND.. if those all match, then we go by the closest to the original click position in our later evaluations
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

        private bool HandleUnClickAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities, out string error)
        {
            // find the best object and un-click on it if possible
            var unClickPaths = _preconditionNormalizedPaths[2];
            var unClickPreconditionMatches = BuildPreconditions(segmentNumber, currentTransforms, currentEntities, unClickPaths);

            var validateObjectMatches = true;
            Vector2 clickPosition;
            if (unClickPreconditionMatches.Length > 0)
            {
                var bestUnClickMatchResult = FindBestMatch(segmentNumber, _unClickAction, unClickPreconditionMatches, out _);

                if (bestUnClickMatchResult.HasValue)
                {

                    clickPosition = GetClickPositionForMatch(bestUnClickMatchResult.Value);

                    // if our best match path isn't the same as the click path.. do some extra evaluation
                    // check if this is an un-click of a previous click.. if so we have some special cases to check to make sure we want to move the click position or not
                    if (_lastClickPosition.HasValue)
                    {
                        if (_lastClickPosition.Value.x >= bestUnClickMatchResult.Value.Item2.Item1
                            && _lastClickPosition.Value.x <= bestUnClickMatchResult.Value.Item2.Item3
                            && _lastClickPosition.Value.y >= bestUnClickMatchResult.Value.Item2.Item2
                            && _lastClickPosition.Value.y <= bestUnClickMatchResult.Value.Item2.Item4)
                        {
                            RGDebug.LogDebug($"({segmentNumber}) Leaving mouse un-click action at previous action position: ({(int)_lastClickPosition.Value.x}, {(int)_lastClickPosition.Value.y}) based on bounds overlaps to original path: {bestUnClickMatchResult.Value.Item1.NormalizedPath}");
                            // if the click position was within the bounds of the un-click object, just use that same one... this is critical for cases where the
                            // clicked button is no longer present in the normalizedPaths list for the un-click... which happens based on how observation of the un-click occurs on a future frame
                            clickPosition = _lastClickPosition.Value;
                            validateObjectMatches = false;
                        }
                        else if (_previousOverlappingObjects.Count > 0)
                        {
                            // this can handle things where the un-click was recorded 'after' the element dis-appeared, but it is still there in the replay until the un-click happens... this is just a matter of how ui events and when we can observe during recording works

                            // an even more special case... even though the bounds don't align.. the resolution could have changed (this happens a lot for bossroom menus when you resize and the 3d game objects scale differently than the UI)
                            // but on un-click.. the ui element isn't in the path list anymore so it tries to un-click on the door or some other background game object instead and misses the button
                            // so if we had this same object listed in the conditions for the prior click calculation.. then the original click already considered this and the scaling factor of the screen
                            // isn't important.. we want to un-click exactly where we clicked
                            if (_previousOverlappingObjects.Any(a => a == bestUnClickMatchResult.Value.Item1))
                            {
                                RGDebug.LogDebug($"({segmentNumber}) Leaving mouse un-click action at previous action position: ({(int)_lastClickPosition.Value.x}, {(int)_lastClickPosition.Value.y}) based on object path existing at time of click: {bestUnClickMatchResult.Value.Item1.NormalizedPath}");
                                clickPosition = _lastClickPosition.Value;
                                validateObjectMatches = false;
                            }

                        }
                        // didn't find a way to leave the click alone.. report this
                        if (validateObjectMatches)
                        {
                            RGDebug.LogDebug($"({segmentNumber}) Performing mouse un-click at new position: ({(int)clickPosition.x}, {(int)clickPosition.y}) for object path: {bestUnClickMatchResult.Value.Item1.NormalizedPath} from intended path: {unClickPaths[0].normalizedPath} instead of lastClickPosition: ({(int)_lastClickPosition.Value.x}, {(int)_lastClickPosition.Value.y})");
                        }
                    }
                }
                else
                {
                    // didn't find it.. this is where 'exploration' is going to start happening based on our result
                    error = $"No valid mouse un-click action object found for path:\n{unClickPaths[0].normalizedPath}";
                    return false;
                }
            }
            else
            {
                // there were no object paths on the un-click. assume it is at the same position as the click
                if (_lastClickPosition.HasValue)
                {
                    clickPosition = _lastClickPosition.Value;
                    validateObjectMatches = false;
                }
                else
                {
                    error = $"No valid mouse un-click position... This is a code bug in Regression Games and should NOT happen";
                    return false;
                }
            }

            var targetObjectPath = _unClickAction.clickedObjectNormalizedPaths.Length > 0 ? _unClickAction.clickedObjectNormalizedPaths[0] : null;
            if (!DoActionForObjectAtPosition(segmentNumber, validateObjectMatches, _unClickAction, clickPosition, targetObjectPath, currentTransforms, out error))
            {
                return false;
            }

            error = null;
            return true;

        }

        /**
         * Handles the mouse inputs to each send their event on a different update loop pass, otherwise the UI event system fails to process correctly as it can't handle 2 mouse input events on the same update
         */
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

            if (!_isStopped)
            {

                if (mouseActions.Count == 2 && mouseActions[^1].clickedObjectNormalizedPaths.Length < 1)
                {
                    // we had an un-click only segment on something without any paths.. basically an un-click on nothing
                    // this can happen when there is a random click/un-click on nothing in the game or on something that is excluded from RG seeing it in the state
                    // while this 'should' be not recorded in the first place, we cover it here also

                    // we just say this worked and is done
                    _isStopped = true;
                    error = null;
                    return true;
                }

                if (_unClickAction != null)
                {
                    // handling [2] index.. the un-click

                    // wait until the right time, then do the un-click mouse action
                    if (Time.unscaledTimeAsDouble >= _unClickTime)
                    {
                        var worked = HandleUnClickAction(segmentNumber, currentTransforms, currentEntities, out error);
                        if (worked)
                        {
                            _unClickAction = null;
                            _isDoneWaitOneFrame = true;
                        }

                        return worked;
                    }
                    else
                    {
                        // need to return null error  so the bot doesn't start exploring.. it needs to think this action is processing as intended while we wait
                        error = null;
                        return false;
                    }
                }
                else if (_lastClickPosition.HasValue)
                {
                    // handling [1] index.. the click/un-click
                    if (!DoActionForObjectAtPosition(segmentNumber, true, mouseActions[1], _lastClickPosition.Value, mouseActions[1].clickedObjectNormalizedPaths[0], currentTransforms, out error))
                    {
                        return false;
                    }

                    if (mouseActions.Count > 2)
                    {
                        _unClickAction = mouseActions[2];
                        // save off the time the unClick should happen here so mouse button holds work
                        var clickGapDelta = _unClickAction.startTime - mouseActions[1].startTime;
                        _unClickTime = mouseActions[1].startTime == 0d ? 0d : Time.unscaledTimeAsDouble + clickGapDelta;
                    }
                    else
                    {
                        // this was just a 2 action segment.. we're done
                        _unClickAction = null;
                        _unClickTime = 0d;
                        _isDoneWaitOneFrame = true;
                    }

                    error = null;
                    return true;
                }
                else
                {
                    // handling [0] , [1] indexes.. the positioning and the click[length=3] or un-click action[length=2]
                    // these have the same position.. so we can compute it just once
                    // we have to be aware that you can get a segment with only a position and an un-click  (length 2) .. so we have to be careful there
                    // we use the [1] index to find the paths we need to get the correct position

                    var clickPaths = _preconditionNormalizedPaths[1];

                    var preconditionMatches = BuildPreconditions(segmentNumber, currentTransforms, currentEntities, clickPaths);
                    var bestMatchResult = FindBestMatch(segmentNumber, mouseActions[1], preconditionMatches, out var overlappingObjects);
                    if (bestMatchResult.HasValue)
                    {

                        var clickPosition = GetClickPositionForMatch(bestMatchResult.Value);

                        if (!DoActionForObjectAtPosition(segmentNumber, true, mouseActions[0], clickPosition, mouseActions[1].clickedObjectNormalizedPaths[0], currentTransforms, out error))
                        {
                            return false;
                        }

                        _previousOverlappingObjects = overlappingObjects;
                        // on the 'click'.. save the position
                        _lastClickPosition = clickPosition;

                        error = null;
                        return true;
                    }
                    else
                    {
                        error = $"No valid mouse action object found for path:\n{clickPaths[0].normalizedPath}";
                        return false;
                    }
                }
            }

            error = null;
            return false;
        }

        private Vector2 GetClickPositionForMatch((ObjectStatus, (float,float,float,float)) bestMatchResult)
        {
            // we started with clicking the center.. but this was limiting
            //clickPosition = new Vector2(minX + (maxX - minX) / 2, minY + (maxY - minY) / 2);
            // instead... make the click-position a random position within the bounds..
            // 1. for more variability in testing
            // 2. to get around cases where we were say clicking on a floor tile, but there is something on that floor tile and we wanted to click on the open space of the floor tile
            //     on the next attempt, it picks a new position to try thus giving us a better chance of passing
            // TODO (REG-2223): Future: Can we capture the relativistic offset click position where we hit a world space object so that we can try to re-click on that same offset given its new world position ???
            // This would allow us to know that we clicked about X far into this floor tile and replicate that positioning regardless of the actual worldspace positioning in the replay...
            // Vector2 result = new Vector2(Random.Range(bestMatchResult.Item2.Item1, bestMatchResult.Item2.Item3), Random.Range(bestMatchResult.Item2.Item2, bestMatchResult.Item2.Item4));
            //we pick in the central 80% of the height/width of the object to avoid edge misses which were happening on UI buttons in initial testing
            var rangeWidth10Percent = (bestMatchResult.Item2.Item3 - bestMatchResult.Item2.Item1) / 10;
            var rangeHeight10Percent = (bestMatchResult.Item2.Item4 - bestMatchResult.Item2.Item2) / 10;
            Vector2 result = new Vector2(Random.Range(bestMatchResult.Item2.Item1+rangeWidth10Percent, bestMatchResult.Item2.Item3-rangeWidth10Percent), Random.Range(bestMatchResult.Item2.Item2+rangeHeight10Percent, bestMatchResult.Item2.Item4-rangeHeight10Percent));

            return result;
        }

        /**
         * Returns an array of Lists where each list has entries that are (ObjectStatus, (first non-matched token index by segment, token match count by segment))
         * array of lists, because for each precondition, there can be multiple possible matching objects to evaluate
         */
        private List<(ObjectStatus, (int[], int[]))>[] BuildPreconditions(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities, List<PreconditionNormalizedPathData> preConditionNormalizedPaths)
        {
            var possibleTransformsToClick = currentTransforms.Values.Where(a => a.screenSpaceBounds.HasValue).ToList();
            var preconditionsLength = preConditionNormalizedPaths.Count;
            List<(ObjectStatus, (int[], int[]))>[] preconditionMatches = Array.Empty<List<(ObjectStatus, (int[], int[]))>>();
            if (preconditionsLength > 0)
            {
                preconditionMatches = new List<(ObjectStatus, (int[], int[]))>[preconditionsLength];
                for (var i = 0; i < preconditionsLength; i++)
                {
                    preconditionMatches[i] = new List<(ObjectStatus, (int[], int[]))>();
                }

                foreach (var possibleTransformToClick in possibleTransformsToClick)
                {
                    possibleTransformToClick.TokenizedObjectPath ??= PreconditionNormalizedPathData.TokenizeObjectPath(possibleTransformToClick.NormalizedPath);

                    // this should nearly always be smaller than the # of possibleTransformToClick
                    for (var j = 0; j < preconditionsLength; j++)
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
                }
            }

            return preconditionMatches;
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

        private (ObjectStatus, (float,float,float,float))? FindBestMatch(int segmentNumber, MouseInputActionData mouseAction, List<(ObjectStatus, (int[], int[]))>[] preconditionMatches, out List<ObjectStatus> overlappingObjects)
        {
            overlappingObjects = new List<ObjectStatus>();

            // now that we have all the precondition matches mapped out, let's see if we have the leftmost object..
            if (preconditionMatches.Length > 0 && preconditionMatches[0].Count > 0)
            {
                // yay.. we found something(s).. figure out which one of them is the 'best' based on number of overlapping matches

                // for each of the left most preconditionMatches[0] .. go through each of other precondition matches
                // we will compute the 'smallest' click bounds for these overlaps as we go
                var matchResults = new List<(ObjectStatus, (float, float, float, float))>(preconditionMatches[0].Count);

                var screenWidth = Screen.width;
                var screenHeight = Screen.height;

                foreach (var preconditionMatch0 in preconditionMatches[0])
                {
                    var isPreconditionMatch0Interactable = true;
                    // now that we have a candidate match ... first let's make sure it's ready to be clicked
                    // visible (already true by the time we get here)/active-enabled(we already know that from a canvas perspective this is visible, but need to check UI component info)
                    if (preconditionMatch0.Item1 is TransformStatus preconditionMatch0TransformStatus)
                    {
                        var preconditionMatch0Transform = preconditionMatch0TransformStatus.Transform;
                        if (preconditionMatch0Transform is RectTransform)
                        {
                            // ui object
                            var selectables = preconditionMatch0Transform.GetComponents<Selectable>();
                            if (selectables.Length > 0)
                            {
                                // make sure 1 is interactable .. otherwise leave isPreconditionMatch0Interactable == true
                                isPreconditionMatch0Interactable = selectables.Any(a => a.interactable);
                            }
                        }
                    }

                    if (isPreconditionMatch0Interactable)
                    {
                        // ReSharper disable once PossibleInvalidOperationException - already filtered in BuildPreconditions to only have entries with valid visible bounds
                        var smallestBounds = preconditionMatch0.Item1.screenSpaceBounds.Value;

                        // for world space object, narrow the bounds to its collider
                        if (preconditionMatch0.Item1.worldSpaceBounds != null && preconditionMatch0.Item1 is TransformStatus preconditionMatch0Ts)
                        {
                            var preconditionMatch0Transform = preconditionMatch0Ts.Transform;

                            // check for a collider
                            var collider = preconditionMatch0Transform.GetComponentInParent<Collider>();
                            if (collider != null)
                            {
                                // limit the bounds starting with the collider bounds... the renderer we captured could be bigger than the collider, but the click will only work on the collider
                                var ssBounds = TransformObjectFinder.ConvertWorldSpaceBoundsToScreenSpace(collider.bounds);
                                if (ssBounds.HasValue)
                                {
                                    smallestBounds = ssBounds.Value;
                                }
                            }
                        }

                        // we tried doing this with int math.. but pixel fluctuations of objects with fractional render bounds are a thing (think shimmering/flicker along aliased edges in games)
                        // limit to the screen space.. some visible things hang off the screen and we don't want to click off the screen
                        var minX = Mathf.Max(smallestBounds.min.x, 0f);
                        var minY = Mathf.Max(smallestBounds.min.y, 0f);
                        var maxX = Mathf.Min(smallestBounds.max.x, screenWidth);
                        var maxY = Mathf.Min(smallestBounds.max.y, screenHeight);

                        var originalMinX = minX;
                        var originalMinY = minY;
                        var originalMaxX = maxX;
                        var originalMaxY = maxY;

                        RGDebug.LogDebug($"({segmentNumber}) Starting with bounds: ({minX}, {minY}),({maxX}, {maxY}) for object path: {mouseAction.clickedObjectNormalizedPaths[0]} , target object: {preconditionMatch0.Item1.NormalizedPath}");
                        // now let's narrow down the screen space bounds more precisely based on all our preconditions
                        for (var i = 1; i < preconditionMatches.Length; i++)
                        {
                            var preconditionMatchesI = preconditionMatches[i];
                            if (preconditionMatchesI.Count > 0)
                            {
                                foreach (var preconditionMatchI in preconditionMatchesI)
                                {
                                    var isInteractable = true;
                                    // now that we have a candidate match ... first let's make sure it's ready to be clicked
                                    // visible (already true by the time we get here)/active-enabled(we already know that from a canvas perspective this is visible, but need to check UI component info)
                                    if (preconditionMatchI.Item1 is TransformStatus tStatus)
                                    {
                                        var theTransform = tStatus.Transform;
                                        if (theTransform is RectTransform)
                                        {
                                            // ui object
                                            var selectables = theTransform.GetComponents<Selectable>();
                                            if (selectables.Length > 0)
                                            {
                                                // make sure 1 is interactable - if none leave isInteratable == true
                                                isInteractable = selectables.Any(a => a.interactable);
                                            }
                                        }
                                    }
                                    if (isInteractable)
                                    {
                                        var pmIDidOverlap = false;
                                        // ReSharper disable once PossibleInvalidOperationException - already filtered in BuildPreconditions to only have entries with valid visible bounds
                                        var newBounds = preconditionMatchI.Item1.screenSpaceBounds.Value;

                                        if (preconditionMatchI.Item1.worldSpaceBounds != null && preconditionMatchI.Item1 is TransformStatus preconditionMatchITs)
                                        {
                                            var preconditionMatchITransform = preconditionMatchITs.Transform;

                                            // check for a collider
                                            var collider = preconditionMatchITransform.GetComponentInParent<Collider>();
                                            if (collider != null)
                                            {
                                                // limit the bounds starting with the collider bounds... the renderer we captured could be bigger than the collider, but the click will only work on the collider
                                                var ssBounds = TransformObjectFinder.ConvertWorldSpaceBoundsToScreenSpace(collider.bounds);
                                                if (ssBounds.HasValue)
                                                {
                                                    newBounds = ssBounds.Value;
                                                }
                                            }
                                        }

                                        // adjust in for the min to ensure we don't miss a click
                                        var newMinX = newBounds.min.x;
                                        var newMinY = newBounds.min.y;
                                        // adjust in for the max to ensure we don't miss a click
                                        var newMaxX = newBounds.max.x;
                                        var newMaxY = newBounds.max.y;

                                        if (newMinX >= minX && newMinX <= maxX)
                                        {
                                            minX = newMinX;
                                        }

                                        if (newMinY >= minY && newMinY <= maxY)
                                        {
                                            minY = newMinY;
                                        }

                                        if (newMaxX >= minX && newMaxX <= maxX)
                                        {
                                            maxX = newMaxX;
                                        }

                                        if (newMaxY >= minY && newMaxY <= maxY)
                                        {
                                            maxY = newMaxY;
                                        }


                                        if (newMinX >= originalMinX && newMinX <= originalMaxX)
                                        {
                                            pmIDidOverlap = true;
                                        }

                                        if (newMinY >= originalMinY && newMinY <= originalMaxY)
                                        {
                                            pmIDidOverlap = true;
                                        }

                                        if (newMaxX >= originalMinX && newMaxX <= originalMaxX)
                                        {
                                            pmIDidOverlap = true;
                                        }

                                        if (newMaxY >= originalMinY && newMaxY <= originalMaxY)
                                        {
                                            pmIDidOverlap = true;
                                        }

                                        if (pmIDidOverlap)
                                        {
                                            overlappingObjects.Add(preconditionMatchI.Item1);
                                            RGDebug.LogDebug($"({segmentNumber}) Tightened bounds: ({(int)minX}, {(int)minY}),({(int)maxX}, {(int)maxY}) for object path: {mouseAction.clickedObjectNormalizedPaths[0]} - overlap with object path [{i}]: {preconditionMatchI.Item1.NormalizedPath}");
                                        }
                                    }
                                }


                            }
                        }

                        matchResults.Add((preconditionMatch0.Item1, (minX, minY, maxX, maxY)));
                    }
                }

                var widthScale = screenWidth / mouseAction.screenSize.x;
                var heightScale = screenHeight / mouseAction.screenSize.y;

                var normalizedMouseActionSSPosition = new Vector2(mouseAction.position.x * widthScale, mouseAction.position.y * heightScale);

                matchResults.Sort((a, b) =>
                {

                    // sort by nearest distance to the original click

                    // consider if world space first
                    if (mouseAction.worldPosition.HasValue)
                    {
                        if (a.Item1.worldSpaceBounds.HasValue)
                        {
                            if (b.Item1.worldSpaceBounds.HasValue)
                            {
                                // compare the distances to the original click point
                                var aClosestPoint = a.Item1.worldSpaceBounds.Value.ClosestPoint(mouseAction.worldPosition.Value);
                                var bClosestPoint = b.Item1.worldSpaceBounds.Value.ClosestPoint(mouseAction.worldPosition.Value);

                                var aDistance = Vector3.Distance(aClosestPoint, mouseAction.worldPosition.Value);
                                var bDistance = Vector3.Distance(bClosestPoint, mouseAction.worldPosition.Value);

                                if (aDistance < bDistance)
                                {
                                    return -1;
                                }

                                if (aDistance > bDistance)
                                {
                                    return 1;
                                }
                                // else - unlikely to be exactly the same.. but let it go anyway
                            }
                            else
                            {
                                return -1; // a had world bounds.. more important
                            }
                        }
                        else if (b.Item1.worldSpaceBounds.HasValue)
                        {
                            return 1; // b had world bounds.. more important
                        }
                    }

                    // otherwise consider screen space bounds
                    // bounds around z=0 (z size 0.5f) ... considering the concise bounds computed from overlaps
                    var aSSBounds = new Bounds(new Vector3((a.Item2.Item3 - a.Item2.Item1) / 2 + a.Item2.Item1, (a.Item2.Item4 - a.Item2.Item2) / 2 + a.Item2.Item2, 0f), new Vector3(a.Item2.Item3 - a.Item2.Item1, a.Item2.Item4 - a.Item2.Item2, 0.5f));
                    var bSSBounds = new Bounds(new Vector3((b.Item2.Item3 - b.Item2.Item1) / 2 + b.Item2.Item1, (b.Item2.Item4 - b.Item2.Item2) / 2 + b.Item2.Item2, 0f), new Vector3(b.Item2.Item3 - b.Item2.Item1, b.Item2.Item4 - b.Item2.Item2, 0.5f));

                    var aSSClosestPoint = aSSBounds.ClosestPoint(normalizedMouseActionSSPosition);
                    var bSSClosestPoint = bSSBounds.ClosestPoint(normalizedMouseActionSSPosition);

                    var aSSDistance = Vector3.Distance(aSSClosestPoint, normalizedMouseActionSSPosition);
                    var bSSDistance = Vector3.Distance(bSSClosestPoint, normalizedMouseActionSSPosition);

                    if (aSSDistance < bSSDistance)
                    {
                        return -1;
                    }

                    if (aSSDistance > bSSDistance)
                    {
                        return 1;
                    }
                    // else - unlikely to be exactly the same.. but let it go anyway

                    // else if still somehow equal sort by smallest bounds area
                    var aArea = (a.Item2.Item3 - a.Item2.Item1) * (a.Item2.Item4 - a.Item2.Item2);
                    var bArea = (b.Item2.Item3 - b.Item2.Item1) * (b.Item2.Item4 - b.Item2.Item2);
                    if (aArea < bArea)
                    {
                        return -1;
                    }
                    // floating point multiplication math for area... don't really care about equality in the zillionth of a percent chance that happens here
                    return 1;
                });

                if (matchResults.Count > 0)
                {
                    var bestMatchResult = matchResults[0];
                    return bestMatchResult;
                }
            }

            return null;
        }

        private bool DoActionForObjectAtPosition(int segmentNumber, bool validateObjectMatches, MouseInputActionData mouseAction, Vector2 myClickPosition, string targetObjectPath, Dictionary<long, ObjectStatus> currentTransforms, out string error)
        {

            // cross check that the top level element we want to click is actually on top at the click position.. .this is to handle things like scene transitions with loading screens, or temporary popups on the screen over
            // our intended click item (iow.. it's there/ready, but obstructed) ...
            // make sure our object is at the front of the list and thus at the closest Z depth at that point.. FindObjectsAtPosition handles z depth/etc sorting for us
            // or that the item at the front of the list is on the same path tree as us (we might have clicked on the text part of the button instead of the button, but both still click the button)
            if (validateObjectMatches)
            {
                var objectsAtClickPosition = MouseInputActionObserver.FindObjectsAtPosition(myClickPosition, currentTransforms.Values);
                // see if our object is 'first' or obstructed
                if (objectsAtClickPosition.Count > 0)
                {
                    // handle the case where what we need to click is actually a collider on a parent object
                    if (!(targetObjectPath.StartsWith(objectsAtClickPosition[0].NormalizedPath) || objectsAtClickPosition[0].NormalizedPath.StartsWith(targetObjectPath)))
                    {
                        error = $"Unable to perform Key Moment Mouse Action at position:\n({(int)myClickPosition.x}, {(int)myClickPosition.y})\n\non object path:\n{targetObjectPath}\n\nanother object is obstructing with path:\n{objectsAtClickPosition[0].NormalizedPath}";
                        return false;
                    }
                }
                else
                {
                    error = $"Unable to perform Key Moment Mouse Action at position:\n({(int)myClickPosition.x}, {(int)myClickPosition.y})\n\non object path:\n{targetObjectPath}\n\nno objects at that position";
                    return false;
                }
            }


            RGDebug.LogInfo($"({segmentNumber}) KeyMoment - Mouse Action at position: ({(int)myClickPosition.x}, {(int)myClickPosition.y}) on object path: {targetObjectPath}");
            // perform the mouse action at the center of our new smallest bounds
            MouseEventSender.SendRawPositionMouseEvent(segmentNumber, myClickPosition, mouseAction.leftButton, mouseAction.middleButton, mouseAction.rightButton, mouseAction.forwardButton, mouseAction.backButton, mouseAction.scroll);

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
