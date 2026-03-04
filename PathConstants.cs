namespace OuterWildsAccess
{
    /// <summary>
    /// Shared constants for pathfinding, guidance, and auto-walk.
    /// Centralised here to prevent mismatches between handlers.
    /// </summary>
    public static class PathConstants
    {
        /// <summary>Horizontal distance to consider the player has arrived at the target.</summary>
        public const float ArrivalDist = 1.2f;

        /// <summary>Max vertical distance to still count as arrived (otherwise "out of reach").</summary>
        public const float ArrivalVertMax = 3f;

        /// <summary>Horizontal distance to advance to the next waypoint along a path.</summary>
        public const float WaypointReachDist = 1.5f;

        /// <summary>Below this horizontal distance, skip A* and walk straight toward the target.</summary>
        public const float DirectPathDist = 2f;
    }
}
