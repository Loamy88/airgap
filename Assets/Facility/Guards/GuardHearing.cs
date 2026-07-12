using System;
using AIRGAP.Shared.Data;
using AIRGAP.Shared.Events;
using UnityEngine;

namespace AIRGAP.Facility.Guards
{
    public enum GuardAlertness
    {
        Relaxed,
        Standard,
        Heightened
    }

    /// <summary>
    /// Per-guard probabilistic hearing: subscribes to the shared SoundBus and rolls
    /// each event independently against the HearingModel curve. The roll result is
    /// exposed for the debug overlay and logged — repeated events compound only
    /// through repetition, never through memory of failed rolls.
    /// </summary>
    [ExecuteAlways]
    public class GuardHearing : MonoBehaviour
    {
        public struct HearingResult
        {
            public string GuardId;
            public SoundEvent Sound;
            public float Distance;
            public float Probability;
            public double Roll;
            public bool Noticed;
        }

        [SerializeField] private GuardAlertness alertness = GuardAlertness.Standard;

        private GuardConfig _config;
        private GuardMarker _marker;
        private System.Random _random;
        private bool _initialized;

        public HearingResult? LastResult { get; private set; }
        public event Action<HearingResult> Heard;

        public GuardAlertness Alertness => alertness;
        public void SetAlertness(GuardAlertness level) => alertness = level;
        public void SetRandom(System.Random random) => _random = random;

        public void EnsureInitialized()
        {
            if (_initialized) return;
            _config = GameConfig.Load().Guard;
            _marker = GetComponent<GuardMarker>();
            _random ??= new System.Random(Environment.TickCount ^ GetHashCode());
            _initialized = true;
        }

        private void OnEnable()
        {
            EnsureInitialized();
            SoundBus.Emitted += OnSound;
        }

        private void OnDisable()
        {
            SoundBus.Emitted -= OnSound;
        }

        private void OnSound(SoundEvent sound) => EvaluateAndRecord(sound);

        public HearingResult EvaluateAndRecord(SoundEvent sound)
        {
            EnsureInitialized();
            float distance = Vector2.Distance(transform.position, sound.Position);
            float probability = HearingModel.NoticeProbability(sound.Loudness, distance, AlertnessMultiplier, _config);
            double roll = _random.NextDouble();

            var result = new HearingResult
            {
                GuardId = _marker != null ? _marker.GuardId : name,
                Sound = sound,
                Distance = distance,
                Probability = probability,
                Roll = roll,
                Noticed = roll < probability
            };

            LastResult = result;
            Debug.Log($"[AIRGAP] HEAR guard={result.GuardId} type={sound.Type} d={distance:F1} " +
                      $"p={probability:F3} roll={roll:F3} noticed={result.Noticed}");
            Heard?.Invoke(result);
            return result;
        }

        public float AlertnessMultiplier => alertness switch
        {
            GuardAlertness.Relaxed => Config.RelaxedMultiplier,
            GuardAlertness.Heightened => Config.HeightenedMultiplier,
            _ => Config.StandardMultiplier
        };

        private GuardConfig Config
        {
            get { EnsureInitialized(); return _config; }
        }
    }
}
