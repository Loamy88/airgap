using System;
using AIRGAP.Shared.Data;

namespace AIRGAP.Infiltrator
{
    public enum Stance
    {
        Walk,
        Crouch,
        Sprint,
        Traversal
    }

    /// <summary>
    /// Plain-C# stance resolution — no Unity lifecycle, so batchmode validators can
    /// drive it directly. Crouch is a toggle; sprint is a hold that overrides crouch
    /// while held and moving; being inside a traversal zone overrides everything.
    /// </summary>
    public class StanceMachine
    {
        private readonly InfiltratorConfig _config;
        private bool _crouched;

        public Stance Current { get; private set; } = Stance.Walk;
        public bool CrouchLatched => _crouched;
        public event Action<Stance, Stance> StanceChanged;

        public StanceMachine(InfiltratorConfig config)
        {
            _config = config;
        }

        public void Resolve(bool sprintHeld, bool crouchTogglePressed, bool inTraversal, bool moving)
        {
            if (crouchTogglePressed) _crouched = !_crouched;

            Stance next = inTraversal ? Stance.Traversal
                : sprintHeld && moving ? Stance.Sprint
                : _crouched ? Stance.Crouch
                : Stance.Walk;

            if (next != Current)
            {
                Stance previous = Current;
                Current = next;
                StanceChanged?.Invoke(previous, next);
            }
        }

        public float CurrentSpeed => Current switch
        {
            Stance.Crouch => _config.CrouchSpeed,
            Stance.Sprint => _config.SprintSpeed,
            Stance.Traversal => _config.TraversalSpeed,
            _ => _config.WalkSpeed
        };

        public float CurrentLoudness => Current switch
        {
            Stance.Crouch => _config.CrouchLoudness,
            Stance.Sprint => _config.SprintLoudness,
            Stance.Traversal => _config.TraversalLoudness,
            _ => _config.WalkLoudness
        };

        public float CurrentStepInterval => Current switch
        {
            Stance.Crouch => _config.CrouchStepInterval,
            Stance.Sprint => _config.SprintStepInterval,
            Stance.Traversal => _config.CrouchStepInterval,
            _ => _config.WalkStepInterval
        };
    }
}
