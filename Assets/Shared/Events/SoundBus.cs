using System;
using UnityEngine;

namespace AIRGAP.Shared.Events
{
    public readonly struct SoundEvent
    {
        public readonly string Type;
        public readonly float Loudness;
        public readonly Vector2 Position;
        public readonly string SourceId;

        public SoundEvent(string type, float loudness, Vector2 position, string sourceId)
        {
            Type = type;
            Loudness = loudness;
            Position = position;
            SourceId = sourceId;
        }
    }

    /// <summary>
    /// The shared sound-event bus (Phase 2). Emitters (footsteps, sprint, pry-bar,
    /// gadgets) broadcast type + loudness + location; guards roll against each event
    /// probabilistically, machines (Phase 9) will trigger deterministically. One bus
    /// for everything so no listener class needs special plumbing later.
    /// </summary>
    public static class SoundBus
    {
        public static event Action<SoundEvent> Emitted;

        /// <summary>Log every emission — off by default (footsteps are chatty), on in CI.</summary>
        public static bool Verbose;

        public static void Emit(in SoundEvent soundEvent)
        {
            if (Verbose)
            {
                Debug.Log($"[AIRGAP] SOUND type={soundEvent.Type} loudness={soundEvent.Loudness:F2} " +
                          $"pos=({soundEvent.Position.x:F1},{soundEvent.Position.y:F1}) src={soundEvent.SourceId}");
            }
            Emitted?.Invoke(soundEvent);
        }

        /// <summary>Test hook: drop all subscribers (validators run repeatedly in one editor session).</summary>
        public static void Reset()
        {
            Emitted = null;
            Verbose = false;
        }
    }
}
