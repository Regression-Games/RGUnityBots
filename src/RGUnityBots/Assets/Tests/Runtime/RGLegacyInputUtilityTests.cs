#if ENABLE_LEGACY_INPUT_MANAGER
using System.Collections.Generic;
using NUnit.Framework;
using RegressionGames.RGLegacyInputUtility;
using UnityEngine;

namespace Tests.Runtime
{
    [TestFixture]
    public class RGLegacyInputUtilityTests
    {
        [Test]
        public void TestKeyNameToCode()
        {
            Dictionary<string, KeyCode> testCases = new Dictionary<string, KeyCode>()
            {
                {"b", KeyCode.B},
                {"5", KeyCode.Alpha5},
                {"right", KeyCode.RightArrow},
                {"[3]", KeyCode.Keypad3},
                {"[+]", KeyCode.KeypadPlus},
                {"left shift", KeyCode.LeftShift},
                {"right ctrl", KeyCode.RightControl},
                {"enter", KeyCode.Return},
                {"[enter]", KeyCode.KeypadEnter},
                {"f6", KeyCode.F6},
                {"mouse 2", KeyCode.Mouse2},
                {"joystick button 4", KeyCode.JoystickButton4},
                {"joystick 6 button 5", KeyCode.Joystick6Button5}
            };

            foreach (var entry in testCases)
            {
                KeyCode result = RGLegacyInputWrapper.KeyNameToCode(entry.Key);
                Assert.That(result, Is.EqualTo(entry.Value), $"{entry.Key} should produce {entry.Value}");
            }
        }
    }
}
#endif
