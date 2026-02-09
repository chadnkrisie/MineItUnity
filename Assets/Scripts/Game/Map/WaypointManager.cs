using UnityEngine;

namespace MineItUnity.Game.Map
{
    /// <summary>
    /// Presentation-layer waypoint state (Unity-side).
    /// Shared by map (click-to-set) and world compass arrow.
    /// </summary>
    public sealed class WaypointManager : MonoBehaviour
    {
        public bool HasWaypoint { get; private set; }
        public int WaypointTx { get; private set; }
        public int WaypointTy { get; private set; }
        // Set by WorldMapController so other UI (compass) can react.
        public bool IsMapOpen { get; set; }

        public void SetWaypoint(int tx, int ty)
        {
            HasWaypoint = true;
            WaypointTx = tx;
            WaypointTy = ty;
        }

        public void ClearWaypoint()
        {
            HasWaypoint = false;
            WaypointTx = 0;
            WaypointTy = 0;
        }
    }
}
