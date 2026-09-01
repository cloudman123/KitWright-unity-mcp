// Copyright (C) KitWright. Licensed under MIT.

#if KITWRIGHT_INPUTSYSTEM
using System;
using System.Collections;
using System.Linq;
using KitWright.Editor.Tools.Builtins;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

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

        // Every member Gamepad's own indexer maps has to resolve, the two triggers included: they sit
        // outside GamepadState's 32-bit button bitmask, which is one reason this writes through the
        // controls rather than through that bitmask.
        [Test]
        public void FindGamepadButton_KeepsEveryButtonTheDeviceIndexerMaps()
        {
            foreach (GamepadButton value in Enum.GetValues(typeof(GamepadButton)))
            {
                Assert.IsNotNull(InputSimulationFunctions.FindGamepadButton(value.ToString()),
                    $"'{value}' is a real GamepadButton and must stay resolvable.");
            }

            Assert.AreEqual(GamepadButton.LeftTrigger, InputSimulationFunctions.FindGamepadButton("left_trigger"));
        }

        [Test]
        public void TouchToolsRefuseOutsidePlayMode()
        {
            StringAssert.Contains("PLAY_MODE_REQUIRED", InputSimulationFunctions.SimulateTouch(10, 10));
            StringAssert.Contains("PLAY_MODE_REQUIRED", InputSimulationFunctions.SimulateTouchDrag(0, 0, 10, 10));
            StringAssert.Contains("PLAY_MODE_REQUIRED", InputSimulationFunctions.SimulateGamepad("south"));
        }

        private InputSettings.EditorInputBehaviorInPlayMode? previousInputBehavior;

        [TearDown]
        public void RestoreInputBehavior()
        {
            if (previousInputBehavior != null)
                InputSystem.settings.editorInputBehaviorInPlayMode = previousInputBehavior.Value;

            previousInputBehavior = null;
        }

        /// <summary>
        /// Probes the raw house pattern with no tool code in the way, so a gamepad assertion can tell
        /// "the tool is wrong" from "the Input System will not route this here". In the editor it can
        /// drop input for devices that respect Game View focus, and a batchmode run has no Game View at
        /// all, so the focus rule is lifted first and restored in TearDown.
        /// </summary>
        private void RequireWorkingGamepadInjection()
        {
            previousInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

            var gamepad = InputSystem.GetDevice<Gamepad>() ?? InputSystem.AddDevice<Gamepad>();
            WriteSouth(gamepad, 1f);
            var landed = gamepad.buttonSouth.isPressed;
            WriteSouth(gamepad, 0f);

            if (!landed)
            {
                Assert.Ignore("Raw Input System state events do not reach a synthetic Gamepad here: batchmode " +
                              $"has no Game View to route device input to (wrote to id={gamepad.deviceId}, " +
                              $"added={gamepad.added}, enabled={gamepad.enabled}). simulate_touch and " +
                              "simulate_key_press do land, and their own tests cover the injection path.");
            }
        }

        private static void WriteSouth(Gamepad gamepad, float value)
        {
            using (StateEvent.From(gamepad, out var eventPtr))
            {
                gamepad.buttonSouth.WriteValueIntoEvent(value, eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }

            InputSystem.Update();
        }

        private static string Diagnose(string answer)
        {
            var gamepad = Gamepad.current;
            return $"tool said [{answer}]; devices=[{string.Join(", ", InputSystem.devices.Select(d => d.name))}]; " +
                   $"south={gamepad?.buttonSouth.ReadValue()}; leftTrigger={gamepad?.leftTrigger.ReadValue()}; " +
                   $"leftStick={gamepad?.leftStick.ReadValue()}; updateMode={InputSystem.settings.updateMode}";
        }

        // Everything below enters Play Mode, because the injection itself is the part that cannot be
        // checked any other way: outside Play Mode every one of these tools returns PLAY_MODE_REQUIRED
        // before touching a device, so an Edit Mode assertion proves only that the guard works.
        [UnityTest]
        public IEnumerator SimulateTouch_DrivesARealTouchscreenDeviceUpAndDown()
        {
            yield return new EnterPlayMode();

            InputSimulationFunctions.SimulateTouch(120, 140, "press");
            Assert.IsNotNull(Touchscreen.current, "the tool is expected to add a Touchscreen when none exists");
            Assert.IsTrue(Touchscreen.current.primaryTouch.press.isPressed, "a press should leave the touch down");
            Assert.AreEqual(120f, Touchscreen.current.primaryTouch.position.ReadValue().x, 1f);

            InputSimulationFunctions.SimulateTouch(120, 140, "release");
            Assert.IsFalse(Touchscreen.current.primaryTouch.press.isPressed, "a release should lift it");

            StringAssert.Contains("swiped", InputSimulationFunctions.SimulateTouchDrag(20, 20, 200, 240));
            Assert.IsFalse(Touchscreen.current.primaryTouch.press.isPressed, "a swipe ends with the finger up");
            Assert.AreEqual(200f, Touchscreen.current.primaryTouch.position.ReadValue().x, 1f);

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SimulateGamepad_KeepsAHeldButtonWhileTheStickMoves()
        {
            yield return new EnterPlayMode();

            RequireWorkingGamepadInjection();

            var pressAnswer = InputSimulationFunctions.SimulateGamepad("south", "press");
            Assert.IsNotNull(Gamepad.current, $"the tool is expected to add a Gamepad when none exists; it said [{pressAnswer}]");
            Assert.IsTrue(Gamepad.current.buttonSouth.isPressed, Diagnose(pressAnswer));

            // The regression this merge exists for: a gamepad state event carries the whole device, so
            // a partial update that did not read the device first would release the held button here.
            InputSimulationFunctions.SimulateGamepad(button: null, left_stick_x: 1f);
            Assert.IsTrue(Gamepad.current.buttonSouth.isPressed, "moving a stick must not release a held button");
            Assert.Greater(Gamepad.current.leftStick.ReadValue().x, 0.5f);

            InputSimulationFunctions.SimulateGamepad("south", "release");
            Assert.IsFalse(Gamepad.current.buttonSouth.isPressed);
            Assert.Greater(Gamepad.current.leftStick.ReadValue().x, 0.5f, "releasing a button must not centre the stick");

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SimulateGamepad_MovesTheAnalogTriggerAndNotTheDpad()
        {
            yield return new EnterPlayMode();

            RequireWorkingGamepadInjection();

            var answer = InputSimulationFunctions.SimulateGamepad(button: null, left_trigger: 1f, right_trigger: 0.5f);

            Assert.Greater(Gamepad.current.leftTrigger.ReadValue(), 0.9f, Diagnose(answer));
            Assert.AreEqual(0.5f, Gamepad.current.rightTrigger.ReadValue(), 0.05f, Diagnose(answer));

            // GamepadButton.LeftTrigger is 32, outside GamepadState's 32-bit mask, so writing it through
            // that bitmask would have folded it onto bit 0 - D-pad Up.
            Assert.IsFalse(Gamepad.current.dpad.up.isPressed, "the trigger must not fold onto the D-pad");
            Assert.IsFalse(Gamepad.current.dpad.down.isPressed);

            // Named as a button it is a full press, and still not the D-pad.
            InputSimulationFunctions.SimulateGamepad("left_trigger", "press");
            Assert.Greater(Gamepad.current.leftTrigger.ReadValue(), 0.9f);
            Assert.IsFalse(Gamepad.current.dpad.up.isPressed, "the trigger must not fold onto the D-pad");

            yield return new ExitPlayMode();
        }
    }
}
#endif
