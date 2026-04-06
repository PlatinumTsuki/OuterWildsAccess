using UnityEngine;

namespace OuterWildsAccess
{
    /// <summary>
    /// Accessibility for gravity transitions. Sighted players see the world
    /// visually flip (gravity crystal), see themselves float (zero-g), or see
    /// debris drift to signal weightlessness. None of that is audible.
    ///
    /// Scope:
    ///   1. Zero-g entry — critical: the player needs to know to switch to
    ///      the jetpack, otherwise WASD does nothing and they get stuck.
    ///   2. Gravity restored — the player can walk normally again.
    ///   3. Gravity flip — a Nomai gravity crystal has just reoriented the
    ///      player onto a new surface (wall/ceiling).
    ///
    /// Filters (to stay equivalent to what a sighted player would notice):
    ///   - Not announced while inside the ship or at the flight console.
    ///     Ship interior gravity is local to the ship; sighted players don't
    ///     see anything flip there. The same applies to piloting.
    ///   - Not announced during a normal jump or jetpack burst. Those don't
    ///     fire the alignment events because the player is still inside a
    ///     gravity field.
    ///   - Flip announcements require a sustained (2s) cooldown so that
    ///     micro-rotations (e.g. on curved terrain) don't spam.
    ///
    /// Implementation:
    ///   - Event subscription on "InitPlayerForceAlignment" and
    ///     "BreakPlayerForceAlignment" for zero-g enter/exit — these fire
    ///     exactly when the game considers the player has lost or regained
    ///     alignment with a gravity field.
    ///   - Polling every 0.3s for the gravity direction (read from the
    ///     player's ForceDetector). A direction change &gt; 60° while still
    ///     under meaningful gravity = flip.
    ///
    /// No Harmony patches, no reflection.
    /// </summary>
    public class GravityHandler
    {
        #region Constants

        private const float PollInterval       = 0.3f;
        private const float FlipAngleThreshold = 60f;
        private const float FlipMinMagnitude   = 0.5f;    // m/s²: ignore micro-forces
        private const float FlipCooldown       = 2f;

        #endregion

        #region State

        private float   _nextPollTime;
        private Vector3 _lastGravityDirection;
        private bool    _hasLastDirection;
        private float   _lastFlipAnnounceTime = -10f;

        #endregion

        #region Lifecycle

        /// <summary>Subscribes to gravity alignment events. Call from Main.Start().</summary>
        public void Initialize()
        {
            GlobalMessenger.AddListener("InitPlayerForceAlignment",  OnInitPlayerForceAlignment);
            GlobalMessenger.AddListener("BreakPlayerForceAlignment", OnBreakPlayerForceAlignment);
            DebugLogger.Log(LogCategory.State, "GravityHandler", "Initialisé");
        }

        /// <summary>Unsubscribes. Call from Main.OnDestroy().</summary>
        public void Cleanup()
        {
            GlobalMessenger.RemoveListener("InitPlayerForceAlignment",  OnInitPlayerForceAlignment);
            GlobalMessenger.RemoveListener("BreakPlayerForceAlignment", OnBreakPlayerForceAlignment);
            _hasLastDirection = false;
        }

        /// <summary>Polls the gravity direction for flip detection. Call from Main.Update().</summary>
        public void Update()
        {
            if (Time.time < _nextPollTime) return;
            _nextPollTime = Time.time + PollInterval;

            // Don't announce flips while piloting or inside the ship — ship
            // gravity is local and would constantly look flipped relative
            // to world coordinates.
            if (IsPlayerBusyInShip()) { _hasLastDirection = false; return; }

            var body = Locator.GetPlayerBody();
            if (body == null) { _hasLastDirection = false; return; }

            var detector = body.GetAttachedForceDetector();
            if (detector == null) { _hasLastDirection = false; return; }

            Vector3 accel     = detector.GetForceAcceleration();
            float   magnitude = accel.magnitude;

            // Below threshold → no meaningful gravity, drop tracking so the
            // next non-zero reading becomes a fresh baseline.
            if (magnitude < FlipMinMagnitude)
            {
                _hasLastDirection = false;
                return;
            }

            Vector3 currentDir = accel / magnitude;

            if (_hasLastDirection)
            {
                float angle = Vector3.Angle(_lastGravityDirection, currentDir);
                if (angle >= FlipAngleThreshold
                    && Time.time - _lastFlipAnnounceTime >= FlipCooldown)
                {
                    _lastFlipAnnounceTime = Time.time;
                    ScreenReader.Say(Loc.Get("gravity_flipped"));
                    DebugLogger.Log(LogCategory.State, "GravityHandler",
                        $"Flip détecté: angle={angle:F0}°, mag={magnitude:F2}");
                }
            }

            _lastGravityDirection = currentDir;
            _hasLastDirection     = true;
        }

        #endregion

        #region Event handlers

        /// <summary>
        /// Fired when the player regains alignment with a gravity field.
        /// Equivalent to a sighted player seeing the world settle after a
        /// weightless moment.
        /// </summary>
        private void OnInitPlayerForceAlignment()
        {
            if (IsPlayerBusyInShip()) return;
            ScreenReader.Say(Loc.Get("gravity_restored"));
            DebugLogger.Log(LogCategory.State, "GravityHandler", "Gravité rétablie");
        }

        /// <summary>
        /// Fired when the player loses alignment with their gravity field.
        /// Equivalent to a sighted player seeing themselves start to float.
        /// </summary>
        private void OnBreakPlayerForceAlignment()
        {
            if (IsPlayerBusyInShip()) return;
            ScreenReader.Say(Loc.Get("gravity_zero"));
            DebugLogger.Log(LogCategory.State, "GravityHandler", "Entrée en zéro-g");
        }

        #endregion

        #region Helpers

        /// <summary>
        /// True if the player is inside the ship cabin or piloting from the
        /// flight console. Gravity transitions inside those states are
        /// local to the ship and shouldn't trigger world-level announcements.
        /// </summary>
        private static bool IsPlayerBusyInShip()
        {
            try
            {
                return PlayerState.IsInsideShip() || PlayerState.AtFlightConsole();
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
