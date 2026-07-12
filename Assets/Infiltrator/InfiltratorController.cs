using AIRGAP.Facility;
using AIRGAP.Shared.Data;
using AIRGAP.Shared.Events;
using UnityEngine;

namespace AIRGAP.Infiltrator
{
    /// <summary>
    /// Phase 1 grey-box movement: crouch/walk/sprint stances from config, vent
    /// traversal by zone detection, footstep sound events onto the shared bus,
    /// noise-ring debug visual, flashlight toggle (the light itself is Phase 2).
    ///
    /// Designed for headless validation: all per-tick work is in Tick(dt), input
    /// comes through IMovementInput, and initialization is explicit — a batchmode
    /// validator calls EnsureInitialized/SetInput/Tick and steps Physics2D itself.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class InfiltratorController : MonoBehaviour
    {
        [SerializeField] private NoiseRing noiseRing;
        [SerializeField] private Transform flashlight;

        private Rigidbody2D _body;
        private StanceMachine _stance;
        private IMovementInput _input;
        private InfiltratorConfig _config;
        private float _stepTimer;
        private Vector2 _lastMoveDirection = Vector2.right;
        private bool _initialized;

        public StanceMachine Stance { get { EnsureInitialized(); return _stance; } }
        public bool InTraversal { get; private set; }
        public bool FlashlightOn { get; private set; }
        public Vector2 Position => _body.position;

        public void SetInput(IMovementInput input) => _input = input;

        public void SetNoiseRing(NoiseRing ring) => noiseRing = ring;
        public void SetFlashlight(Transform light) => flashlight = light;

        public void EnsureInitialized()
        {
            if (_initialized) return;
            _body = GetComponent<Rigidbody2D>();
            _body.gravityScale = 0f;
            _body.freezeRotation = true;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _config = GameConfig.Load().Infiltrator;
            _stance = new StanceMachine(_config);
            _stance.StanceChanged += (previous, current) =>
                Debug.Log($"[AIRGAP] STANCE {previous} -> {current}");
            if (_input == null) _input = new InputSystemMovementInput();
            if (flashlight != null) flashlight.gameObject.SetActive(false);
            _initialized = true;
        }

        private void Awake() => EnsureInitialized();

        private void FixedUpdate()
        {
            if (Application.isPlaying) Tick(Time.fixedDeltaTime);
        }

        public void Tick(float deltaTime)
        {
            EnsureInitialized();
            _input.Poll();

            InTraversal = TraversalZone.Contains(_body.position);
            bool moving = _input.Move.sqrMagnitude > 0.001f;
            _stance.Resolve(_input.SprintHeld, _input.CrouchToggled, InTraversal, moving);

            Vector2 velocity = moving ? _input.Move.normalized * _stance.CurrentSpeed : Vector2.zero;
            _body.linearVelocity = velocity;
            if (moving) _lastMoveDirection = _input.Move.normalized;

            UpdateFootsteps(deltaTime, moving);
            UpdateFlashlight();

            if (_input.TestNoisePressed)
                SoundBus.Emit(new SoundEvent("test-noise", 0.9f, _body.position, "infiltrator"));

            if (noiseRing != null)
                noiseRing.SetRadius(_stance.CurrentLoudness * _config.NoiseRingScale);
        }

        private void UpdateFootsteps(float deltaTime, bool moving)
        {
            if (!moving)
            {
                _stepTimer = 0f;
                return;
            }
            _stepTimer += deltaTime;
            if (_stepTimer < _stance.CurrentStepInterval) return;
            _stepTimer = 0f;
            string type = _stance.Current == AIRGAP.Infiltrator.Stance.Sprint ? "sprint-footstep" : "footstep";
            SoundBus.Emit(new SoundEvent(type, _stance.CurrentLoudness, _body.position, "infiltrator"));
        }

        private void UpdateFlashlight()
        {
            if (_input.FlashlightToggled)
            {
                FlashlightOn = !FlashlightOn;
                Debug.Log($"[AIRGAP] FLASHLIGHT {(FlashlightOn ? "on" : "off")}");
                if (flashlight != null) flashlight.gameObject.SetActive(FlashlightOn);
            }

            if (flashlight == null || !FlashlightOn) return;

            Vector2 aim = _lastMoveDirection;
            if (_input.AimScreenPosition.HasValue && Camera.main != null)
            {
                Vector3 world = Camera.main.ScreenToWorldPoint(_input.AimScreenPosition.Value);
                Vector2 toMouse = (Vector2)world - _body.position;
                if (toMouse.sqrMagnitude > 0.04f) aim = toMouse.normalized;
            }
            flashlight.right = aim;
        }
    }
}
