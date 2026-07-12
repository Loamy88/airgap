using UnityEngine;
using UnityEngine.InputSystem;

namespace AIRGAP.Infiltrator
{
    /// <summary>
    /// Input source abstraction so the controller's movement logic is identical
    /// under a human (Input System polling) and under a batchmode validator
    /// (scripted input). Edge flags (toggles) are true for exactly one Poll.
    /// </summary>
    public interface IMovementInput
    {
        Vector2 Move { get; }
        bool SprintHeld { get; }
        bool CrouchToggled { get; }
        bool FlashlightToggled { get; }
        bool TestNoisePressed { get; }
        Vector2? AimScreenPosition { get; }
        void Poll();
    }

    /// <summary>Bindings per docs/CONTROLS.md (keyboard+mouse and gamepad).</summary>
    public class InputSystemMovementInput : IMovementInput
    {
        public Vector2 Move { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool CrouchToggled { get; private set; }
        public bool FlashlightToggled { get; private set; }
        public bool TestNoisePressed { get; private set; }
        public Vector2? AimScreenPosition { get; private set; }

        public void Poll()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            var mouse = Mouse.current;

            Vector2 move = Vector2.zero;
            bool sprint = false, crouch = false, flashlight = false, testNoise = false;

            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed) move.y += 1;
                if (keyboard.sKey.isPressed) move.y -= 1;
                if (keyboard.aKey.isPressed) move.x -= 1;
                if (keyboard.dKey.isPressed) move.x += 1;
                sprint |= keyboard.leftShiftKey.isPressed;
                crouch |= keyboard.leftCtrlKey.wasPressedThisFrame || keyboard.cKey.wasPressedThisFrame;
                flashlight |= keyboard.fKey.wasPressedThisFrame;
                testNoise |= keyboard.tKey.wasPressedThisFrame;
            }

            if (gamepad != null)
            {
                Vector2 stick = gamepad.leftStick.ReadValue();
                if (stick.sqrMagnitude > move.sqrMagnitude) move = stick;
                sprint |= gamepad.leftStickButton.isPressed;
                crouch |= gamepad.buttonEast.wasPressedThisFrame;
                flashlight |= gamepad.dpad.up.wasPressedThisFrame;
            }

            Move = Vector2.ClampMagnitude(move, 1f);
            SprintHeld = sprint;
            CrouchToggled = crouch;
            FlashlightToggled = flashlight;
            TestNoisePressed = testNoise;
            AimScreenPosition = mouse != null ? mouse.position.ReadValue() : (Vector2?)null;
        }
    }

    /// <summary>CI input: the validator sets fields directly; edge flags auto-clear after one Poll.</summary>
    public class ScriptedMovementInput : IMovementInput
    {
        public Vector2 Move { get; set; }
        public bool SprintHeld { get; set; }
        public bool CrouchToggled { get; private set; }
        public bool FlashlightToggled { get; private set; }
        public bool TestNoisePressed { get; private set; }
        public Vector2? AimScreenPosition => null;

        private bool _crouchQueued, _flashlightQueued, _noiseQueued;

        public void QueueCrouchToggle() => _crouchQueued = true;
        public void QueueFlashlightToggle() => _flashlightQueued = true;
        public void QueueTestNoise() => _noiseQueued = true;

        public void Poll()
        {
            CrouchToggled = _crouchQueued;
            FlashlightToggled = _flashlightQueued;
            TestNoisePressed = _noiseQueued;
            _crouchQueued = _flashlightQueued = _noiseQueued = false;
        }
    }
}
