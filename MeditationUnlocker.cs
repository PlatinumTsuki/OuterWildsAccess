using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace OuterWildsAccess
{
    /// <summary>
    /// Unlocks the "Skip to Next Loop" (meditation) button in the pause menu
    /// from the start, without requiring the player to find Gabbro first.
    ///
    /// The vanilla game checks PlayerData.GetPersistentCondition("KNOWS_MEDITATION")
    /// before showing the button. This patch overrides that check.
    ///
    /// Guard: ModSettings.MeditationEnabled
    /// </summary>
    public static class MeditationUnlocker
    {
        private static FieldInfo _skipButtonField;

        /// <summary>
        /// Applies the Harmony postfix on PauseMenuManager.Start().
        /// Call from Main.Start().
        /// </summary>
        public static void Initialize()
        {
            try
            {
                var harmony = new Harmony("com.outerwildsaccess.meditation");

                var targetMethod = typeof(PauseMenuManager).GetMethod(
                    "Start", BindingFlags.NonPublic | BindingFlags.Instance);

                if (targetMethod == null)
                {
                    DebugLogger.Log(LogCategory.State, "MeditationUnlocker",
                        "ERROR: PauseMenuManager.Start() not found");
                    return;
                }

                _skipButtonField = typeof(PauseMenuManager).GetField(
                    "_skipToNextLoopButton",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (_skipButtonField == null)
                {
                    DebugLogger.Log(LogCategory.State, "MeditationUnlocker",
                        "ERROR: _skipToNextLoopButton field not found");
                    return;
                }

                harmony.Patch(targetMethod,
                    postfix: new HarmonyMethod(typeof(MeditationUnlocker), nameof(Postfix_Start)));

                DebugLogger.Log(LogCategory.State, "MeditationUnlocker", "Initialized");
            }
            catch (Exception ex)
            {
                DebugLogger.Log(LogCategory.State, "MeditationUnlocker",
                    "ERROR Initialize: " + ex.Message);
            }
        }

        /// <summary>
        /// Postfix on PauseMenuManager.Start().
        /// Forces the meditation button to be visible.
        /// </summary>
        private static void Postfix_Start(object __instance)
        {
            if (!ModSettings.MeditationEnabled) return;

            try
            {
                var button = _skipButtonField?.GetValue(__instance) as GameObject;
                if (button != null)
                {
                    button.SetActive(true);
                    DebugLogger.Log(LogCategory.State, "MeditationUnlocker",
                        "Meditation button activated");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log(LogCategory.State, "MeditationUnlocker",
                    "ERROR Postfix_Start: " + ex.Message);
            }
        }
    }
}
