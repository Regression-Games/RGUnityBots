using System.Collections.Generic;
using RegressionGames.StateRecorder.Models;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace RegressionGames.StateRecorder.BotSegments.Samples
{
    public class SimpleRandomMovementBehaviour : MonoBehaviour
    {
        private const float ActionTimeRoll = 0f;
        private const float ActionTimeCrouch = 0f;
        private const float ActionTimeJump = 0.3f;
        private const float ActionTimeFly = 5f;
        private const float ActionTimeSprint = 3f;
        private const float ActionTimeNone = 1f;

        private const float MoveTimeMax = 15f;
        private const float MoveTimeMin = 5f;

        private float _moveTimeLimit = MoveTimeMin;

        private const float StuckTime = 2f;

        private float _actionTime;

        private Key? _lastActionKey;

        public float lastMoveTime = float.MinValue;

        public float lastActionTime = float.MinValue;

        private Key? _currentDirectionMain;
        private Key? _currentDirectionSecondary;

        private Vector3 _lastPlayerPosition = Vector3.zero;

        private float _lastPositionTime = float.MinValue;

        private void Update()
        {
            var time = Time.unscaledTime;

            List<(Key, KeyState)> keyStates = new();

            // check every StuckTime interval to see if we've gotten stuck or died
            if (time - _lastPositionTime > StuckTime)
            {
                _lastPositionTime = time;
                var transforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
                foreach (var transform1 in transforms)
                {
                    // check if we're 'dead' (normally from fall damage)
                    if (transform1.gameObject.name.Contains("DeadBodyMarker"))
                    {
                        RGDebug.LogInfo("RandomMovement - Dead - Respawning");
                        if (_currentDirectionMain.HasValue)
                        {
                            keyStates.Add((_currentDirectionMain.Value, KeyState.Up));
                        }

                        if (_currentDirectionSecondary.HasValue)
                        {
                            keyStates.Add((_currentDirectionSecondary.Value, KeyState.Up));
                        }

                        var keysToSend = new[] { Key.Enter, Key.Slash, Key.R, Key.E, Key.L, Key.I, Key.F, Key.E, Key.Enter, Key.Escape };
                        foreach (var key in keysToSend)
                        {
                            keyStates.Add((key, KeyState.Down));
                            keyStates.Add((key, KeyState.Up));
                        }

                        _currentDirectionMain = null;
                        _currentDirectionSecondary = null;
                        lastMoveTime = float.MinValue;
                        return;
                    }

                    // check if we're stuck
                    if (transform1.gameObject.name.Contains("LocalPlayer"))
                    {
                        var pos = transform1.position;
                        if (Vector3.Distance(pos, _lastPlayerPosition) < 1f)
                        {
                            RGDebug.LogInfo("RandomMovement - Stuck !!!!!!!!!!!!!!!!");
                            // stuck.. need new movements
                            lastMoveTime = float.MinValue;
                        }
                        _lastPlayerPosition = pos;
                        break;
                    }
                }
            }

            // pick a primary direction and move that way with keyboard for some seconds
            // every some other seconds, pick a new secondary direction or null
            // every second pick an action of roll, crouch, sprint, jump, fly, or none

            // primary movement
            if (time-lastMoveTime > _moveTimeLimit)
            {
                lastMoveTime = time;

                _moveTimeLimit = Random.Range(MoveTimeMin, MoveTimeMax);

                if (_currentDirectionMain.HasValue)
                {
                    keyStates.Add((_currentDirectionMain.Value, KeyState.Up));
                }
                if (_currentDirectionSecondary.HasValue)
                {
                    keyStates.Add((_currentDirectionSecondary.Value, KeyState.Up));
                }

                var previousMainDirection = _currentDirectionMain;
                var previousSecondaryDirection = _currentDirectionSecondary;

                while (previousMainDirection == _currentDirectionMain || previousSecondaryDirection == _currentDirectionMain)
                {
                    var direction = Random.Range(0, 4);
                    switch (direction)
                    {
                        //W
                        default:
                        case 0:
                            _currentDirectionMain = Key.W;
                            break;
                        //S
                        case 1:
                            _currentDirectionMain = Key.S;
                            break;
                        //A
                        case 2:
                            _currentDirectionMain = Key.A;
                            break;
                        //D
                        case 3:
                            _currentDirectionMain = Key.D;
                            break;
                    }
                }


                while (previousMainDirection == _currentDirectionSecondary || previousSecondaryDirection == _currentDirectionSecondary)
                {
                    // more chance of no secondary movement
                    var secondaryDirection = Random.Range(0, 6);
                    switch (secondaryDirection)
                    {
                        //W
                        case 0:
                            if (_currentDirectionMain != Key.S)
                            {
                                _currentDirectionSecondary = Key.W;
                            }

                            break;
                        //S
                        case 1:
                            if (_currentDirectionMain != Key.W)
                            {
                                _currentDirectionSecondary = Key.S;
                            }

                            break;
                        //A
                        case 2:
                            if (_currentDirectionMain != Key.D)
                            {
                                _currentDirectionSecondary = Key.A;
                            }

                            break;
                        //D
                        case 3:
                            if (_currentDirectionMain != Key.A)
                            {
                                _currentDirectionSecondary = Key.D;
                            }

                            break;
                        //none
                        default:
                            _currentDirectionSecondary = null;
                            break;
                    }

                    if (_currentDirectionSecondary == _currentDirectionMain)
                    {
                        _currentDirectionSecondary = null;
                    }
                }

                RGDebug.LogInfo("RandomMovement - Primary Movement Key - " + _currentDirectionMain.Value.ToString());
                keyStates.Add((_currentDirectionMain.Value, KeyState.Down));

                if (_currentDirectionSecondary.HasValue)
                {
                    RGDebug.LogInfo("RandomMovement - Secondary Movement Key - " + _currentDirectionSecondary.Value.ToString());
                    keyStates.Add((_currentDirectionSecondary.Value, KeyState.Down));
                }
                else
                {
                    RGDebug.LogInfo("RandomMovement - Secondary Movement Key - NONE");
                }
            }

            // action
            if (time - lastActionTime > _actionTime)
            {
                var action = Random.Range(0, 7);
                if (_lastActionKey.HasValue)
                {
                    keyStates.Add((_lastActionKey.Value, KeyState.Up));
                }

                var resumeMoving = false;
                switch (action)
                {
                    //roll
                    case 0:
                        RGDebug.LogInfo("RandomMovement - Action - Roll");
                        _lastActionKey = Key.LeftCtrl;
                        _actionTime = ActionTimeRoll;
                        break;
                    //sprint
                    case 1:
                        RGDebug.LogInfo("RandomMovement - Action - Sprint");
                        _lastActionKey = Key.LeftShift;
                        _actionTime = ActionTimeSprint;
                        break;
                    //jump (has 2x the probability of any other action)
                    case 2:
                    case 6:
                        RGDebug.LogInfo("RandomMovement - Action - Jump");
                        _lastActionKey = Key.Space;
                        _actionTime = ActionTimeJump;
                        break;
                    //fly
                    case 3:
                        RGDebug.LogInfo("RandomMovement - Action - Fly");
                        _lastActionKey = Key.Space;
                        _actionTime = ActionTimeFly;
                        break;
                    // no action
                    case 4:
                    default:
                        RGDebug.LogInfo("RandomMovement - Action - NONE");
                        _lastActionKey = null;
                        _actionTime = ActionTimeNone;
                        break;
                    // crouch
                    case 5:
                        RGDebug.LogInfo("RandomMovement - Action - Crouch");
                        _lastActionKey = Key.LeftCtrl;
                        _actionTime = ActionTimeCrouch;
                        // stop moving long enough to crouch
                        if (_currentDirectionMain.HasValue)
                        {
                            resumeMoving = true;
                            keyStates.Add((_currentDirectionMain.Value, KeyState.Up));
                        }
                        if (_currentDirectionSecondary.HasValue)
                        {
                            resumeMoving = true;
                            keyStates.Add((_currentDirectionSecondary.Value, KeyState.Up));
                        }
                        break;
                }

                if (_lastActionKey.HasValue)
                {
                    RGDebug.LogInfo("RandomMovement - Action Key - " + _lastActionKey.Value.ToString());
                    keyStates.Add((_lastActionKey.Value, KeyState.Down));
                }

                if (resumeMoving)
                {
                    if (_currentDirectionMain.HasValue)
                    {
                        keyStates.Add((_currentDirectionMain.Value, KeyState.Down));
                    }
                    if ( _currentDirectionSecondary.HasValue)
                    {
                        keyStates.Add((_currentDirectionSecondary.Value, KeyState.Down));
                    }
                }

                lastActionTime = time;
            }

            if (keyStates.Count > 0)
            {
                KeyboardEventSender.SendKeysInOneEvent(0, keyStates);
            }

        }

        private void OnDestroy()
        {
            List<(Key, KeyState)> keyStates = new();

            if (_currentDirectionMain.HasValue)
            {
                keyStates.Add((_currentDirectionMain.Value, KeyState.Up));
            }

            if (_currentDirectionSecondary.HasValue)
            {
                keyStates.Add((_currentDirectionSecondary.Value, KeyState.Up));
            }

            if (_lastActionKey.HasValue)
            {
                keyStates.Add((_lastActionKey.Value, KeyState.Up));
            }

            if (keyStates.Count > 0)
            {
                KeyboardEventSender.SendKeysInOneEvent(0, keyStates);
            }
        }
    }
}
