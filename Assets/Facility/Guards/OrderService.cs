using System;
using AIRGAP.Shared.Data;
using UnityEngine;

namespace AIRGAP.Facility.Guards
{
    public struct OrderResult
    {
        public string GuardId;
        public Order Order;
        public OrderSource Source;
        public bool Offered;      // false = executability gate rejected (never reached plausibility)
        public OrderDecision Decision;
    }

    /// <summary>
    /// The one order channel (Phase 4). Every caller — the Warden's UI (Phase 6)
    /// and the radio spoofer (Phase 10) — goes through IssueOrder. The source is
    /// consumed ONLY by the logger and the transient-durability rule; the
    /// plausibility check cannot see it (OrderContext has no field for it), which
    /// is the spoofer's entire security model.
    /// </summary>
    public static class OrderService
    {
        public static event Action<OrderResult> OrderProcessed;

        /// <summary>Raised when a guard queries an order — Phase 7 turns the reason code into a spoken line.</summary>
        public static event Action<OrderResult> GuardQuery;

        public static OrderResult IssueOrder(string guardId, Order order, OrderSource source)
        {
            GuardAgent agent = GuardAgent.FindById(guardId);
            if (agent == null)
            {
                Debug.LogWarning($"[AIRGAP] ORDER source={source} guard={guardId} — no such guard");
                return new OrderResult { GuardId = guardId, Order = order, Source = source, Offered = false };
            }
            agent.EnsureInitialized();

            var result = new OrderResult { GuardId = guardId, Order = order, Source = source };

            if (agent.Consciousness.IsDown)
            {
                // Radio silence — a Down guard can't answer. The Warden's channel
                // for LEARNING that is the status check or the wake-up report.
                Debug.Log($"[AIRGAP] ORDER source={source} guard={guardId} {order.Type} — no response");
                OrderProcessed?.Invoke(result);
                return result;
            }

            // Executability gate: un-offerable orders never reach the plausibility
            // check — they're simply absent from the UI.
            if (!agent.OfferableOrders().Contains(order.Type))
            {
                result.Offered = false;
                Debug.Log($"[AIRGAP] ORDER source={source} guard={guardId} {order.Type} — not offerable (gate)");
                OrderProcessed?.Invoke(result);
                return result;
            }
            result.Offered = true;

            OrderContext context = agent.BuildOrderContext();
            float debounce = GameConfig.Load().Guard.Orders.DebounceSeconds;
            result.Decision = GuardOrderRules.EvaluateOrder(context, order, debounce);

            if (result.Decision.Obey)
            {
                // The one thing the source changes: a spoofed order is ALWAYS
                // transient, deployment orders included (Phase 10's constraint).
                agent.ApplyOrder(order, forceTransient: source == OrderSource.Spoofed);
                Debug.Log($"[AIRGAP] ORDER source={source} guard={guardId} {order.Type} -> obeyed");
            }
            else
            {
                Debug.Log($"[AIRGAP] ORDER source={source} guard={guardId} {order.Type} -> QUERY ({result.Decision.Reason})");
                GuardQuery?.Invoke(result);
            }

            OrderProcessed?.Invoke(result);
            return result;
        }

        /// <summary>Test hook: drop subscribers between validator runs.</summary>
        public static void Reset()
        {
            OrderProcessed = null;
            GuardQuery = null;
        }
    }
}
