using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine.UI;

namespace OuterWildsAccess
{
    /// <summary>
    /// Reads the ship log (journal de bord) aloud via the screen reader.
    ///
    /// Map mode patches (ShipLogMapMode):
    ///   - Announce the focused planet (FocusOnAstroObject / FocusOnAstroObjectImmediate)
    ///   - Announce entry name + state + facts when an entry is focused (SetEntryFocus)
    ///   - Announce "no discoveries" when a planet has no entries (OpenEntryMenu)
    ///   - Announce "back to map" when the entry menu is closed (CloseEntryMenu)
    ///
    /// Detective mode patch (ShipLogDetectiveMode):
    ///   - On EnterMode with a reveal queue: announces newly discovered entry names
    ///     + hint to press E (skip animation) then Q (switch to map mode).
    ///
    /// Also subscribes to "ShipLogUpdated" to announce journal updates during gameplay.
    ///

    /// </summary>
    public class ShipLogHandler
    {
        #region Static state (shared with Harmony patches)

        // Cached Reflection fields for reading private state from ShipLogMapMode
        private static FieldInfo _nameFieldInfo;   // _nameField (UnityEngine.UI.Text)
        private static FieldInfo _listItemsInfo;   // _listItems (ShipLogEntryListItem[])
        private static FieldInfo _maxIndexInfo;    // _maxIndex (int)

        #endregion

        #region Lifecycle

        /// <summary>
        /// Applies Harmony patches and subscribes to journal events.
        /// Call from Main.Start().
        /// </summary>
        public void Initialize()
        {
            try
            {
                // Cache Reflection fields once
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                _nameFieldInfo = typeof(ShipLogMapMode).GetField("_nameField", flags);
                _listItemsInfo = typeof(ShipLogMapMode).GetField("_listItems", flags);
                _maxIndexInfo  = typeof(ShipLogMapMode).GetField("_maxIndex",  flags);

                if (_nameFieldInfo == null || _listItemsInfo == null || _maxIndexInfo == null)
                {
                    DebugLogger.Log(LogCategory.State, "ShipLogHandler",
                        "ERROR: Reflection fields not found on ShipLogMapMode");
                    return;
                }

                var harmony    = new Harmony("com.outerwildsaccess.shiplog");
                var privateInst = BindingFlags.NonPublic | BindingFlags.Instance;

                // Planet navigation — both immediate (on enter) and animated (while navigating)
                var focusImmMethod = typeof(ShipLogMapMode).GetMethod(
                    "FocusOnAstroObjectImmediate", privateInst, null,
                    new System.Type[] { typeof(int), typeof(int) }, null);
                var focusMethod = typeof(ShipLogMapMode).GetMethod(
                    "FocusOnAstroObject", privateInst, null,
                    new System.Type[] { typeof(int), typeof(int) }, null);

                // Entry menu open / close
                var openMenuMethod = typeof(ShipLogMapMode).GetMethod(
                    "OpenEntryMenu", privateInst, null,
                    new System.Type[] { typeof(string) }, null);
                var closeMenuMethod = typeof(ShipLogMapMode).GetMethod(
                    "CloseEntryMenu", privateInst, null, System.Type.EmptyTypes, null);

                // Entry navigation
                var setFocusMethod = typeof(ShipLogMapMode).GetMethod(
                    "SetEntryFocus", privateInst, null,
                    new System.Type[] { typeof(int) }, null);

                if (focusImmMethod == null || focusMethod == null || openMenuMethod == null ||
                    closeMenuMethod == null || setFocusMethod == null)
                {
                    DebugLogger.Log(LogCategory.State, "ShipLogHandler",
                        "ERROR: One or more methods not found on ShipLogMapMode");
                    return;
                }

                var postfixFocus    = new HarmonyMethod(typeof(ShipLogHandler), nameof(Postfix_FocusOnAstroObject));
                var postfixOpenMenu = new HarmonyMethod(typeof(ShipLogHandler), nameof(Postfix_OpenEntryMenu));
                var postfixClose    = new HarmonyMethod(typeof(ShipLogHandler), nameof(Postfix_CloseEntryMenu));
                var postfixSetFocus = new HarmonyMethod(typeof(ShipLogHandler), nameof(Postfix_SetEntryFocus));

                harmony.Patch(focusImmMethod,  postfix: postfixFocus);
                harmony.Patch(focusMethod,     postfix: postfixFocus);
                harmony.Patch(openMenuMethod,  postfix: postfixOpenMenu);
                harmony.Patch(closeMenuMethod, postfix: postfixClose);
                harmony.Patch(setFocusMethod,  postfix: postfixSetFocus);

                // Detective mode — announce newly revealed entries on enter
                var detectiveEnterMethod = typeof(ShipLogDetectiveMode).GetMethod(
                    "EnterMode",
                    BindingFlags.Public | BindingFlags.Instance, null,
                    new System.Type[] { typeof(string), typeof(List<ShipLogFact>) }, null);

                if (detectiveEnterMethod != null)
                    harmony.Patch(detectiveEnterMethod,
                        postfix: new HarmonyMethod(typeof(ShipLogHandler), nameof(Postfix_DetectiveEnterMode)));
                else
                    DebugLogger.Log(LogCategory.State, "ShipLogHandler",
                        "WARNING: ShipLogDetectiveMode.EnterMode not found");

                GlobalMessenger.AddListener("ShipLogUpdated", OnShipLogUpdated);

                DebugLogger.Log(LogCategory.State, "ShipLogHandler", "Initialized");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log(LogCategory.State, "ShipLogHandler",
                    "ERROR Initialize: " + ex.Message);
            }
        }

        /// <summary>Unsubscribes from events. Call from Main.OnDestroy().</summary>
        public void Cleanup()
        {
            GlobalMessenger.RemoveListener("ShipLogUpdated", OnShipLogUpdated);
        }

        #endregion

        #region Event handlers

        private void OnShipLogUpdated()
        {

            ScreenReader.Say(Loc.Get("shiplog_updated"));
        }

        #endregion

        #region Harmony patches

        /// <summary>
        /// Announces the focused planet name after focus changes.
        /// Fires for both immediate (on computer enter) and animated (while navigating) focus.
        /// </summary>
        private static void Postfix_FocusOnAstroObject(ShipLogMapMode __instance)
        {


            var nameField = _nameFieldInfo?.GetValue(__instance) as Text;
            if (nameField == null) return;

            string name = nameField.text;
            if (string.IsNullOrEmpty(name) || name == "Location Unknown") return;

            DebugLogger.Log(LogCategory.State, "ShipLogHandler", "Planet: " + name);
            ScreenReader.Say(name);
        }

        /// <summary>
        /// Announces "no discoveries" when a planet's entry menu opens with no visible entries.
        /// When entries exist, SetEntryFocus announces the first entry automatically.
        /// </summary>
        private static void Postfix_OpenEntryMenu(ShipLogMapMode __instance)
        {


            int maxIndex = (int)(_maxIndexInfo?.GetValue(__instance) ?? -1);
            if (maxIndex < 0)
            {
                DebugLogger.Log(LogCategory.State, "ShipLogHandler", "No discoveries for this planet.");
                ScreenReader.Say(Loc.Get("shiplog_no_discoveries"));
            }
            // Non-empty case: SetEntryFocus fires synchronously inside OpenEntryMenu
            // and announces the first entry — no duplicate announcement needed here.
        }

        /// <summary>
        /// Announces the focused entry name, exploration state, and all revealed facts.
        /// Fires only when focus actually changed (__result == true).
        /// </summary>
        private static void Postfix_SetEntryFocus(ShipLogMapMode __instance, int index, bool __result)
        {

            if (!__result) return; // Focus did not change

            var listItems = _listItemsInfo?.GetValue(__instance) as ShipLogEntryListItem[];
            if (listItems == null || index < 0 || index >= listItems.Length) return;

            ShipLogEntry entry = listItems[index].GetEntry();
            if (entry == null) return;

            string name  = entry.GetName(withLineBreaks: false);
            string state = StateString(entry.GetState());

            List<ShipLogFact> facts = entry.GetFactsForDisplay();

            var sb = new System.Text.StringBuilder();
            sb.Append(name);
            if (!string.IsNullOrEmpty(state))
            {
                sb.Append(", ");
                sb.Append(state);
            }
            foreach (ShipLogFact fact in facts)
            {
                string txt = fact.GetText();
                if (!string.IsNullOrEmpty(txt))
                {
                    sb.Append(". ");
                    sb.Append(txt);
                }
            }

            string announcement = sb.ToString();
            DebugLogger.Log(LogCategory.State, "ShipLogHandler", "Entry: " + name);
            ScreenReader.Say(announcement);
        }

        /// <summary>
        /// Announces "back to map" when the entry menu is closed.
        /// </summary>
        private static void Postfix_CloseEntryMenu()
        {

            ScreenReader.Say(Loc.Get("shiplog_back_to_map"));
        }

        /// <summary>
        /// Announces newly discovered entry names when detective mode opens with a reveal queue.
        /// Fires only when revealQueue is non-empty (i.e. new facts were just revealed).
        /// Also announces the hint to skip the animation and switch to map mode.
        /// </summary>
        private static void Postfix_DetectiveEnterMode(List<ShipLogFact> revealQueue)
        {

            if (revealQueue == null || revealQueue.Count == 0) return;

            // Collect unique entry names in reveal order
            var seen  = new System.Collections.Generic.HashSet<string>();
            var names = new System.Collections.Generic.List<string>();
            var mgr   = Locator.GetShipLogManager();

            foreach (ShipLogFact fact in revealQueue)
            {
                string entryID = fact.GetEntryID();
                if (seen.Add(entryID) && mgr != null)
                {
                    ShipLogEntry entry = mgr.GetEntry(entryID);
                    if (entry != null)
                        names.Add(entry.GetName(withLineBreaks: false));
                }
            }

            string entriesText = string.Join(", ", names.ToArray());
            string announcement = Loc.Get("shiplog_detective_reveal", entriesText);
            DebugLogger.Log(LogCategory.State, "ShipLogHandler", "Detective reveal: " + entriesText);
            ScreenReader.Say(announcement);
        }

        #endregion

        #region Helpers

        private static string StateString(ShipLogEntry.State state)
        {
            switch (state)
            {
                case ShipLogEntry.State.Explored: return Loc.Get("shiplog_explored");
                case ShipLogEntry.State.Rumored:  return Loc.Get("shiplog_rumored");
                default:                          return string.Empty;
            }
        }

        #endregion
    }
}
