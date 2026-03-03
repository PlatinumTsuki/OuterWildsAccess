using UnityEngine;
using UnityEngine.InputSystem;

namespace OuterWildsAccess
{
    /// <summary>
    /// Plays a short audio click when the player moves toward an obstacle that is
    /// blocking their path, giving tactile audio feedback for wall contact.
    ///
    /// Fires a raycast every frame in the player's movement direction.
    /// Beep is a 700 Hz click (50 ms), 2D (non-spatial), with a 0.3 s cooldown.
    /// No-op when CollisionEnabled is false or player is not in Character mode.
    /// </summary>
    public class CollisionBeepHandler
    {
        #region Constants

        private const float RayDistance      = 0.65f;  // metres ahead to check
        private const float InputThreshold   = 0.25f;  // minimum stick/WASD magnitude
        private const float CooldownDuration = 0.3f;   // seconds between beeps
        private const float BeepFrequency    = 700f;
        private const float BeepDuration     = 0.05f;  // seconds
        private const int   SampleRate       = 44100;

        #endregion

        #region State

        private GameObject  _go;
        private AudioSource _audio;
        private AudioClip   _beepClip;
        private float       _cooldown;

        #endregion

        #region Lifecycle

        /// <summary>Creates the 2D AudioSource for collision beeps. Call from Main.Start().</summary>
        public void Initialize()
        {
            _go = new GameObject("CollisionBeep");
            Object.DontDestroyOnLoad(_go);

            _audio = _go.AddComponent<AudioSource>();
            _audio.spatialBlend  = 0f;   // 2D — not directional, just a notification
            _audio.volume        = 0.7f;
            _audio.playOnAwake   = false;

            _beepClip = CreateClickClip();
            DebugLogger.LogState("[CollisionBeepHandler] Initialized.");
        }

        /// <summary>Destroys the audio GameObject. Call from Main.OnDestroy().</summary>
        public void Cleanup()
        {
            if (_go != null)
                Object.Destroy(_go);
        }

        #endregion

        #region Update

        /// <summary>Call every frame from Main.Update().</summary>
        public void Update()
        {
            if (!ModSettings.CollisionEnabled) return;
            if (_cooldown > 0f) { _cooldown -= Time.deltaTime; return; }

            // Only check in normal gameplay (not in menus, ship cockpit, etc.)
            if (!OWInput.IsInputMode(InputMode.Character)) return;

            // Skip during the Eye of the Universe end sequence (player is invulnerable)
            if (PlayerState.IsInsideTheEye()) return;

            Transform playerTr = Locator.GetPlayerTransform();
            if (playerTr == null) return;

            // Get movement input (keyboard WASD or gamepad left stick)
            Vector2 moveInput = OWInput.GetAxisValue(InputLibrary.moveXZ, InputMode.Character);
            if (moveInput.magnitude < InputThreshold) return;

            // Convert 2D input to 3D world direction (relative to player facing)
            Vector3 moveDir = (playerTr.right   * moveInput.x
                             + playerTr.forward * moveInput.y).normalized;

            // Cast from chest height in the move direction
            Vector3 origin = playerTr.position + playerTr.up * 0.9f;

            if (Physics.Raycast(origin, moveDir, RayDistance,
                    OWLayerMask.physicalMask, QueryTriggerInteraction.Ignore))
            {
                _audio.PlayOneShot(_beepClip);
                _cooldown = CooldownDuration;
                DebugLogger.Log(LogCategory.State, "CollisionBeep", "Hit");
            }
        }

        #endregion

        #region Audio generation

        /// <summary>
        /// Generates a short 700 Hz click with linear decay envelope.
        /// Formula: sin(2π · freq · t) · (1 - t / dur)
        /// </summary>
        private static AudioClip CreateClickClip()
        {
            int     count   = Mathf.RoundToInt(SampleRate * BeepDuration);
            float[] samples = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t        = (float)i / SampleRate;
                float envelope = 1f - (t / BeepDuration);   // linear decay
                samples[i]     = Mathf.Sin(2f * Mathf.PI * BeepFrequency * t) * envelope;
            }

            AudioClip clip = AudioClip.Create("CollisionClick", count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        #endregion
    }
}
