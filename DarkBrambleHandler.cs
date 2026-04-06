using System.Collections.Generic;
using UnityEngine;

namespace OuterWildsAccess
{
    /// <summary>
    /// Accessibility for Dark Bramble anglerfish encounters.
    ///
    /// Design principle (fairness with sighted players):
    ///   - Sighted players see anglerfish emerge from the fog at roughly 80m
    ///     (the fog visibility range inside bramble dimensions). They cannot
    ///     see them any earlier.
    ///   - Sighted players also hear the same 3D audio cues the blind player
    ///     hears (already handled by the game's AnglerfishAudioController).
    ///   - Anglerfish are canonically blind and hunt by sound: cutting the
    ///     throttle is the intended "solution" and works identically for both.
    ///
    /// What this handler adds (and ONLY this):
    ///   - A single proximity announcement when an anglerfish enters the 80m
    ///     "visible through fog" range, with direction + distance. This is
    ///     equivalent to a sighted player spotting the silhouette.
    ///   - State-change announcements when an in-range anglerfish transitions
    ///     between Lurking / Investigating / Chasing — equivalent to a sighted
    ///     player seeing the creature suddenly jerk awake or charge.
    ///   - A discreet "lost you" announcement when an in-range anglerfish
    ///     drops back to Lurking.
    ///
    /// What this handler does NOT do:
    ///   - No global map of anglerfish locations.
    ///   - No early warnings beyond 80m.
    ///   - No tracking of direction-of-travel or velocity.
    ///
    /// Implementation:
    ///   - Polls every 0.5s. Iterates AnglerfishController instances currently
    ///     loaded in the scene (only dimensions the player has entered). For
    ///     each one, tracks whether it is currently "in range" and its last
    ///     known state, and announces transitions.
    ///   - No allocations per poll beyond the scan array returned by Unity.
    ///
    /// Always active. Cost negligible: FindObjectsOfType returns empty outside
    /// Dark Bramble, and a single poll runs every 500 ms.
    /// </summary>
    public class DarkBrambleHandler
    {
        #region Constants

        // Fog visibility range inside bramble dimensions.
        private const float FogVisibilityRange = 80f;

        // Polling interval.
        private const float PollInterval = 0.5f;

        #endregion

        #region Per-anglerfish tracking state

        private class AnglerState
        {
            public bool InRange;
            public AnglerfishController.AnglerState LastAiState;
        }

        private readonly Dictionary<int, AnglerState> _tracked = new Dictionary<int, AnglerState>();
        private float _nextPollTime;

        #endregion

        #region Lifecycle

        /// <summary>Initializes the handler. Call from Main.Start().</summary>
        public void Initialize()
        {
            _nextPollTime = 0f;
            _tracked.Clear();
            DebugLogger.Log(LogCategory.State, "DarkBrambleHandler", "Initialisé");
        }

        /// <summary>Clears state. Call from Main.OnDestroy().</summary>
        public void Cleanup()
        {
            _tracked.Clear();
        }

        /// <summary>Call from Main.Update(). Poll-driven — no per-frame cost.</summary>
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

            var anglers = Object.FindObjectsOfType<AnglerfishController>();

            // Early out: no anglerfish loaded → nothing to do, forget any stale state.
            if (anglers == null || anglers.Length == 0)
            {
                if (_tracked.Count > 0) _tracked.Clear();
                return;
            }

            Vector3 playerPos = playerTr.position;

            // Track which instance IDs we saw this poll so we can cull stale entries.
            var seenIds = new HashSet<int>();

            for (int i = 0; i < anglers.Length; i++)
            {
                var angler = anglers[i];
                if (angler == null || !angler.gameObject.activeInHierarchy) continue;

                int id = angler.GetInstanceID();
                seenIds.Add(id);

                float distance = Vector3.Distance(angler.transform.position, playerPos);
                bool inRange = distance <= FogVisibilityRange;
                var aiState = angler.GetAnglerState();

                if (!_tracked.TryGetValue(id, out AnglerState track))
                {
                    // First time we see this instance. Seed silently — we only
                    // announce on transitions, and for proximity we only fire
                    // when the angler *enters* range (so seed with its current
                    // out-of-range state; if already in range, announce now).
                    track = new AnglerState { InRange = false, LastAiState = aiState };
                    _tracked[id] = track;
                }

                // Proximity transition: out-of-range → in-range = "spotted".
                if (inRange && !track.InRange)
                {
                    string dir  = BuildDirectionLabel(playerTr, angler.transform.position);
                    int    dist = Mathf.RoundToInt(distance);
                    ScreenReader.Say(Loc.Get("angler_spotted", dir, dist));
                    DebugLogger.Log(LogCategory.State, "DarkBrambleHandler",
                        $"Angler {id} spotted: {dir}, {dist}m, state={aiState}");
                }

                // State transition while in range = behavioral announcement.
                if (inRange && aiState != track.LastAiState)
                {
                    switch (aiState)
                    {
                        case AnglerfishController.AnglerState.Investigating:
                            ScreenReader.Say(Loc.Get("angler_investigating"));
                            break;
                        case AnglerfishController.AnglerState.Chasing:
                            ScreenReader.Say(Loc.Get("angler_chasing"), SpeechPriority.Now);
                            break;
                        case AnglerfishController.AnglerState.Lurking:
                            // Only announce "lost you" if previously agitated.
                            if (track.LastAiState == AnglerfishController.AnglerState.Investigating
                             || track.LastAiState == AnglerfishController.AnglerState.Chasing)
                            {
                                ScreenReader.Say(Loc.Get("angler_lost"));
                            }
                            break;
                        // Consuming / Stunned: no announcement (consuming = dead player, stunned = stun animation).
                    }
                    DebugLogger.Log(LogCategory.State, "DarkBrambleHandler",
                        $"Angler {id} state: {track.LastAiState} → {aiState}");
                }

                track.InRange    = inRange;
                track.LastAiState = aiState;
            }

            // Cull tracking entries for anglerfish no longer loaded (dimension unloaded).
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

        #region Direction helper

        /// <summary>
        /// Returns a short direction label ("devant", "à droite", "derrière-gauche", …)
        /// from the player to a world position, using the player's current facing.
        /// Simpler than NavigationHandler's version — we just want a single label.
        /// </summary>
        private static string BuildDirectionLabel(Transform playerTr, Vector3 targetPos)
        {
            Vector3 toTarget = targetPos - playerTr.position;
            float forward = Vector3.Dot(toTarget, playerTr.forward);
            float right   = Vector3.Dot(toTarget, playerTr.right);

            // Threshold: anything within 30% of the dominant axis is "diagonal".
            float absF = Mathf.Abs(forward);
            float absR = Mathf.Abs(right);

            bool significantF = absF > absR * 0.4f;
            bool significantR = absR > absF * 0.4f;

            string f = forward >= 0f ? Loc.Get("nav_north") : Loc.Get("nav_south");
            string r = right   >= 0f ? Loc.Get("nav_east")  : Loc.Get("nav_west");

            if (significantF && significantR) return f + " " + r;
            if (significantF) return f;
            return r;
        }

        #endregion
    }
}
