using System;
using HarmonyLib;
using UnityEngine;

namespace OuterWildsAccess
{
    /// <summary>
    /// Accessibility for quantum objects. Outer Wilds has several objects that
    /// change position or state when the player is not looking at them — this
    /// is impossible to perceive for a blind player.
    ///
    /// Scope of this handler:
    ///   - Announces nearby non-moon quantum collapses (shrine rocks, village
    ///     rock, etc.) via a Harmony postfix on QuantumObject.Collapse. Filtered
    ///     by distance (75m) and a 4s global cooldown to avoid spam.
    ///
    /// Explicitly NOT handled here (by design, to preserve puzzle fairness):
    ///   - The Quantum Moon's orbit relocations. Sighted players only learn
    ///     about the moon's location by looking at the sky or by using the
    ///     signalscope on the "Quantum Fluctuations" frequency. The blind
    ///     player gets the same information through SignalscopeHandler, which
    ///     already announces frequency name, signal name, strength tier,
    ///     distance, and direction for any frequency — including quantum.
    ///     Automatic announcements of every moon relocation would give the
    ///     blind player strictly more information than sighted players and
    ///     break the "play as sighted" principle.
    ///
    /// Always active — no ModSettings guard. Cost is negligible.
    /// </summary>
    public class QuantumHandler
    {
        #region Static state (shared with Harmony patches)

        // Global cooldown for collapse announcements.
        private static float _lastCollapseAnnounceTime = -10f;
        private const  float COLLAPSE_COOLDOWN        = 4f;
        private const  float COLLAPSE_MAX_DISTANCE    = 75f;

        #endregion

        #region Lifecycle

        /// <summary>
        /// Applies the Harmony patch. Call from Main.Start().
        /// </summary>
        public void Initialize()
        {
            try
            {
                var harmony = new Harmony("com.outerwildsaccess.quantum");

                // QuantumObject.Collapse is protected — use AccessTools to find it.
                var collapseMethod = AccessTools.Method(typeof(QuantumObject), "Collapse");
                if (collapseMethod == null)
                {
                    DebugLogger.Log(LogCategory.State, "QuantumHandler",
                        "ERROR: QuantumObject.Collapse method not found");
                    return;
                }

                harmony.Patch(collapseMethod,
                    postfix: new HarmonyMethod(
                        typeof(QuantumHandler), nameof(Postfix_Collapse)));

                DebugLogger.Log(LogCategory.State, "QuantumHandler", "Initialisé");
            }
            catch (Exception ex)
            {
                DebugLogger.Log(LogCategory.State, "QuantumHandler",
                    "ERROR Initialize: " + ex.Message);
            }
        }

        /// <summary>
        /// Resets state. Call from Main.OnDestroy().
        /// </summary>
        public void Cleanup()
        {
            _lastCollapseAnnounceTime = -10f;
        }

        #endregion

        #region Harmony patches

        /// <summary>
        /// Postfix on QuantumObject.Collapse. Fires for every attempted collapse,
        /// successful or not. We only announce on success (__result == true),
        /// when the object is within range, and respecting the global cooldown.
        /// Quantum Moon is excluded — see class-level comment for rationale.
        /// </summary>
        private static void Postfix_Collapse(QuantumObject __instance, bool __result)
        {
            if (!__result || __instance == null) return;

            // Quantum Moon is intentionally not announced — the player must use
            // the signalscope to locate it, just like sighted players do.
            if (__instance is QuantumMoon) return;

            float now = Time.time;
            if (now - _lastCollapseAnnounceTime < COLLAPSE_COOLDOWN) return;

            var camera = Locator.GetPlayerCamera();
            if (camera == null) return;

            float distance = Vector3.Distance(
                __instance.transform.position, camera.transform.position);
            if (distance > COLLAPSE_MAX_DISTANCE) return;

            _lastCollapseAnnounceTime = now;
            ScreenReader.Say(Loc.Get("quantum_object_moved"));
        }

        #endregion
    }
}
