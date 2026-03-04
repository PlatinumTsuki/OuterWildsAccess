using System.Collections.Generic;
using UnityEngine;

namespace OuterWildsAccess
{
    /// <summary>
    /// Guides the player along an A* path using 3D-positioned audio ticks.
    /// The tick sound comes from the direction of the next waypoint (spatial panning),
    /// and the tick rate changes based on alignment (5 tiers):
    ///   - Facing away  (dot &lt; 0):    slow (1.5s), long 1000Hz click
    ///   - Slightly off (dot 0–0.3):  slow-med (0.9s), long click
    ///   - Partial      (dot 0.3–0.7): medium (0.5s), short click
    ///   - Good         (dot 0.7–0.93): fast (0.2s), short click
    ///   - On target    (dot &gt; 0.93): very fast (0.12s), short 1200Hz click
    ///
    /// Toggle with G key. Requires an active navigation target.
    /// </summary>
    public class PathGuidanceHandler
    {
        #region Constants

        private const int   SampleRate         = 44100;
        private const float ClickShortDuration = 0.04f;   // 40 ms — aligned tick
        private const float ClickLongDuration  = 0.08f;   // 80 ms — off-direction tick
        private const float ClickFrequency     = 1000f;   // Hz — standard guidance tick
        private const float OnTargetFrequency  = 1200f;   // Hz — "pile en face" distinct tick
        private const float ArrivalClickFreq   = 1200f;   // Hz — double-click on arrival

        // 5-tier tick intervals based on alignment (dot product)
        private const float Tier1Interval      = 0.12f;   // dot > 0.93: on target — very fast
        private const float Tier2Interval      = 0.2f;    // dot > 0.7:  good
        private const float Tier3Interval      = 0.5f;    // dot > 0.3:  partial
        private const float Tier4Interval      = 0.9f;    // dot > 0:    slightly off
        private const float Tier5Interval      = 1.5f;    // dot < 0:    facing away
        private const float OnTargetThreshold  = 0.93f;
        private const float GoodThreshold      = 0.7f;
        private const float PartialThreshold   = 0.3f;

        // 3D audio positioning
        private const float AudioDistance      = 10f;     // metres from player toward waypoint

        // Path following — ArrivalDist, ArrivalVertMax, WaypointReachDist from PathConstants
        private const float RescanInterval     = 0.5f;    // seconds between A* rescans
        private const float SegmentDistance    = 50f;     // max A* segment for long distances

        #endregion

        #region State

        private GameObject  _go;
        private AudioSource _audio;
        private AudioClip   _clickShort;
        private AudioClip   _clickLong;
        private AudioClip   _clickOnTarget;
        private AudioClip   _arrivalClick;

        private Transform   _target;
        private string      _targetName;
        private bool        _isActive;

        // Path following (shared PathScanner instance, set via Initialize)
        private PathScanner _pathScanner;
        private List<PathWaypoint>   _path;
        private int                  _waypointIndex;
        private float                _rescanTimer;
        private bool                 _segmented;

        // Tick timing
        private float _tickTimer;

        /// <summary>True while path guidance is actively ticking.</summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// Returns the position of the current A* waypoint, or null if guidance
        /// is not active or has no path. Used by NavigationHandler for direction.
        /// </summary>
        public Vector3? CurrentWaypointPosition
        {
            get
            {
                if (!_isActive || _path == null || _waypointIndex >= _path.Count)
                    return null;
                return _path[_waypointIndex].Position;
            }
        }

        #endregion

        #region Lifecycle

        /// <summary>
        /// Creates the 3D AudioSource and procedural click clips.
        /// Call from Main.Start().
        /// </summary>
        public void Initialize(PathScanner sharedScanner)
        {
            _pathScanner = sharedScanner;
            _go = new GameObject("PathGuidance");
            Object.DontDestroyOnLoad(_go);

            _audio = _go.AddComponent<AudioSource>();
            _audio.spatialBlend  = 1.0f;                        // fully 3D
            _audio.rolloffMode   = AudioRolloffMode.Linear;
            _audio.minDistance   = 1f;
            _audio.maxDistance   = 50f;
            _audio.volume        = 1f;
            _audio.playOnAwake   = false;
            _audio.loop          = false;

            _clickShort    = CreateClickClip(ClickFrequency, ClickShortDuration);
            _clickLong     = CreateClickClip(ClickFrequency, ClickLongDuration);
            _clickOnTarget = CreateClickClip(OnTargetFrequency, ClickShortDuration);
            _arrivalClick  = CreateDoubleClickClip(ArrivalClickFreq, 0.04f, 0.06f);

            DebugLogger.LogState("[PathGuidanceHandler] Initialized.");
        }

        /// <summary>Destroys the audio GameObject. Call from Main.OnDestroy().</summary>
        public void Cleanup()
        {
            if (_go != null) Object.Destroy(_go);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Sets the navigation target. Called from Main when End key anchors a target.
        /// Does not start guidance automatically.
        /// </summary>
        public void SetTarget(Transform target, string name)
        {
            _target        = target;
            _targetName    = name ?? string.Empty;
            _path          = null;
            _waypointIndex = 0;
            DebugLogger.LogState("[PathGuidanceHandler] Target set: " + _targetName);
        }

        /// <summary>Silently clears target and stops guidance.</summary>
        public void ClearTarget()
        {
            Stop(announce: false);
            _target     = null;
            _targetName = string.Empty;
            _path       = null;
            _segmented  = false;
            DebugLogger.LogState("[PathGuidanceHandler] Target cleared.");
        }

        /// <summary>
        /// Toggles guidance on/off. Returns true if guidance is now active
        /// (so Main.cs can coordinate beacon muting).
        /// </summary>
        public bool Toggle()
        {
            if (!ModSettings.GuidanceEnabled) return false;

            if (_isActive)
            {
                Stop(announce: true);
                return false;
            }

            if (_target == null)
            {
                ScreenReader.Say(Loc.Get("nav_no_target"));
                return false;
            }

            _isActive      = true;
            _path          = null;
            _waypointIndex = 0;
            _rescanTimer   = 0f;    // compute path immediately
            _tickTimer     = 0f;    // tick immediately

            ScreenReader.Say(Loc.Get("guidance_on", _targetName));
            DebugLogger.LogInput("G", "Guidance start -> " + _targetName);
            return true;
        }

        /// <summary>
        /// Stops guidance. Called externally by Main when beacon is activated.
        /// </summary>
        public void Stop(bool announce)
        {
            if (!_isActive) return;
            _isActive  = false;
            _path      = null;
            _segmented = false;
            if (announce)
                ScreenReader.Say(Loc.Get("guidance_off"));
            DebugLogger.LogState("[PathGuidanceHandler] Stopped.");
        }

        #endregion

        #region Update

        /// <summary>
        /// Computes alignment with next waypoint and plays tick sounds.
        /// Call every frame from Main.Update().
        /// </summary>
        public void Update()
        {
            if (!_isActive) return;
            if (!ModSettings.GuidanceEnabled) { Stop(announce: false); return; }

            Transform playerTr = Locator.GetPlayerTransform();
            if (playerTr == null) return;

            // Target destroyed?
            if (_target == null)
            {
                ScreenReader.Say(Loc.Get("nav_target_lost"));
                Stop(announce: false);
                return;
            }

            // Only guide in character mode
            if (!OWInput.IsInputMode(InputMode.Character)) return;

            Vector3 up         = playerTr.up;
            Vector3 toTarget3D = _target.position - playerTr.position;
            Vector3 vertVec    = Vector3.Project(toTarget3D, up);
            Vector3 horizVec   = toTarget3D - vertVec;
            float   horizDist  = horizVec.magnitude;
            float   vertDist   = vertVec.magnitude;

            // ── Arrival check (horizontal + vertical) ────────────────
            if (horizDist <= PathConstants.ArrivalDist && vertDist <= PathConstants.ArrivalVertMax)
            {
                _audio.PlayOneShot(_arrivalClick);
                ScreenReader.Say(Loc.Get("guidance_arrived", _targetName));
                Stop(announce: false);
                return;
            }

            // Horizontally close but too high/low — announce out of reach
            if (horizDist <= PathConstants.ArrivalDist && vertDist > PathConstants.ArrivalVertMax)
            {
                ScreenReader.Say(Loc.Get("auto_walk_out_of_reach", _targetName));
                Stop(announce: false);
                return;
            }

            // ── Periodic path rescan ─────────────────────────────────
            if (Time.time >= _rescanTimer)
            {
                _path = null;
                _rescanTimer = Time.time + RescanInterval;
            }

            // ── Compute path if needed (with segmentation for long distances)
            if (_path == null)
            {
                if (horizDist > SegmentDistance)
                {
                    Vector3 segGoal = playerTr.position + horizVec.normalized * SegmentDistance;
                    _path = _pathScanner.FindPath(playerTr.position, up, segGoal);
                    _segmented = true;
                }
                else
                {
                    _path = _pathScanner.FindPath(playerTr.position, up, _target.position);
                    _segmented = false;
                }
                _waypointIndex = 0;

                if (_path == null || _path.Count == 0)
                {
                    // Fallback: walk straight toward target
                    _path = new List<PathWaypoint>
                    {
                        new PathWaypoint { Position = _target.position, NeedsJump = false }
                    };
                    _segmented = false;
                }

                DebugLogger.Log(LogCategory.State, "Guidance",
                    "Path: " + _path.Count + " waypoints, dist=" + horizDist.ToString("F1") + "m"
                    + (_segmented ? " (segment)" : ""));
            }

            // ── Advance past reached waypoints ───────────────────────
            while (_waypointIndex < _path.Count)
            {
                Vector3 toWp     = _path[_waypointIndex].Position - playerTr.position;
                Vector3 toWpFlat = toWp - Vector3.Project(toWp, up);
                if (toWpFlat.magnitude > PathConstants.WaypointReachDist) break;
                _waypointIndex++;
            }

            // All waypoints consumed → recompute immediately
            if (_waypointIndex >= _path.Count)
            {
                _path        = null;
                _rescanTimer = Time.time + RescanInterval;
                if (_segmented)
                {
                    // Segment consumed — recompute next segment this frame
                    if (horizDist > SegmentDistance)
                    {
                        Vector3 segGoal = playerTr.position + horizVec.normalized * SegmentDistance;
                        _path = _pathScanner.FindPath(playerTr.position, up, segGoal);
                        _segmented = true;
                    }
                    else
                    {
                        _path = _pathScanner.FindPath(playerTr.position, up, _target.position);
                        _segmented = false;
                    }
                    _waypointIndex = 0;

                    if (_path == null || _path.Count == 0)
                    {
                        _path = new List<PathWaypoint>
                        {
                            new PathWaypoint { Position = _target.position, NeedsJump = false }
                        };
                        _segmented = false;
                    }

                    // Re-advance past reached waypoints
                    while (_waypointIndex < _path.Count)
                    {
                        Vector3 toWp2     = _path[_waypointIndex].Position - playerTr.position;
                        Vector3 toWpFlat2 = toWp2 - Vector3.Project(toWp2, up);
                        if (toWpFlat2.magnitude > PathConstants.WaypointReachDist) break;
                        _waypointIndex++;
                    }
                    if (_waypointIndex >= _path.Count) return;
                }
                else
                {
                    return;
                }
            }

            // ── Compute alignment and position 3D audio ─────────────
            Vector3 toWaypoint     = _path[_waypointIndex].Position - playerTr.position;
            Vector3 toWaypointFlat = toWaypoint - Vector3.Project(toWaypoint, up);

            if (toWaypoint.sqrMagnitude < 0.01f) return;

            // Position audio source in full 3D toward the next waypoint
            // (includes vertical — player hears above/below as well as left/right)
            _go.transform.position = playerTr.position + toWaypoint.normalized * AudioDistance;

            // Alignment dot uses horizontal only (player can't look up/down to walk)
            if (toWaypointFlat.sqrMagnitude < 0.01f) return;
            Vector3 playerFwd = playerTr.forward - Vector3.Project(playerTr.forward, up);
            if (playerFwd.sqrMagnitude < 0.001f) return;

            float dot = Vector3.Dot(playerFwd.normalized, toWaypointFlat.normalized);

            // ── Tick based on alignment (5 tiers) ────────────────────
            _tickTimer -= Time.deltaTime;
            if (_tickTimer <= 0f)
            {
                if (dot > OnTargetThreshold)
                {
                    // Tier 1: on target — very fast, distinct higher pitch
                    _audio.PlayOneShot(_clickOnTarget);
                    _tickTimer = Tier1Interval;
                }
                else if (dot > GoodThreshold)
                {
                    // Tier 2: good alignment — fast
                    _audio.PlayOneShot(_clickShort);
                    _tickTimer = Tier2Interval;
                }
                else if (dot > PartialThreshold)
                {
                    // Tier 3: partially aligned — medium
                    _audio.PlayOneShot(_clickShort);
                    _tickTimer = Tier3Interval;
                }
                else if (dot >= 0f)
                {
                    // Tier 4: slightly off — slow-medium, long click
                    _audio.PlayOneShot(_clickLong);
                    _tickTimer = Tier4Interval;
                }
                else
                {
                    // Tier 5: facing away — slow, long click
                    _audio.PlayOneShot(_clickLong);
                    _tickTimer = Tier5Interval;
                }
            }
        }

        #endregion

        #region Audio Generation

        /// <summary>
        /// Generates a procedural click with sharp linear decay.
        /// </summary>
        private static AudioClip CreateClickClip(float freq, float duration)
        {
            int     count   = Mathf.RoundToInt(SampleRate * duration);
            float[] samples = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t        = (float)i / SampleRate;
                float envelope = Mathf.Max(0f, 1f - (t / duration));   // sharp linear decay, clamped
                samples[i]     = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * 0.8f;
            }

            AudioClip clip = AudioClip.Create(
                "GuidanceClick_" + (int)freq + "Hz_" + Mathf.RoundToInt(duration * 1000) + "ms",
                count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Generates a double-click clip with a silence gap between the two clicks.
        /// </summary>
        private static AudioClip CreateDoubleClickClip(float freq, float singleDuration, float gap)
        {
            int singleSamples = Mathf.RoundToInt(SampleRate * singleDuration);
            int gapSamples    = Mathf.RoundToInt(SampleRate * gap);
            int total         = singleSamples * 2 + gapSamples;
            float[] samples   = new float[total];

            // First click
            for (int i = 0; i < singleSamples; i++)
            {
                float t   = (float)i / SampleRate;
                float env = Mathf.Max(0f, 1f - (t / singleDuration));
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.8f;
            }
            // Gap is silence (already 0)
            // Second click
            int offset = singleSamples + gapSamples;
            for (int i = 0; i < singleSamples; i++)
            {
                float t   = (float)i / SampleRate;
                float env = Mathf.Max(0f, 1f - (t / singleDuration));
                samples[offset + i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.8f;
            }

            AudioClip clip = AudioClip.Create("GuidanceArrival", total, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        #endregion
    }
}
