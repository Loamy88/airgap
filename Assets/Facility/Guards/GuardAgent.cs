using System.Collections.Generic;
using AIRGAP.Infiltrator;
using AIRGAP.Shared.Data;
using UnityEngine;

namespace AIRGAP.Facility.Guards
{
    public enum DutyMode
    {
        Post,
        Patrol,
        Investigate,
        Search,
        Chase
    }

    /// <summary>
    /// Phase 4 guard: consumes Phase 3's authored posts/loops (never runtime-drawn
    /// routes), climbs the alertness ladder from Phase 2 perception, keeps the
    /// rolling memory buffer, executes standing/transient orders, and enforces the
    /// two capture rules. MonoBehaviour stays thin — rules live in the plain-C#
    /// AlertnessLadder / GuardMemory / GuardOrderRules, and all per-tick work is in
    /// Tick(dt) so batchmode validators drive it exactly like play mode does.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class GuardAgent : MonoBehaviour
    {
        private static readonly List<GuardAgent> Registry = new List<GuardAgent>();
        public static IReadOnlyList<GuardAgent> All => Registry;

        public static GuardAgent FindById(string guardId)
        {
            foreach (GuardAgent agent in Registry)
                if (agent.GuardId == guardId) return agent;
            return null;
        }

        private Rigidbody2D _body;
        private GuardMarker _marker;
        private GuardVision _vision;
        private GuardHearing _hearing;
        private GuardConfig _config;
        private bool _initialized;

        // Duty data — wired by the scene loader at EDITOR time, so it must
        // serialize into the scene asset ([SerializeField], not runtime-only).
        [SerializeField] private Vector2[] _patrolWaypoints;
        [SerializeField] private bool _patrolClosed;
        [SerializeField] private Vector2 _postPosition;
        [SerializeField] private Vector2 _postFacing = Vector2.right;
        [SerializeField] private string _postRoomRole = "filler";
        [SerializeField] private bool _hasPatrol, _hasPost;
        [SerializeField] private GuardAlertness _baseline = GuardAlertness.Relaxed;

        private int _patrolIndex;
        private int _patrolDirection = 1;
        private float _pauseRemaining;

        // Live state.
        public AlertnessLadder Ladder { get; private set; }
        public GuardMemory Memory { get; private set; }
        public OrderSlots Orders { get; private set; }
        public GuardConsciousness Consciousness { get; private set; }
        public DutyMode Duty { get; private set; } = DutyMode.Post;
        public GuardAlertness Baseline => _baseline;
        public bool ChannelOpen { get; set; } // Phase 7 wires reports; tests set it directly

        private Vector2 _focusPoint;      // investigate/search/chase target
        private GuardAlertness? _preTransientBaseline; // restored when a transient SetAlertness expires
        private SpriteRenderer _bodyRenderer;
        private Color _bodyColor;
        private float _searchTimer;
        private float _loseSightTimer;
        private float _sightMemoryPush;
        private float _sinceAcceptedOrder = 999f;
        private Order? _pendingStanding;  // deployment orders queue until the beat lands
        private float _pendingLatency;

        public string GuardId => _marker != null ? _marker.GuardId : name;
        public Vector2 Position => _body.position;

        public void EnsureInitialized()
        {
            if (_initialized) return;
            _body = GetComponent<Rigidbody2D>();
            _body.gravityScale = 0f;
            _body.freezeRotation = true;
            _marker = GetComponent<GuardMarker>();
            _vision = GetComponent<GuardVision>();
            _hearing = GetComponent<GuardHearing>();
            _config = GameConfig.Load().Guard;
            Ladder = new AlertnessLadder(_config.Ladder);
            Memory = new GuardMemory(_config.MemoryBufferSeconds);
            Orders = new OrderSlots();
            Consciousness = new GuardConsciousness();
            Consciousness.Woke += OnWake;
            _bodyRenderer = GetComponentInChildren<SpriteRenderer>();
            if (_bodyRenderer != null) _bodyColor = _bodyRenderer.color;
            BadgeSystem.Register(GuardId, _config.BadgeIdPrefix);
            Ladder.StateChanged += (previous, current) =>
                Debug.Log($"[AIRGAP] LADDER guard={GuardId} {previous} -> {current} (suspicion={Ladder.Suspicion:F2})");
            if (_hearing != null) _hearing.Heard += OnHeard;
            if (!Registry.Contains(this)) Registry.Add(this);
            // Re-establish runtime state from the serialized duty wiring: OrderSlots
            // is runtime-only, so the standing order must be rebuilt on scene load.
            if (_hasPatrol)
            {
                Orders.SetStanding(Order.Patrol("authored"));
                Duty = DutyMode.Patrol;
            }
            else if (_hasPost)
            {
                Orders.SetStanding(Order.GuardPoint("authored"));
                Duty = DutyMode.Post;
            }
            if (_hearing != null) _hearing.SetAlertness(_baseline);
            _initialized = true;
        }

        private void Awake() => EnsureInitialized();
        private void OnDestroy() => Registry.Remove(this);

        private void FixedUpdate()
        {
            if (Application.isPlaying) Tick(Time.fixedDeltaTime);
        }

        // ---- loader wiring ---------------------------------------------------

        // Loader-time wiring: serialized fields ONLY — these run in edit mode where
        // no runtime state exists. EnsureInitialized rebuilds Duty/Orders from them.
        public void SetPatrol(Vector2[] worldWaypoints, bool closed)
        {
            _patrolWaypoints = worldWaypoints;
            _patrolClosed = closed;
            _hasPatrol = true;
        }

        public void SetPost(Vector2 worldPosition, Vector2 worldFacing, string roomRole)
        {
            _postPosition = worldPosition;
            _postFacing = worldFacing.normalized;
            _postRoomRole = string.IsNullOrEmpty(roomRole) ? "filler" : roomRole;
            _hasPost = true;
        }

        public void SetBaseline(GuardAlertness level)
        {
            _baseline = level;
            if (_hearing != null) _hearing.SetAlertness(level);
        }

        // ---- the tick --------------------------------------------------------

        public void Tick(float deltaTime)
        {
            EnsureInitialized();
            if (CaptureSystem.IsCaptured)
            {
                _body.linearVelocity = Vector2.zero;
                return;
            }

            Memory.Tick(deltaTime);
            Consciousness.Tick(deltaTime);
            if (Consciousness.IsDown)
            {
                // Down = inert. No perception, no movement, no orders — and no
                // event to the Warden: indistinguishable, without a status check,
                // from a guard standing still.
                _body.linearVelocity = Vector2.zero;
                return;
            }
            _sinceAcceptedOrder += deltaTime;
            TickPendingStanding(deltaTime);
            if (Orders.Tick(deltaTime))
            {
                Debug.Log($"[AIRGAP] ORDER guard={GuardId} transient expired -> reverting to standing order");
                if (_preTransientBaseline.HasValue)
                {
                    SetBaseline(_preTransientBaseline.Value);
                    _preTransientBaseline = null;
                }
                ResumeStandingDuty();
            }

            VisibilityCategory sight = _vision != null ? _vision.Tick() : VisibilityCategory.None;
            var player = Object.FindFirstObjectByType<InfiltratorController>();
            TickSightMemory(deltaTime, sight, player);
            Ladder.Tick(deltaTime, sight, Baseline);

            if (player != null && TryCapture(sight, player)) return;

            TickDuty(deltaTime, sight, player);
        }

        private void TickSightMemory(float deltaTime, VisibilityCategory sight, InfiltratorController player)
        {
            if (sight == VisibilityCategory.None || player == null) return;
            _sightMemoryPush -= deltaTime;
            if (_sightMemoryPush > 0f) return;
            _sightMemoryPush = 0.5f;
            float confidence = sight == VisibilityCategory.Clear ? 1f
                : sight == VisibilityCategory.Partial ? 0.6f : 0.3f;
            Memory.Push(MemoryEventType.Sight, player.Position, sight.ToString().ToLowerInvariant(), confidence);
            _focusPoint = player.Position;
        }

        private void OnHeard(GuardHearing.HearingResult result)
        {
            if (!result.Noticed || !_initialized || Consciousness.IsDown) return;
            Memory.Push(MemoryEventType.Sound, result.Sound.Position, result.Sound.Type,
                Mathf.Clamp01(result.Probability));
            Ladder.OnNoticedSound(Baseline);
            if (Ladder.State >= GuardAlertState.Suspicious && Duty != DutyMode.Chase)
                _focusPoint = result.Sound.Position;
        }

        private bool TryCapture(VisibilityCategory sight, InfiltratorController player)
        {
            float distance = Vector2.Distance(_body.position, player.Position);

            // Close-range clear sighting captures at ANY rung — no reaction window.
            // This is the rule that makes the pry bar a positioning tool (Phase 10).
            if (sight == VisibilityCategory.Clear && distance <= _config.CloseRangeCaptureDistance)
            {
                CaptureSystem.Capture(GuardId, "close-range clear sighting");
                _body.linearVelocity = Vector2.zero;
                return true;
            }
            if (Ladder.State == GuardAlertState.Alarmed && distance <= _config.Duty.MeleeReachTiles)
            {
                CaptureSystem.Capture(GuardId, "alarmed guard reached you");
                _body.linearVelocity = Vector2.zero;
                return true;
            }
            return false;
        }

        // ---- duty execution --------------------------------------------------

        private void TickDuty(float deltaTime, VisibilityCategory sight, InfiltratorController player)
        {
            switch (Ladder.State)
            {
                case GuardAlertState.Alarmed:
                    Duty = DutyMode.Chase;
                    TickChase(deltaTime, sight, player);
                    return;

                case GuardAlertState.Searching:
                    if (Duty != DutyMode.Search && Duty != DutyMode.Investigate)
                    {
                        Duty = DutyMode.Search;
                        _searchTimer = 0f;
                    }
                    TickSearch(deltaTime);
                    return;

                case GuardAlertState.Suspicious:
                    // Pause and stare at what bothered you.
                    _body.linearVelocity = Vector2.zero;
                    FaceToward(_focusPoint);
                    return;

                default:
                    if (Orders.TransientOrder?.Type == OrderType.HoldPosition)
                    {
                        // Hold means hold: stand where you are until the TTL reverts you.
                        _body.linearVelocity = Vector2.zero;
                        return;
                    }
                    if (Duty == DutyMode.Chase || Duty == DutyMode.Search)
                        ResumeStandingDuty();
                    if (Duty == DutyMode.Investigate) TickInvestigate(deltaTime);
                    else if (Duty == DutyMode.Patrol && _hasPatrol) TickPatrol(deltaTime);
                    else TickPost();
                    return;
            }
        }

        private void TickChase(float deltaTime, VisibilityCategory sight, InfiltratorController player)
        {
            if (sight != VisibilityCategory.None && player != null)
            {
                _focusPoint = player.Position;
                _loseSightTimer = 0f;
            }
            else
            {
                _loseSightTimer += deltaTime;
                if (_loseSightTimer >= _config.Duty.ChaseLoseSightSeconds)
                {
                    Debug.Log($"[AIRGAP] LADDER guard={GuardId} lost the target — dropping to Searching");
                    Ladder.DropOneRung();
                    Duty = DutyMode.Search;
                    _searchTimer = 0f;
                    return;
                }
            }
            MoveToward(_focusPoint, _config.ChaseSpeed);
        }

        private void TickSearch(float deltaTime)
        {
            if (Vector2.Distance(_body.position, _focusPoint) > _config.Duty.WaypointTolerance)
            {
                MoveToward(_focusPoint, _config.InvestigateSpeed);
                return;
            }
            _body.linearVelocity = Vector2.zero;
            _searchTimer += deltaTime;
            transform.right = Quaternion.Euler(0f, 0f, 90f * deltaTime) * transform.right; // scan
            // The ladder decays on its own; when it drops below Searching the state
            // machine routes back through TickDuty.
        }

        private void TickInvestigate(float deltaTime)
        {
            if (Vector2.Distance(_body.position, _focusPoint) > _config.Duty.WaypointTolerance)
            {
                MoveToward(_focusPoint, _config.InvestigateSpeed);
                return;
            }
            _body.linearVelocity = Vector2.zero;
            _searchTimer += deltaTime;
            transform.right = Quaternion.Euler(0f, 0f, 90f * deltaTime) * transform.right;
            if (_searchTimer >= _config.Duty.InvestigateSeconds)
            {
                Orders.ClearTransient();
                ResumeStandingDuty();
            }
        }

        private void TickPatrol(float deltaTime)
        {
            if (_patrolWaypoints == null || _patrolWaypoints.Length == 0) { TickPost(); return; }

            if (_pauseRemaining > 0f)
            {
                _pauseRemaining -= deltaTime;
                _body.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 target = _patrolWaypoints[_patrolIndex];
            if (Vector2.Distance(_body.position, target) <= _config.Duty.WaypointTolerance)
            {
                _pauseRemaining = _config.Duty.PatrolPauseSeconds;
                if (_patrolClosed)
                {
                    _patrolIndex = (_patrolIndex + 1) % _patrolWaypoints.Length;
                }
                else
                {
                    // Out-and-back: bounce at the ends.
                    if (_patrolIndex == _patrolWaypoints.Length - 1) _patrolDirection = -1;
                    else if (_patrolIndex == 0) _patrolDirection = 1;
                    _patrolIndex += _patrolDirection;
                }
                return;
            }
            MoveToward(target, _config.PatrolSpeed);
        }

        private void TickPost()
        {
            if (!_hasPost)
            {
                _body.linearVelocity = Vector2.zero;
                return;
            }
            if (Vector2.Distance(_body.position, _postPosition) > _config.Duty.WaypointTolerance)
            {
                MoveToward(_postPosition, _config.PatrolSpeed);
                return;
            }
            _body.linearVelocity = Vector2.zero;
            transform.right = _postFacing;
        }

        private void MoveToward(Vector2 target, float speed)
        {
            Vector2 toTarget = target - _body.position;
            if (toTarget.sqrMagnitude < 0.0001f) { _body.linearVelocity = Vector2.zero; return; }
            Vector2 direction = toTarget.normalized;
            _body.linearVelocity = direction * speed;
            transform.right = direction;
        }

        private void FaceToward(Vector2 point)
        {
            Vector2 toPoint = point - _body.position;
            if (toPoint.sqrMagnitude > 0.01f) transform.right = toPoint.normalized;
        }

        private void ResumeStandingDuty()
        {
            Duty = _hasPatrol && Orders.StandingOrder?.Type == OrderType.Patrol ? DutyMode.Patrol : DutyMode.Post;
            _searchTimer = 0f;
            _loseSightTimer = 0f;
        }

        // ---- consciousness (Phase 5) -----------------------------------------

        /// <summary>
        /// Silent takedown (Phase 10's pry bar calls this; validators call it now).
        /// Enqueues NO report — a takedown is not, by itself, information.
        /// </summary>
        public void TakeDown(float? seconds = null)
        {
            EnsureInitialized();
            Consciousness.GoDown(seconds ?? _config.DownDurationSeconds);
            _body.linearVelocity = Vector2.zero;
            if (_hearing != null) _hearing.Suppressed = true;
            if (_bodyRenderer != null) _bodyRenderer.color = _bodyColor * 0.35f;
            Debug.Log($"[AIRGAP] DOWN guard={GuardId} for {Consciousness.DownRemaining:F0}s (no disclosure)");
        }

        private void OnWake()
        {
            if (_hearing != null) _hearing.Suppressed = false;
            if (_bodyRenderer != null) _bodyRenderer.color = _bodyColor;

            // Wake at Searching, focused on where it happened — they know they
            // were attacked, they just don't know by whom or from where.
            Ladder.ForceState(GuardAlertState.Searching, _config.Ladder.SearchingThreshold);
            _focusPoint = _body.position;
            Duty = DutyMode.Search;
            _searchTimer = 0f;

            // The unconditional incident report — fires whether or not the Warden
            // ever ran a status check — and the second Badge Flag confirmation path.
            ReportPipeline.Enqueue(new ReportRequest
            {
                GuardId = GuardId,
                Tag = "recovering-from-attack",
                Position = _body.position
            });
            BadgeSystem.ConfirmUnresponsive(GuardId);
        }

        /// <summary>
        /// The badge plausibility comparison set: a post guard is expected at
        /// their post; a patrol guard anywhere along their authored loop.
        /// </summary>
        public IEnumerable<Vector2> ExpectedDutyPoints()
        {
            if (_hasPost) { yield return _postPosition; yield break; }
            if (_patrolWaypoints != null)
                foreach (Vector2 waypoint in _patrolWaypoints) yield return waypoint;
            else yield return _body != null ? _body.position : (Vector2)transform.position;
        }

        // ---- orders ----------------------------------------------------------

        public OrderContext BuildOrderContext() => new OrderContext
        {
            SightWithinTwoSeconds = Memory.HasSightWithin(2f) ||
                (_vision != null && _vision.Current != VisibilityCategory.None),
            PostRoomRole = _postRoomRole,
            ChannelOpen = ChannelOpen,
            SecondsSinceAcceptedOrder = _sinceAcceptedOrder,
            Ladder = Ladder.State
        };

        public List<OrderType> OfferableOrders()
        {
            EnsureInitialized();
            return GuardOrderRules.OfferableOrders(Ladder.State, Memory.TryLatestLocatedEvent(out _), ChannelOpen);
        }

        /// <summary>
        /// Apply an order that EvaluateOrder already approved. forceTransient is
        /// the ONE thing the transmitter's identity changes: a spoofed deployment
        /// is always transient (Phase 10's constraint, built now for free).
        /// </summary>
        public void ApplyOrder(in Order order, bool forceTransient)
        {
            EnsureInitialized();
            _sinceAcceptedOrder = 0f;

            if (GuardOrderRules.IsDeployment(order.Type) && !forceTransient)
            {
                _pendingStanding = order;
                _pendingLatency = _config.Orders.DeploymentLatencySeconds;
                return;
            }

            Orders.SetTransient(order, _config.Orders.TransientTtlSeconds);
            ApplyTransientEffect(order);
        }

        private void TickPendingStanding(float deltaTime)
        {
            if (!_pendingStanding.HasValue) return;
            _pendingLatency -= deltaTime;
            if (_pendingLatency > 0f) return;

            Order order = _pendingStanding.Value;
            _pendingStanding = null;
            Orders.SetStanding(order);
            ApplyStandingEffect(order);
        }

        private void ApplyStandingEffect(in Order order)
        {
            switch (order.Type)
            {
                case OrderType.Patrol:
                    // Loop retarget by id needs the loader's waypoint table (Phase 6's
                    // deployment UI wires it); an authored self-loop keeps current data.
                    Duty = _hasPatrol ? DutyMode.Patrol : DutyMode.Post;
                    break;
                case OrderType.GuardPoint:
                    Duty = DutyMode.Post;
                    break;
                case OrderType.SetAlertness:
                    SetBaseline(order.AlertnessLevel);
                    break;
            }
        }

        private void ApplyTransientEffect(in Order order)
        {
            switch (order.Type)
            {
                case OrderType.Investigate:
                    if (Memory.TryLatestLocatedEvent(out MemoryEntry entry))
                    {
                        _focusPoint = entry.Position;
                        Duty = DutyMode.Investigate;
                        _searchTimer = 0f;
                    }
                    break;
                case OrderType.HoldPosition:
                    _body.linearVelocity = Vector2.zero; // TickDuty holds while the transient lives
                    break;
                case OrderType.Disregard:
                    Ladder.DropOneRung();
                    Orders.ClearTransient();
                    ResumeStandingDuty();
                    break;
                case OrderType.SetAlertness:
                    // Transient alertness reverts with the TTL — the Warden's last
                    // AUTHENTICATED baseline is on record; a voice on the radio isn't.
                    _preTransientBaseline ??= Baseline;
                    SetBaseline(order.AlertnessLevel);
                    break;
                case OrderType.GuardPoint:
                case OrderType.Patrol:
                    // Spoofed deployment: act on it for the TTL window only.
                    ApplyStandingEffect(order);
                    break;
                    // SayAgain / WhosNearYou produce radio lines (Phase 7), no movement.
            }
        }
    }
}
