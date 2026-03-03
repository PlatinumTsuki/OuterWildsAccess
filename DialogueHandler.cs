using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace OuterWildsAccess
{
    /// <summary>
    /// Reads NPC dialogue aloud via the screen reader.
    ///
    /// Harmony postfixes intercept DialogueBoxVer2 methods to:
    ///   - Capture the NPC name (SetNameField)
    ///   - Announce each dialogue page with choices (SetDialogueText)
    ///   - Announce the newly focused choice when navigating (OnUpPressed / OnDownPressed)
    ///

    /// </summary>
    public class DialogueHandler
    {
        #region Static state (shared with Harmony patches)

        // NPC name captured just before each SetDialogueText call
        private static string _currentNpcName = "";

        // Cached Reflection fields for reading private state from DialogueBoxVer2
        private static FieldInfo _selectedOptionField;
        private static FieldInfo _displayedOptionsField;

        #endregion

        #region Lifecycle

        /// <summary>
        /// Applies Harmony patches and subscribes to conversation events.
        /// Call from Main.Start().
        /// </summary>
        public void Initialize()
        {
            // Reset NPC name at the start of each new conversation
            GlobalMessenger.AddListener("EnterConversation", OnEnterConversation);

            try
            {
                // Cache Reflection fields once — avoids per-frame overhead
                _selectedOptionField = typeof(DialogueBoxVer2).GetField(
                    "_selectedOption", BindingFlags.NonPublic | BindingFlags.Instance);
                _displayedOptionsField = typeof(DialogueBoxVer2).GetField(
                    "_displayedOptions", BindingFlags.NonPublic | BindingFlags.Instance);

                if (_selectedOptionField == null || _displayedOptionsField == null)
                {
                    DebugLogger.Log(LogCategory.State, "DialogueHandler",
                        "ERROR: Reflection fields not found on DialogueBoxVer2");
                    return;
                }

                var harmony = new Harmony("com.outerwildsaccess.dialogue");

                // Capture NPC name before each page
                var setNameMethod = typeof(DialogueBoxVer2).GetMethod(
                    "SetNameField", BindingFlags.Public | BindingFlags.Instance);
                harmony.Patch(setNameMethod,
                    postfix: new HarmonyMethod(typeof(DialogueHandler), nameof(Postfix_SetNameField)));

                // Announce each dialogue page (text + options)
                var setTextMethod = typeof(DialogueBoxVer2).GetMethod(
                    "SetDialogueText", BindingFlags.Public | BindingFlags.Instance);
                harmony.Patch(setTextMethod,
                    postfix: new HarmonyMethod(typeof(DialogueHandler), nameof(Postfix_SetDialogueText)));

                // Announce newly focused option when navigating with Up / Down
                var onUpMethod = typeof(DialogueBoxVer2).GetMethod(
                    "OnUpPressed", BindingFlags.Public | BindingFlags.Instance);
                var onDownMethod = typeof(DialogueBoxVer2).GetMethod(
                    "OnDownPressed", BindingFlags.Public | BindingFlags.Instance);
                harmony.Patch(onUpMethod,
                    postfix: new HarmonyMethod(typeof(DialogueHandler), nameof(Postfix_OnOptionChanged)));
                harmony.Patch(onDownMethod,
                    postfix: new HarmonyMethod(typeof(DialogueHandler), nameof(Postfix_OnOptionChanged)));

                DebugLogger.Log(LogCategory.State, "DialogueHandler", "Initialisé");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log(LogCategory.State, "DialogueHandler",
                    "ERROR Initialize: " + ex.Message);
            }
        }

        /// <summary>
        /// Unsubscribes from events. Call from Main.OnDestroy().
        /// </summary>
        public void Cleanup()
        {
            GlobalMessenger.RemoveListener("EnterConversation", OnEnterConversation);
            _currentNpcName = "";
        }

        #endregion

        #region Event handlers

        private void OnEnterConversation()
        {
            // Reset so signs/recordings (which never call SetNameField) get no name prefix
            _currentNpcName = "";
        }

        #endregion

        #region Harmony patches

        /// <summary>
        /// Captures the translated NPC name whenever the name field is updated.
        /// Called by CharacterDialogueTree.DisplayDialogueBox2() before SetDialogueText.
        /// </summary>
        private static void Postfix_SetNameField(string name)
        {
            _currentNpcName = name ?? "";
        }

        /// <summary>
        /// Announces the dialogue page text followed by any available choices.
        /// Fires every time a new page of dialogue is displayed.
        /// </summary>
        private static void Postfix_SetDialogueText(
            DialogueBoxVer2 __instance, string richText, List<DialogueOption> listOptions)
        {


            string cleanText = TextUtils.CleanText(richText);
            if (string.IsNullOrEmpty(cleanText)) return;

            // Read filtered (condition-checked) options from the private field.
            // SetDialogueText calls SetUpOptions() synchronously before returning,
            // so _displayedOptions is already populated when this postfix runs.
            var displayed = _displayedOptionsField?.GetValue(__instance) as List<DialogueOption>;

            string announcement;
            if (displayed != null && displayed.Count > 0)
            {
                // Announce text + first option only (no list, no numbering)
                string firstOpt = TextUtils.CleanText(displayed[0].Text);
                announcement = cleanText + " " + firstOpt;
            }
            else
            {
                announcement = cleanText;
            }

            DebugLogger.Log(LogCategory.State, "DialogueHandler", "Page: " + announcement);
            ScreenReader.Say(announcement);
        }

        /// <summary>
        /// Announces the newly focused choice after the player presses Up or Down.
        /// </summary>
        private static void Postfix_OnOptionChanged(DialogueBoxVer2 __instance)
        {

            if (_selectedOptionField == null || _displayedOptionsField == null) return;

            int selectedIdx = (int)(_selectedOptionField.GetValue(__instance) ?? -1);
            var displayed   = _displayedOptionsField.GetValue(__instance) as List<DialogueOption>;

            if (displayed == null || selectedIdx < 0 || selectedIdx >= displayed.Count) return;

            string optText = TextUtils.CleanText(displayed[selectedIdx].Text);
            DebugLogger.Log(LogCategory.State, "DialogueHandler", "Option focus: " + optText);
            ScreenReader.Say(optText);
        }

        #endregion
    }
}
