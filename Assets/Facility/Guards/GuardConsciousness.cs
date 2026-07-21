using System;

namespace AIRGAP.Facility.Guards
{
    public enum ConsciousnessState
    {
        Active,
        Down
    }

    /// <summary>
    /// The per-guard consciousness machine (Phase 5): Active → Down (timer) →
    /// Active on wake. Deliberately separate from the alertness ladder — a guard
    /// can go Down at any rung. Going Down discloses NOTHING on its own; the
    /// wake event is where the unconditional incident report fires (GuardAgent
    /// wires it). Plain C#, batchmode-testable.
    /// </summary>
    public class GuardConsciousness
    {
        public ConsciousnessState State { get; private set; } = ConsciousnessState.Active;
        public float DownRemaining { get; private set; }

        /// <summary>Fires on the Down→Active transition (the wake-up moment).</summary>
        public event Action Woke;

        public bool IsDown => State == ConsciousnessState.Down;

        public void GoDown(float seconds)
        {
            State = ConsciousnessState.Down;
            DownRemaining = seconds;
        }

        public void Tick(float deltaTime)
        {
            if (State != ConsciousnessState.Down) return;
            DownRemaining -= deltaTime;
            if (DownRemaining > 0f) return;
            DownRemaining = 0f;
            State = ConsciousnessState.Active;
            Woke?.Invoke();
        }
    }
}
