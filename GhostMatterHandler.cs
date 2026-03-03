using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace OuterWildsAccess
{
    /// <summary>
    /// Proactively warns the player when they enter a ghost matter proximity zone,
    /// BEFORE contact with the lethal substance.
    ///
    /// Strategy: the game already places a NotificationVolume (type DarkMatter) around
    /// each ghost matter patch — larger than the lethal zone. We postfix
    /// NotificationDetector.OnVolumeAdded / OnVolumeRemoved to announce when the
    /// player enters or exits this warning volume.
    ///
    /// A counter tracks overlapping volumes so we only announce on
    /// the first entry (0→1) and the final exit (1→0).
    /// The counter is reset on PlayerResurrection in case instant death
    /// prevented OnVolumeRemoved from firing.
    ///
    /// If auto-walk is active on entry, it is stopped immediately.
    ///
    /// Nerf mode (GhostMatterNerfEnabled): sets _darkMatterDamagePerSecond to 0
    /// every frame, making the player immune to ghost matter damage.
    ///
    /// Guard: ModSettings.GhostMatterProtectionEnabled (detection + immunity)
    /// </summary>
    public class GhostMatterHandler
    {
        #region Static state (shared with Harmony patches)

        // Counts overlapping DarkMatter notification volumes the player is inside.
        private static int    _zoneCount    = 0;

        // Callback to stop auto-walk (without announcing separately).
        private static Action _stopAutoWalk = null;

        // Cached reflection for ghost matter nerf
        private static FieldInfo _darkMatterDmgField;
        private static bool      _nerfFieldResolved;

        #endregion

        #region Lifecycle

        /// <summary>
        /// Applies Harmony patches and subscribes to game events.
        /// Call from Main.Start().
        /// </summary>
        /// <param name="stopAutoWalk">
        /// Optional callback invoked when ghost matter is detected nearby and auto-walk
        /// is active. Should stop auto-walk without announcing (the handler announces).
        /// </param>
        public void Initialize(Action stopAutoWalk = null)
        {
            _stopAutoWalk = stopAutoWalk;

            GlobalMessenger.AddListener("PlayerResurrection", OnPlayerResurrection);

            try
            {
                var harmony = new Harmony("com.outerwildsaccess.ghostmatter");

                var addMethod = typeof(NotificationDetector).GetMethod(
                    "OnVolumeAdded",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                var removeMethod = typeof(NotificationDetector).GetMethod(
                    "OnVolumeRemoved",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (addMethod == null || removeMethod == null)
                {
                    DebugLogger.Log(LogCategory.State, "GhostMatterHandler",
                        "ERROR: OnVolumeAdded/OnVolumeRemoved not found on NotificationDetector");
                    return;
                }

                harmony.Patch(addMethod,
                    postfix: new HarmonyMethod(
                        typeof(GhostMatterHandler), nameof(Postfix_OnVolumeAdded)));

                harmony.Patch(removeMethod,
                    postfix: new HarmonyMethod(
                        typeof(GhostMatterHandler), nameof(Postfix_OnVolumeRemoved)));

                DebugLogger.Log(LogCategory.State, "GhostMatterHandler", "Initialisé");
            }
            catch (Exception ex)
            {
                DebugLogger.Log(LogCategory.State, "GhostMatterHandler",
                    "ERROR Initialize: " + ex.Message);
            }
        }

        /// <summary>
        /// Unsubscribes from events and resets state. Call from Main.OnDestroy().
        /// </summary>
        public void Cleanup()
        {
            GlobalMessenger.RemoveListener("PlayerResurrection", OnPlayerResurrection);
            _zoneCount         = 0;
            _stopAutoWalk      = null;
            _nerfFieldResolved = false;
            _darkMatterDmgField = null;
        }

        /// <summary>
        /// Call from Main.Update(). When GhostMatterNerfEnabled, sets dark matter
        /// damage to 0 every frame so the player is immune.
        /// </summary>
        public void Update()
        {
            if (!ModSettings.GhostMatterProtectionEnabled) return;

            try
            {
                // Resolve field once
                if (!_nerfFieldResolved)
                {
                    _darkMatterDmgField = typeof(HazardDetector).GetField(
                        "_darkMatterDamagePerSecond",
                        BindingFlags.NonPublic | BindingFlags.Instance);

                    _nerfFieldResolved = true;

                    if (_darkMatterDmgField == null)
                    {
                        DebugLogger.Log(LogCategory.State, "GhostMatterHandler",
                            "ERROR: _darkMatterDamagePerSecond field not found");
                    }
                }

                if (_darkMatterDmgField == null) return;

                var detector = Locator.GetPlayerDetector()?.GetComponent<HazardDetector>();
                if (detector != null)
                {
                    _darkMatterDmgField.SetValue(detector, 0f);
                }
            }
            catch
            {
                // Silently ignore — player may not be spawned yet
            }
        }

        #endregion

        #region Event handlers

        // Reset counter on respawn — instant death may skip OnVolumeRemoved.
        private void OnPlayerResurrection()
        {
            _zoneCount = 0;
            DebugLogger.Log(LogCategory.State, "GhostMatterHandler", "Compteur réinitialisé (respawn)");
        }

        #endregion

        #region Harmony patches

        /// <summary>
        /// Postfix on NotificationDetector.OnVolumeAdded.
        /// Announces a warning when the player enters the first DarkMatter notification zone.
        /// </summary>
        private static void Postfix_OnVolumeAdded(EffectVolume effectVol)
        {
            if (!ModSettings.GhostMatterProtectionEnabled) return;

            var nv = effectVol as NotificationVolume;
            if (nv == null || nv.GetNotificationType() != NotificationVolume.Type.DarkMatter) return;

            _zoneCount++;
            DebugLogger.Log(LogCategory.State, "GhostMatterHandler",
                $"Zone DM entrée (count={_zoneCount})");

            if (_zoneCount != 1) return;    // announce only on first entry

            // Stop auto-walk first so its own announcement fires before ours,
            // then our warning overrides it in the screen reader (interrupts).
            _stopAutoWalk?.Invoke();

            ScreenReader.Say(Loc.Get("ghost_matter_near"), SpeechPriority.Now);
        }

        /// <summary>
        /// Postfix on NotificationDetector.OnVolumeRemoved.
        /// Announces "zone clear" when the player exits the last DarkMatter notification zone.
        /// </summary>
        private static void Postfix_OnVolumeRemoved(EffectVolume effectVol)
        {
            if (!ModSettings.GhostMatterProtectionEnabled) return;

            var nv = effectVol as NotificationVolume;
            if (nv == null || nv.GetNotificationType() != NotificationVolume.Type.DarkMatter) return;

            _zoneCount--;
            if (_zoneCount < 0) _zoneCount = 0;

            DebugLogger.Log(LogCategory.State, "GhostMatterHandler",
                $"Zone DM quittée (count={_zoneCount})");

            if (_zoneCount == 0)
            {
                ScreenReader.Say(Loc.Get("ghost_matter_clear"));
            }
        }

        #endregion
    }
}
