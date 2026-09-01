// Copyright (C) KitWright. Licensed under MIT.

#if KITWRIGHT_INPUTSYSTEM
using System;
using KitWright.Editor.Tools.Builtins;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace KitWright.Editor.Tests
{
    public sealed class InputSimulationFunctionsTests
    {
        [Test]
        public void FindGamepadButton_AcceptsTheSnakeCaseNamesTheToolAdvertises()
        {
            Assert.AreEqual(GamepadButton.DpadUp, InputSimulationFunctions.FindGamepadButton("dpad_up"));
            Assert.AreEqual(GamepadButton.LeftShoulder, InputSimulationFunctions.FindGamepadButton("left_shoulder"));
            Assert.AreEqual(GamepadButton.Start, InputSimulationFunctions.FindGamepadButton("START"));
        }

        [Test]
        public void FindGamepadButton_ResolvesTheVendorAliasesToTheSameFace()
        {
            Assert.AreEqual(InputSimulationFunctions.FindGamepadButton("south"), InputSimulationFunctions.FindGamepadButton("a"));
            Assert.AreEqual(InputSimulationFunctions.FindGamepadButton("south"), InputSimulationFunctions.FindGamepadButton("cross"));
            Assert.AreEqual(InputSimulationFunctions.FindGamepadButton("east"), InputSimulationFunctions.FindGamepadButton("b"));
        }

        [Test]
        public void FindGamepadButton_ReturnsNullRatherThanAGuess()
        {
            Assert.IsNull(InputSimulationFunctions.FindGamepadButton("turbo"));
            Assert.IsNull(InputSimulationFunctions.FindGamepadButton(""));
            Assert.IsNull(InputSimulationFunctions.FindGamepadButton(null));
        }

        // An enum member's ordinal parses as that enum, so "3" would silently mean the fourth button.
        [Test]
        public void FindGamepadButton_RejectsANumber()
        {
            Assert.IsNull(InputSimulationFunctions.FindGamepadButton("3"));
        }

        // LeftTrigger and RightTrigger are 32 and 33, outside GamepadState's 32-bit button mask, and
        // WithButton would fold them onto bit 0 and bit 1 - DpadUp and DpadDown.
        [Test]
        public void FindGamepadButton_RejectsTheTriggersBecauseTheyAreNotInTheButtonMask()
        {
            Assert.GreaterOrEqual((int)GamepadButton.LeftTrigger, 32);
            Assert.GreaterOrEqual((int)GamepadButton.RightTrigger, 32);

            Assert.IsNull(InputSimulationFunctions.FindGamepadButton("left_trigger"));
            Assert.IsNull(InputSimulationFunctions.FindGamepadButton("RightTrigger"));
        }

        [Test]
        public void FindGamepadButton_KeepsEveryButtonThatIsInTheMask()
        {
            foreach (GamepadButton value in Enum.GetValues(typeof(GamepadButton)))
            {
                if ((int)value >= 32)
                    continue;

                Assert.IsNotNull(InputSimulationFunctions.FindGamepadButton(value.ToString()),
                    $"'{value}' is in the button mask and must stay resolvable.");
            }
        }

        [Test]
        public void TouchToolsRefuseOutsidePlayMode()
        {
            StringAssert.Contains("PLAY_MODE_REQUIRED", InputSimulationFunctions.SimulateTouch(10, 10));
            StringAssert.Contains("PLAY_MODE_REQUIRED", InputSimulationFunctions.SimulateTouchDrag(0, 0, 10, 10));
            StringAssert.Contains("PLAY_MODE_REQUIRED", InputSimulationFunctions.SimulateGamepad("south"));
        }
    }
}
#endif
