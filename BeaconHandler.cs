using UnityEngine;

namespace OuterWildsAccess
{
    /// <summary>
    /// Plays a repeating 3D audio beacon positioned in the direction of the active
    /// navigation target, giving the player a spatial (left/right panning) audio cue.
    ///
    /// The beacon GameObject survives scene loads (DontDestroyOnLoad).
    /// Audio is procedurally generated — no external audio files required.
    ///
    /// SetTarget(Transform) — call from Main after NavigateToTarget().
    /// ToggleEnabled()      — toggles ModSettings.BeaconEnabled.
    /// Update()             — call every frame from Main.Update().
    /// </summary>
    public class BeaconHandler
    {
        #region Constants

        private const int   SampleRate     = 44100;
        private const float BeepDuration   = 0.15f;   // seconds
        private const float BeepFrequency  = 440f;    // Hz (concert A)
        private const float MinPulseInterval = 0.25f;  // seconds — very close (< 5 m)
        private const float MaxPulseInterval = 1.5f;  // seconds — far (> 20 m)
        private const float BeaconDistance = 10f;     // metres from player (constant for stable volume)

        #endregion

        #region State

        private GameObject  _go;
        private AudioSource _audio;
        private AudioClip   _beepClip;
        private Transform   _target;
        private float       _pulseTimer;
        private bool        _hadTarget;    // tracks target loss for beacon_lost announcement
        private bool        _muted;        // per-target silence — resets on new target

        #endregion

        #region Lifecycle

        /// <summary>
        /// Creates the persistent beacon GameObject with a 3D AudioSource.
        /// Call from Main.Start().
        /// </summary>
        public void Initialize()
        {
            _go = new GameObject("AccessibilityBeacon");
            Object.DontDestroyOnLoad(_go);

            _audio = _go.AddComponent<AudioSource>();
            _audio.spatialBlend  = 1.0f;                        // fully 3D
            _audio.rolloffMode   = AudioRolloffMode.Linear;
            _audio.minDistance   = 1f;
            _audio.maxDistance   = 50f;
            _audio.volume        = 1f;
            _audio.playOnAwake   = false;
            _audio.loop          = false;

            _beepClip    = CreateBeepClip();
            _pulseTimer  = 0f;
            _hadTarget   = false;

            DebugLogger.LogState("[BeaconHandler] Initialized.");
        }

        /// <summary>Destroys the beacon GameObject. Call from Main.OnDestroy().</summary>
        public void Cleanup()
        {
            if (_go != null)
                Object.Destroy(_go);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Sets the navigation target for the beacon.
        /// Called from Main after NavigationHandler.NavigateToTarget().
        /// </summary>
        public void SetTarget(Transform target)
        {
            _target    = target;
            _hadTarget = target != null;
            _muted     = true;    // beacon starts silent — player activates with Backspace
            DebugLogger.LogState("[BeaconHandler] Target set: " + (target != null ? target.name : "null"));
        }

        /// <summary>True if the beacon is currently muted for the current target.</summary>
        public bool IsMuted => _muted;

        /// <summary>
        /// Externally sets the mute state without user announcement.
        /// Used by Main.cs for mutual exclusion with path guidance.
        /// </summary>
        public void SetMuted(bool muted)
        {
            _muted = muted;
            DebugLogger.LogState("[BeaconHandler] Muted externally: " + _muted);
        }

        /// <summary>
        /// Silences or resumes the beacon for the current target without changing
        /// the global BeaconEnabled setting. Resets automatically on next SetTarget().
        /// </summary>
        public void ToggleMute()
        {
            if (_target == null)
            {
                ScreenReader.Say(Loc.Get("nav_no_target"));
                return;
            }
            _muted = !_muted;
            ScreenReader.Say(_muted ? Loc.Get("beacon_muted") : Loc.Get("beacon_unmuted"));
            DebugLogger.LogInput("Backspace", "BeaconMute → " + _muted);
        }

        /// <summary>
        /// Toggles beacon on/off and announces the new state.
        /// </summary>
        public void ToggleEnabled()
        {
            ModSettings.BeaconEnabled = !ModSettings.BeaconEnabled;
            ScreenReader.Say(ModSettings.BeaconEnabled
                ? Loc.Get("beacon_on")
                : Loc.Get("beacon_off"));
            DebugLogger.LogInput("Backspace", "BeaconToggle → " + ModSettings.BeaconEnabled);
        }

        /// <summary>
        /// Updates beacon position and fires pulse beeps. Call every frame from Main.Update().
        /// </summary>
        public void Update()
        {
            if (!ModSettings.BeaconEnabled) return;

            // Detect when a previously valid target has been destroyed
            if (_target == null)
            {
                if (_hadTarget)
                {
                    ScreenReader.Say(Loc.Get("beacon_lost"));
                    _hadTarget = false;
                }
                return;
            }

            Transform playerTr = Locator.GetPlayerTransform();
            if (playerTr == null) return;

            // Position beacon BeaconDistance metres from player toward target
            Vector3 toTarget = _target.position - playerTr.position;
            if (toTarget.sqrMagnitude < 0.001f) return;  // player is on top of target

            float dist = toTarget.magnitude;
            _go.transform.position = playerTr.position + toTarget.normalized * BeaconDistance;

            if (_muted) return;

            // Pulse timer — interval shrinks as distance decreases
            _pulseTimer -= Time.deltaTime;
            if (_pulseTimer <= 0f)
            {
                _audio.PlayOneShot(_beepClip);
                _pulseTimer = GetPulseInterval(dist);
            }
        }

        #endregion

        #region Pulse Interval

        /// <summary>
        /// Returns the beacon pulse interval in seconds based on distance to target.
        /// Closer = faster pulses, inspired by Diablo Access LowHpIntervalMs pattern.
        /// </summary>
        private static float GetPulseInterval(float dist)
        {
            if (dist > 20f) return MaxPulseInterval; // 1.5 s
            if (dist > 10f) return 1.0f;
            if (dist >  5f) return 0.5f;
            return MinPulseInterval;                 // 0.25 s
        }

        #endregion

        #region Audio generation

        /// <summary>
        /// Generates a 440 Hz sine wave with a bell-shaped amplitude envelope.
        /// Formula: sample[i] = sin(2π · freq · t) · sin(π · t / dur)
        /// </summary>
        private static AudioClip CreateBeepClip()
        {
            int     count   = Mathf.RoundToInt(SampleRate * BeepDuration);
            float[] samples = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t        = (float)i / SampleRate;
                float envelope = Mathf.Sin(Mathf.PI * t / BeepDuration);   // bell shape
                samples[i]     = Mathf.Sin(2f * Mathf.PI * BeepFrequency * t) * envelope;
            }

            AudioClip clip = AudioClip.Create("BeaconBeep", count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        #endregion
    }
}
