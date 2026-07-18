using System.Collections.Generic;
using UnityEngine;

namespace AIRGAP.Facility.Guards
{
    public enum MemoryEventType
    {
        Sight,
        Sound
    }

    public struct MemoryEntry
    {
        public MemoryEventType Type;
        public Vector2 Position;   // world position of the event
        public float Time;         // agent-local clock at perception
        public string Detail;      // visibility category or sound type
        public float Confidence;   // 0..1: clear sight = 1, faint sound = low
    }

    /// <summary>
    /// The rolling per-guard memory buffer (Phase 4): the last ~60s of personally
    /// perceived sight/sound events. This is the input to report generation
    /// (Phases 6-8) and to the Investigate order's executability gate — get the
    /// data structure right before any language model consumes it. Plain C#.
    /// </summary>
    public class GuardMemory
    {
        private readonly float _bufferSeconds;
        private readonly List<MemoryEntry> _entries = new List<MemoryEntry>();
        private float _clock;

        public GuardMemory(float bufferSeconds)
        {
            _bufferSeconds = bufferSeconds;
        }

        public float Clock => _clock;
        public IReadOnlyList<MemoryEntry> Entries => _entries;

        public void Tick(float deltaTime)
        {
            _clock += deltaTime;
            while (_entries.Count > 0 && _clock - _entries[0].Time > _bufferSeconds)
                _entries.RemoveAt(0);
        }

        public void Push(MemoryEventType type, Vector2 position, string detail, float confidence)
        {
            _entries.Add(new MemoryEntry
            {
                Type = type,
                Position = position,
                Time = _clock,
                Detail = detail,
                Confidence = confidence
            });
        }

        /// <summary>Most recent event that carries a position — the Investigate target.</summary>
        public bool TryLatestLocatedEvent(out MemoryEntry entry)
        {
            if (_entries.Count == 0) { entry = default; return false; }
            entry = _entries[_entries.Count - 1];
            return true;
        }

        /// <summary>Any sight event within the last N seconds (order plausibility predicate 1).</summary>
        public bool HasSightWithin(float seconds)
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (_clock - _entries[i].Time > seconds) break;
                if (_entries[i].Type == MemoryEventType.Sight) return true;
            }
            return false;
        }
    }
}
