using System;
using AIRGAP.Shared.Data;

namespace AIRGAP.Facility.Guards
{
    public enum GuardAlertState
    {
        Unaware,
        Suspicious,
        Searching,
        Alarmed
    }

    /// <summary>
    /// The per-guard alertness ladder (Phase 4): plain C#, no Unity lifecycle.
    /// Suspicion accumulates from vision categories and noticed sounds, decays in
    /// calm; thresholds climb the ladder one state machine — the Warden-set
    /// baseline (Relaxed/Standard/Heightened) scales the RATE, never the rules.
    /// Alarmed requires current sight (a noise alone never alarms).
    /// </summary>
    public class AlertnessLadder
    {
        private readonly LadderConfig _config;
        private float _sinceStimulus;

        public GuardAlertState State { get; private set; } = GuardAlertState.Unaware;
        public float Suspicion { get; private set; }
        public event Action<GuardAlertState, GuardAlertState> StateChanged;

        public AlertnessLadder(LadderConfig config)
        {
            _config = config;
        }

        public float RateFor(GuardAlertness baseline) => baseline switch
        {
            GuardAlertness.Relaxed => _config.RelaxedRate,
            GuardAlertness.Heightened => _config.HeightenedRate,
            _ => _config.StandardRate
        };

        public void Tick(float deltaTime, VisibilityCategory sight, GuardAlertness baseline)
        {
            float rate = RateFor(baseline);
            float visionGain = sight switch
            {
                VisibilityCategory.Silhouette => _config.SilhouetteVision,
                VisibilityCategory.Partial => _config.PartialVision,
                VisibilityCategory.Clear => _config.ClearVision,
                _ => _config.NoneVision
            };

            if (visionGain > 0f)
            {
                Suspicion += visionGain * rate * deltaTime;
                _sinceStimulus = 0f;
            }
            else
            {
                _sinceStimulus += deltaTime;
                if (_sinceStimulus >= _config.DecayDelaySeconds)
                    Suspicion = Math.Max(0f, Suspicion - _config.DecayPerSecond * deltaTime);
            }

            Resolve(sight);
        }

        /// <summary>A noticed sound event (GuardHearing rolled true).</summary>
        public void OnNoticedSound(GuardAlertness baseline)
        {
            Suspicion += _config.NoticedSoundBump * RateFor(baseline);
            _sinceStimulus = 0f;
            Resolve(VisibilityCategory.None);
        }

        private void Resolve(VisibilityCategory sight)
        {
            GuardAlertState next = State;
            bool sightNow = sight != VisibilityCategory.None;

            if (Suspicion >= _config.AlarmedThreshold && (!_config.AlarmedRequiresSight || sightNow))
                next = GuardAlertState.Alarmed;
            else if (Suspicion >= _config.SearchingThreshold)
                next = GuardAlertState.Searching;
            else if (Suspicion >= _config.SuspiciousThreshold)
                next = GuardAlertState.Suspicious;
            else
                next = GuardAlertState.Unaware;

            // Alarmed is sticky downward only through Searching (a chase that loses
            // its target de-escalates via ForceState from the agent, not by decay
            // skipping rungs mid-chase).
            if (State == GuardAlertState.Alarmed && next != GuardAlertState.Alarmed)
                next = GuardAlertState.Searching;

            SetState(next);
        }

        /// <summary>Agent-driven transitions (chase lost, Disregard order drops a rung).</summary>
        public void ForceState(GuardAlertState state, float suspicion)
        {
            Suspicion = suspicion;
            SetState(state);
        }

        public void DropOneRung()
        {
            GuardAlertState next = State switch
            {
                GuardAlertState.Alarmed => GuardAlertState.Searching,
                GuardAlertState.Searching => GuardAlertState.Suspicious,
                _ => GuardAlertState.Unaware
            };
            Suspicion = next switch
            {
                GuardAlertState.Searching => _config.SearchingThreshold,
                GuardAlertState.Suspicious => _config.SuspiciousThreshold,
                _ => 0f
            };
            SetState(next);
        }

        private void SetState(GuardAlertState next)
        {
            if (next == State) return;
            GuardAlertState previous = State;
            State = next;
            StateChanged?.Invoke(previous, next);
        }
    }
}
