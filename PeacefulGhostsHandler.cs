using System.Reflection;
using HarmonyLib;

namespace OuterWildsAccess
{
    /// <summary>
    /// Disables hostile ghost AI from the Echoes of the Eye DLC.
    ///
    /// Patches:
    ///   - GrabAction/ChaseAction/HuntAction/StalkAction.CalculateUtility → -1337f (never selected)
    ///   - GhostSensors.FixedUpdate_Sensors → reset all sensor booleans (ghosts never detect player)
    ///   - GhostPartyDirector.OnEnterDoorTrigger/OnEnterAmbushTrigger → skip (no ambush/trap)
    ///
    /// Guard: ModSettings.PeacefulGhostsEnabled
    /// </summary>
    public static class PeacefulGhostsHandler
    {
        private static FieldInfo _dataField; // GhostSensors._data (GhostData)

        /// <summary>Applies all Harmony patches. Call from Main.Start().</summary>
        public static void Initialize()
        {
            try
            {
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;

                // Cache reflection field for sensor reset
                _dataField = typeof(GhostSensors).GetField("_data", flags);
                if (_dataField == null)
                {
                    DebugLogger.Log(LogCategory.State, "PeacefulGhosts",
                        "WARNING: GhostSensors._data field not found (DLC not installed?)");
                }

                var harmony = new Harmony("com.outerwildsaccess.peacefulghosts");

                // ── Hostile action patches ──
                var prefixHostile = new HarmonyMethod(
                    typeof(PeacefulGhostsHandler), nameof(Prefix_HostileUtility));

                PatchUtility(harmony, typeof(GrabAction),  prefixHostile);
                PatchUtility(harmony, typeof(ChaseAction), prefixHostile);
                PatchUtility(harmony, typeof(HuntAction),  prefixHostile);
                PatchUtility(harmony, typeof(StalkAction), prefixHostile);

                // ── Sensor reset patch ──
                var sensorMethod = typeof(GhostSensors).GetMethod(
                    "FixedUpdate_Sensors", BindingFlags.Public | BindingFlags.Instance);
                if (sensorMethod != null)
                    harmony.Patch(sensorMethod,
                        postfix: new HarmonyMethod(
                            typeof(PeacefulGhostsHandler), nameof(Postfix_ResetSensors)));

                // ── Party trigger patches ──
                var prefixBlock = new HarmonyMethod(
                    typeof(PeacefulGhostsHandler), nameof(Prefix_BlockTrigger));

                var doorMethod = typeof(GhostPartyDirector).GetMethod(
                    "OnEnterDoorTrigger", flags);
                var ambushMethod = typeof(GhostPartyDirector).GetMethod(
                    "OnEnterAmbushTrigger", flags);

                if (doorMethod != null)
                    harmony.Patch(doorMethod, prefix: prefixBlock);
                if (ambushMethod != null)
                    harmony.Patch(ambushMethod, prefix: prefixBlock);

                DebugLogger.Log(LogCategory.State, "PeacefulGhosts", "Initialized");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log(LogCategory.State, "PeacefulGhosts",
                    "ERROR Initialize: " + ex.Message);
            }
        }

        #region Harmony patches

        /// <summary>
        /// Prefix for hostile action CalculateUtility: returns -1337 so the action
        /// is never selected by GhostBrain.EvaluateActions().
        /// </summary>
        private static bool Prefix_HostileUtility(ref float __result)
        {
            if (!ModSettings.PeacefulGhostsEnabled) return true;
            __result = -1337f;
            return false;
        }

        /// <summary>
        /// Postfix for GhostSensors.FixedUpdate_Sensors: resets all sensor booleans
        /// so ghosts never detect the player and threat awareness never escalates.
        /// </summary>
        private static void Postfix_ResetSensors(GhostSensors __instance)
        {
            if (!ModSettings.PeacefulGhostsEnabled) return;
            if (_dataField == null) return;

            var data = _dataField.GetValue(__instance) as GhostData;
            if (data == null) return;

            data.sensor.isPlayerVisible              = false;
            data.sensor.isPlayerHeldLanternVisible    = false;
            data.sensor.isPlayerDroppedLanternVisible = false;
            data.sensor.isIlluminated                = false;
            data.sensor.isIlluminatedByPlayer         = false;
            data.sensor.isPlayerIlluminatedByUs       = false;
            data.sensor.isPlayerIlluminated           = false;
            data.sensor.isPlayerOccluded              = true;
            data.sensor.inContactWithPlayer           = false;
            data.sensor.isPlayerInGuardVolume         = false;
        }

        /// <summary>
        /// Prefix for GhostPartyDirector triggers: blocks door trap and ambush.
        /// </summary>
        private static bool Prefix_BlockTrigger()
        {
            return !ModSettings.PeacefulGhostsEnabled;
        }

        #endregion

        #region Helpers

        private static void PatchUtility(Harmony harmony, System.Type actionType,
            HarmonyMethod prefix)
        {
            var method = actionType.GetMethod("CalculateUtility",
                BindingFlags.Public | BindingFlags.Instance);
            if (method != null)
                harmony.Patch(method, prefix: prefix);
            else
                DebugLogger.Log(LogCategory.State, "PeacefulGhosts",
                    "WARNING: " + actionType.Name + ".CalculateUtility not found");
        }

        #endregion
    }
}
