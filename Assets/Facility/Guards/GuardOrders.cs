using System.Collections.Generic;

namespace AIRGAP.Facility.Guards
{
    public enum OrderType
    {
        GuardPoint,
        Patrol,
        SetAlertness,
        Investigate,
        HoldPosition,
        Disregard,
        SayAgain,
        WhosNearYou
    }

    public enum OrderSource
    {
        Warden,
        Spoofed
    }

    public enum QueryReason
    {
        None,
        LiveSighting,     // "Control, negative, I'm looking right at—"
        HighSecurityPost, // "Say again? I'm supposed to leave the vault?"
        NoOpenChannel,    // "Control, I didn't call anything in."
        JustOrdered,      // "Control, you just told me—"
        Alarmed           // chasing guards take no orders of any kind
    }

    public struct Order
    {
        public OrderType Type;
        public string TargetId;               // guardPost id / patrol id / null
        public GuardAlertness AlertnessLevel; // SetAlertness payload

        public static Order GuardPoint(string postId) => new Order { Type = OrderType.GuardPoint, TargetId = postId };
        public static Order Patrol(string patrolId) => new Order { Type = OrderType.Patrol, TargetId = patrolId };
        public static Order SetAlertness(GuardAlertness level) => new Order { Type = OrderType.SetAlertness, AlertnessLevel = level };
        public static Order Of(OrderType type) => new Order { Type = type };
    }

    /// <summary>
    /// Everything EvaluateOrder is allowed to know. Deliberately contains no
    /// transmitter identity and no randomness inputs — compliance must be
    /// decided identically for every caller (the spoofer's entire security
    /// model, asserted by ValidatePhase4).
    /// </summary>
    public struct OrderContext
    {
        public bool SightWithinTwoSeconds;   // guard's sight state != none in the last ~2s
        public string PostRoomRole;          // resolved role of the guard's current post room ("vault"/"power"/"ops"/"filler"/null)
        public bool ChannelOpen;             // a report window is open (Phase 7 wires this; closed until then)
        public float SecondsSinceAcceptedOrder;
        public GuardAlertState Ladder;
    }

    public struct OrderDecision
    {
        public bool Obey;
        public QueryReason Reason;

        public static readonly OrderDecision Obeyed = new OrderDecision { Obey = true, Reason = QueryReason.None };
        public static OrderDecision Query(QueryReason reason) => new OrderDecision { Obey = false, Reason = reason };
    }

    /// <summary>
    /// Phase 4 order rules: pure functions, no Unity lifecycle, no RNG, no
    /// personality input, no knowledge of who transmitted. The predicate list is
    /// evaluated in DEVELOPMENT.md's authored order; a query is an outcome, not
    /// a failure mode.
    /// </summary>
    public static class GuardOrderRules
    {
        public static readonly OrderType[] DeploymentOrders =
            { OrderType.GuardPoint, OrderType.Patrol, OrderType.SetAlertness };

        public static readonly OrderType[] ResponseOrders =
            { OrderType.Investigate, OrderType.HoldPosition, OrderType.Disregard, OrderType.SayAgain, OrderType.WhosNearYou };

        public static bool IsDeployment(OrderType type) =>
            type == OrderType.GuardPoint || type == OrderType.Patrol || type == OrderType.SetAlertness;

        public static bool IsResponse(OrderType type) => !IsDeployment(type);

        private static bool IsHighSecurityRole(string role) =>
            role == "vault" || role == "power" || role == "ops";

        /// <summary>
        /// The deterministic plausibility check. Identical for every caller;
        /// there is intentionally no way to pass a source.
        /// </summary>
        public static OrderDecision EvaluateOrder(in OrderContext ctx, in Order order, float debounceSeconds)
        {
            // 1. You cannot dismiss what you are looking at.
            if ((order.Type == OrderType.Disregard || order.Type == OrderType.HoldPosition) &&
                ctx.SightWithinTwoSeconds)
                return OrderDecision.Query(QueryReason.LiveSighting);

            // 2. High-security posts are not abandoned on a voice order.
            if ((order.Type == OrderType.GuardPoint || order.Type == OrderType.Patrol) &&
                IsHighSecurityRole(ctx.PostRoomRole))
                return OrderDecision.Query(QueryReason.HighSecurityPost);

            // 3. Control cannot answer a report that was never made.
            if (IsResponse(order.Type) && !ctx.ChannelOpen)
                return OrderDecision.Query(QueryReason.NoOpenChannel);

            // 4. Debounce / anti-stacking.
            if (ctx.SecondsSinceAcceptedOrder < debounceSeconds)
                return OrderDecision.Query(QueryReason.JustOrdered);

            // 5. Chasing guards take no orders of any kind.
            if (ctx.Ladder == GuardAlertState.Alarmed)
                return OrderDecision.Query(QueryReason.Alarmed);

            return OrderDecision.Obeyed;
        }

        /// <summary>
        /// The executability gate: an order that cannot be performed is never
        /// OFFERED — it doesn't reach the plausibility check, it's simply absent
        /// from the UI (Phase 6 renders this list).
        /// </summary>
        public static List<OrderType> OfferableOrders(GuardAlertState ladder, bool hasLocatedMemory, bool channelOpen)
        {
            var offerable = new List<OrderType>
            {
                OrderType.GuardPoint,
                OrderType.Patrol,
                OrderType.SetAlertness,
                OrderType.WhosNearYou
            };
            if (hasLocatedMemory) offerable.Add(OrderType.Investigate);
            if (ladder == GuardAlertState.Suspicious || ladder == GuardAlertState.Searching)
            {
                offerable.Add(OrderType.HoldPosition);
                offerable.Add(OrderType.Disregard);
            }
            if (channelOpen) offerable.Add(OrderType.SayAgain);
            return offerable;
        }
    }

    /// <summary>
    /// Standing vs transient order slots (Phase 4). Deployment orders overwrite
    /// the durable standing order; response orders apply as a transient with a
    /// TTL, on whose expiry the guard reverts to standing. This one distinction
    /// is what later makes the spoofer temporary (Phase 10) — the spoofer issues
    /// EVERYTHING as transient, including deployments.
    /// </summary>
    public class OrderSlots
    {
        public Order? StandingOrder { get; private set; }
        public Order? TransientOrder { get; private set; }
        public float TransientRemaining { get; private set; }

        public Order? Active => TransientOrder ?? StandingOrder;
        public bool TransientActive => TransientOrder.HasValue;

        public void SetStanding(in Order order) => StandingOrder = order;

        public void SetTransient(in Order order, float ttlSeconds)
        {
            TransientOrder = order;
            TransientRemaining = ttlSeconds;
        }

        /// <summary>Returns true when the transient expired this tick (revert moment).</summary>
        public bool Tick(float deltaTime)
        {
            if (!TransientOrder.HasValue) return false;
            TransientRemaining -= deltaTime;
            if (TransientRemaining > 0f) return false;
            TransientOrder = null;
            TransientRemaining = 0f;
            return true;
        }

        public void ClearTransient()
        {
            TransientOrder = null;
            TransientRemaining = 0f;
        }
    }
}
