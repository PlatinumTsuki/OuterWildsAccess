using System.Collections.Generic;
using UnityEngine;

namespace OuterWildsAccess
{
    /// <summary>
    /// Accessibility for elevators. Sighted players can see whether an elevator
    /// is moving, in which direction, and when it stops. A blind player hears
    /// the motor audio but has no reliable way to tell direction or arrival,
    /// and — worse — can walk into an empty shaft and fall to their death.
    ///
    /// Scope:
    ///   - Announces when an elevator NEAR the player starts moving, with
    ///     direction relative to gravity (up or down).
    ///   - Announces when that same elevator stops.
    ///   - "Near the player" means within ~15m of the platform — the platform
    ///     the player is actually riding or standing on. Distant elevators
    ///     starting up are ignored (a sighted player wouldn't see them either
    ///     unless they're nearby).
    ///
    /// Covered elevator types:
    ///   - Elevator (Brittle Hollow crane, Tower of Quantum Knowledge, Timber
    ///     Hearth launch pad, etc.)
    ///   - CageElevator (DLC Stranger cage lifts)
    ///
    /// Not covered (intentional, to keep scope small):
    ///   - NomaiElevator (activated via Nomai slots, rare, different pattern)
    ///   - PrisonCellElevator (DLC dream cell, not a standing platform)
    ///   - DamRaftLift (handled by future Raft handler)
    ///
    /// Implementation: position polling. Every 0.3s, for each Elevator and
    /// CageElevator instance currently loaded, we record the platform's world
    /// position and derive a "moving/not moving" state from the delta to the
    /// previous poll. Direction is derived from the delta projected on the
    /// player's local up vector (gravity-aligned). Announcements fire only
    /// when the platform is within PROXIMITY_RANGE of the player.
    ///
    /// No reflection, no Harmony patches, no allocations per frame.
    /// </summary>
    public class ElevatorHandler
    {
        #region Constants

        private const float PollInterval    = 0.3f;
        private const float MovementEpsilon = 0.02f;  // world-units per poll to count as moving
        private const float ProximityRange  = 15f;    // "player is using this elevator"

        #endregion

        #region Tracking state

        private class ElevatorTrack
        {
            public Transform Platform;
            public Vector3   LastLocalPosition;  // relative to parent — stable when idle on a rotating planet
            public bool      WasMoving;
            public bool      Seeded;             // first observation done
        }

        private readonly Dictionary<int, ElevatorTrack> _tracked = new Dictionary<int, ElevatorTrack>();
        private float _nextPollTime;

        #endregion

        #region Lifecycle

        /// <summary>Initializes the handler. Call from Main.Start().</summary>
        public void Initialize()
        {
            _nextPollTime = 0f;
            _tracked.Clear();
            DebugLogger.Log(LogCategory.State, "ElevatorHandler", "Initialisé");
        }

        /// <summary>Clears state. Call from Main.OnDestroy().</summary>
        public void Cleanup()
        {
            _tracked.Clear();
        }

        /// <summary>Call from Main.Update().</summary>
        public void Update()
        {
            if (Time.time < _nextPollTime) return;
            _nextPollTime = Time.time + PollInterval;

            var playerTr = Locator.GetPlayerTransform();
            if (playerTr == null)
            {
                if (_tracked.Count > 0) _tracked.Clear();
                return;
            }

            var elevators  = Object.FindObjectsOfType<Elevator>();
            var cageLifts  = Object.FindObjectsOfType<CageElevator>();

            int total = (elevators != null ? elevators.Length : 0)
                      + (cageLifts != null ? cageLifts.Length : 0);

            if (total == 0)
            {
                if (_tracked.Count > 0) _tracked.Clear();
                return;
            }

            var seenIds = new HashSet<int>();
            Vector3 playerPos = playerTr.position;
            Vector3 playerUp  = playerTr.up;

            if (elevators != null)
            {
                for (int i = 0; i < elevators.Length; i++)
                {
                    ProcessPlatform(elevators[i], elevators[i] != null ? elevators[i].transform : null,
                                    playerPos, playerUp, seenIds);
                }
            }

            if (cageLifts != null)
            {
                for (int i = 0; i < cageLifts.Length; i++)
                {
                    ProcessPlatform(cageLifts[i], cageLifts[i] != null ? cageLifts[i].transform : null,
                                    playerPos, playerUp, seenIds);
                }
            }

            // Cull stale entries (elevators no longer loaded).
            if (seenIds.Count != _tracked.Count)
            {
                var stale = new List<int>();
                foreach (var kv in _tracked)
                {
                    if (!seenIds.Contains(kv.Key)) stale.Add(kv.Key);
                }
                for (int i = 0; i < stale.Count; i++) _tracked.Remove(stale[i]);
            }
        }

        #endregion

        #region Per-platform logic

        private void ProcessPlatform(
            Object owner,
            Transform platform,
            Vector3 playerPos,
            Vector3 playerUp,
            HashSet<int> seenIds)
        {
            if (owner == null || platform == null) return;
            if (!platform.gameObject.activeInHierarchy) return;

            int id = owner.GetInstanceID();
            seenIds.Add(id);

            // Movement detection uses LOCAL position so planet rotation does
            // not look like movement. The Elevator class itself drives the
            // platform via localPosition (see decompiled Elevator.cs).
            Vector3 currentLocalPos = platform.localPosition;
            Vector3 currentWorldPos = platform.position;

            if (!_tracked.TryGetValue(id, out ElevatorTrack track))
            {
                track = new ElevatorTrack
                {
                    Platform          = platform,
                    LastLocalPosition = currentLocalPos,
                    WasMoving         = false,
                    Seeded            = true,
                };
                _tracked[id] = track;
                return;  // seed silently
            }

            Vector3 delta     = currentLocalPos - track.LastLocalPosition;
            float   deltaMag  = delta.magnitude;
            bool    isMoving  = deltaMag > MovementEpsilon;

            // Proximity gate: only announce for the elevator the player is on/near.
            // Both player and elevator share world coordinates at the same instant,
            // so this snapshot check is unaffected by planet rotation.
            float distance   = Vector3.Distance(currentWorldPos, playerPos);
            bool  playerNear = distance <= ProximityRange;

            // Transition: stopped → moving
            if (isMoving && !track.WasMoving && playerNear)
            {
                // Direction: convert the local delta to world space, then project
                // onto the player's up axis (gravity-aligned).
                Vector3 worldDelta = platform.parent != null
                    ? platform.parent.TransformVector(delta)
                    : delta;
                float verticalComponent = Vector3.Dot(worldDelta, playerUp);
                string key = verticalComponent >= 0f ? "elevator_going_up" : "elevator_going_down";
                ScreenReader.Say(Loc.Get(key));
                DebugLogger.Log(LogCategory.State, "ElevatorHandler",
                    $"Elevator {id} started: {key}, dist={distance:F1}m");
            }
            // Transition: moving → stopped
            else if (!isMoving && track.WasMoving && playerNear)
            {
                ScreenReader.Say(Loc.Get("elevator_arrived"));
                DebugLogger.Log(LogCategory.State, "ElevatorHandler",
                    $"Elevator {id} arrived, dist={distance:F1}m");
            }

            track.LastLocalPosition = currentLocalPos;
            track.WasMoving         = isMoving;
        }

        #endregion
    }
}
