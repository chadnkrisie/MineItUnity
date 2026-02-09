using UnityEngine;
using MineIt.Mining;

namespace MineItUnity.Game.Map
{
    /// <summary>
    /// Auto-sets a waypoint after a scan executes, but ONLY if no waypoint is currently set.
    /// Presentation-only: reads core scan results and drives Unity-owned waypoint state.
    /// </summary>
    public sealed class AutoWaypointOnScanController : MonoBehaviour
    {
        [Header("References")]
        public GameController Controller;
        public WaypointManager Waypoints;

        [Header("Behavior")]
        [Tooltip("If true, auto-waypoint runs even if the map is open. If false, we skip while map is open.")]
        public bool AllowWhileMapOpen = false;

        [Tooltip("If true, auto-waypoint will only fire when scan returns at least one hit.")]
        public bool RequireAtLeastOneHit = true;

        private bool _subscribed;

        private void Awake()
        {
            if (Controller == null)
                Controller = FindObjectOfType<GameController>();

            if (Waypoints == null)
                Waypoints = FindObjectOfType<WaypointManager>();
        }

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            // In case script enable order is weird, keep trying until we succeed.
            if (!_subscribed)
                TrySubscribe();
        }

        private void TrySubscribe()
        {
            if (_subscribed) return;
            if (Controller == null || Controller.Session == null) return;

            Controller.Session.ScanExecuted += OnScanExecuted;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (Controller == null || Controller.Session == null) { _subscribed = false; return; }

            Controller.Session.ScanExecuted -= OnScanExecuted;
            _subscribed = false;
        }

        private void OnScanExecuted()
        {
            if (Controller == null || Controller.Session == null) return;
            if (Waypoints == null) return;

            // Only if no waypoint is set already
            if (Waypoints.HasWaypoint) return;

            // Optional: skip while map open (prevents surprise UI changes)
            if (!AllowWhileMapOpen && Waypoints.IsMapOpen) return;

            var s = Controller.Session;
            var results = s.LastScanResults;

            if (results == null) return;
            if (RequireAtLeastOneHit && results.Count == 0) return;

            // Choose best target:
            // 1) highest signal bars
            // 2) nearest to player (tile distance)
            int pTx = Mathf.FloorToInt((float)s.Player.PositionX);
            int pTy = Mathf.FloorToInt((float)s.Player.PositionY);

            int bestBars = -1;
            int bestDist2 = int.MaxValue;
            int bestTx = 0, bestTy = 0;
            bool found = false;

            for (int i = 0; i < results.Count; i++)
            {
                ScanResult r = results[i];
                if (r == null) continue;

                int tx = r.CenterTx;
                int ty = r.CenterTy;

                int dx = tx - pTx;
                int dy = ty - pTy;
                int dist2 = dx * dx + dy * dy;

                int bars = r.SignalBars;

                if (!found ||
                    bars > bestBars ||
                    (bars == bestBars && dist2 < bestDist2))
                {
                    found = true;
                    bestBars = bars;
                    bestDist2 = dist2;
                    bestTx = tx;
                    bestTy = ty;
                }
            }

            if (!found) return;

            Waypoints.SetWaypoint(bestTx, bestTy);

            // Optional user feedback (core HUD status line)
            s.PostStatus($"AUTO WAYPOINT: Scan target ({bestBars} bars)", 1.5);
        }
    }
}
