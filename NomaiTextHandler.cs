using HarmonyLib;
using UnityEngine;

namespace OuterWildsAccess
{
    /// <summary>
    /// Reads Nomai text aloud via screen reader when the translator targets it.
    ///
    /// 6 Harmony postfixes on NomaiTranslatorProp:
    ///   - SetNomaiText(NomaiText, int) — wall text + computers (with ID)
    ///   - SetNomaiText(NomaiText)      — fallback without ID
    ///   - SetNomaiAudio(NomaiAudioVolume, int) — audio recordings
    ///   - ClearNomaiText()             — reset tracking on look-away
    ///   - ClearNomaiAudio()            — reset tracking for audio
    ///   - SwitchTextNode(string)       — fallback for audio page changes
    ///
    /// Auto-translates text blocks so the ship log updates automatically
    /// (blind player cannot see the word-by-word animation completing).
    ///
    /// Tree context: wall text prefixed "Message :" (root) or "Réponse :" (reply).

    /// </summary>
    public class NomaiTextHandler
    {
        #region Static state (shared with Harmony patches)

        private static NomaiText _lastNomaiText;
        private static int       _lastTextID = -999;
        private static string    _lastAnnouncedRawText;

        // Reflection for tree context (ParentID)
        private static System.Reflection.FieldInfo _dictField;
        private static System.Reflection.FieldInfo _parentIDField;

        #endregion

        #region Lifecycle

        /// <summary>
        /// Applies Harmony patches on NomaiTranslatorProp.
        /// Call from Main.Start().
        /// </summary>
        public void Initialize()
        {
            try
            {
                var rf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

                // Cache Reflection for ParentID access
                _dictField = typeof(NomaiText).GetField("_dictNomaiTextData",
                    rf | System.Reflection.BindingFlags.FlattenHierarchy);

                var dataType = typeof(NomaiText).GetNestedType("NomaiTextData",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                if (dataType != null)
                    _parentIDField = dataType.GetField("ParentID");

                DebugLogger.Log(LogCategory.State, "NomaiTextHandler",
                    "Reflection: dict=" + (_dictField != null) + " parentID=" + (_parentIDField != null));

                var harmony = new Harmony("com.outerwildsaccess.nomaitext");
                var pub = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;

                // Patch 1: SetNomaiText(NomaiText, int)
                var setTextWithID = typeof(NomaiTranslatorProp).GetMethod("SetNomaiText", pub, null,
                    new System.Type[] { typeof(NomaiText), typeof(int) }, null);
                if (setTextWithID != null)
                {
                    harmony.Patch(setTextWithID,
                        postfix: new HarmonyMethod(typeof(NomaiTextHandler), nameof(Postfix_SetNomaiText_WithID)));
                    DebugLogger.Log(LogCategory.State, "NomaiTextHandler", "Patched SetNomaiText(NomaiText,int)");
                }

                // Patch 2: SetNomaiText(NomaiText)
                var setTextNoID = typeof(NomaiTranslatorProp).GetMethod("SetNomaiText", pub, null,
                    new System.Type[] { typeof(NomaiText) }, null);
                if (setTextNoID != null)
                {
                    harmony.Patch(setTextNoID,
                        postfix: new HarmonyMethod(typeof(NomaiTextHandler), nameof(Postfix_SetNomaiText_NoID)));
                    DebugLogger.Log(LogCategory.State, "NomaiTextHandler", "Patched SetNomaiText(NomaiText)");
                }

                // Patch 3: SetNomaiAudio(NomaiAudioVolume, int)
                var setAudio = typeof(NomaiTranslatorProp).GetMethod("SetNomaiAudio", pub);
                if (setAudio != null)
                {
                    harmony.Patch(setAudio,
                        postfix: new HarmonyMethod(typeof(NomaiTextHandler), nameof(Postfix_SetNomaiAudio)));
                    DebugLogger.Log(LogCategory.State, "NomaiTextHandler", "Patched SetNomaiAudio");
                }

                // Patch 4: ClearNomaiText()
                var clearText = typeof(NomaiTranslatorProp).GetMethod("ClearNomaiText", pub);
                if (clearText != null)
                {
                    harmony.Patch(clearText,
                        postfix: new HarmonyMethod(typeof(NomaiTextHandler), nameof(Postfix_ClearNomaiText)));
                }

                // Patch 5: ClearNomaiAudio()
                var clearAudio = typeof(NomaiTranslatorProp).GetMethod("ClearNomaiAudio", pub);
                if (clearAudio != null)
                {
                    harmony.Patch(clearAudio,
                        postfix: new HarmonyMethod(typeof(NomaiTextHandler), nameof(Postfix_ClearNomaiAudio)));
                }

                // Patch 6: SwitchTextNode(string) — private, fallback for page changes
                var switchNode = typeof(NomaiTranslatorProp).GetMethod("SwitchTextNode", rf);
                if (switchNode != null)
                {
                    harmony.Patch(switchNode,
                        postfix: new HarmonyMethod(typeof(NomaiTextHandler), nameof(Postfix_SwitchTextNode)));
                    DebugLogger.Log(LogCategory.State, "NomaiTextHandler", "Patched SwitchTextNode");
                }

                DebugLogger.Log(LogCategory.State, "NomaiTextHandler", "Initialized");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log(LogCategory.State, "NomaiTextHandler", "Initialize failed: " + ex.Message);
                ScreenReader.Say(Loc.Get("nomai_init_error"));
            }
        }

        /// <summary>Resets state. Call from Main.OnDestroy().</summary>
        public void Cleanup()
        {
            _lastNomaiText = null;
            _lastTextID = -999;
            _lastAnnouncedRawText = null;
        }

        #endregion

        #region Harmony patches

        private static void Postfix_SetNomaiText_WithID(NomaiText text, int textID)
        {

            if (text == _lastNomaiText && textID == _lastTextID) return;
            AnnounceNomaiText(text, textID);
        }

        private static void Postfix_SetNomaiText_NoID(NomaiText text)
        {

            if (text == _lastNomaiText && _lastTextID == -1) return;
            AnnounceNomaiText(text, -1);
        }

        private static void Postfix_SetNomaiAudio(NomaiAudioVolume audio, int textPage)
        {

            if (audio == null) return;

            NomaiText audioText = audio.GetAudioText();
            if (audioText == null) return;

            if (audioText == _lastNomaiText && textPage == _lastTextID) return;

            string rawText = null;
            try { rawText = audioText.GetTextNode(textPage); }
            catch { return; }

            if (string.IsNullOrEmpty(rawText)) return;

            string cleanText = TextUtils.CleanText(rawText);
            if (string.IsNullOrEmpty(cleanText)) return;

            // Page info for multi-page recordings
            string announcement = cleanText;
            try
            {
                int totalPages = audioText.GetNumTextBlocks();
                if (totalPages > 1)
                    announcement = Loc.Get("nomai_page", textPage, totalPages) + " " + cleanText;
            }
            catch { /* ignore */ }

            _lastNomaiText = audioText;
            _lastTextID = textPage;
            _lastAnnouncedRawText = rawText;

            DebugLogger.Log(LogCategory.State, "NomaiTextHandler",
                "Audio [" + textPage + "]: " + cleanText);
            ScreenReader.Say(announcement);

            // Auto-translate for ship log
            try
            {
                if (!audioText.IsTranslated(textPage))
                    audioText.SetAsTranslated(textPage);
            }
            catch { /* ignore */ }
        }

        private static void Postfix_ClearNomaiText()
        {
            _lastNomaiText = null;
            _lastTextID = -999;
            _lastAnnouncedRawText = null;
        }

        private static void Postfix_ClearNomaiAudio()
        {
            _lastNomaiText = null;
            _lastTextID = -999;
            _lastAnnouncedRawText = null;
        }

        /// <summary>
        /// Fallback for text changes not caught by SetNomaiText/SetNomaiAudio
        /// (e.g. audio page cycling).
        /// </summary>
        private static void Postfix_SwitchTextNode(string textNode)
        {

            if (string.IsNullOrEmpty(textNode)) return;
            if (textNode == _lastAnnouncedRawText) return;

            string cleanText = TextUtils.CleanText(textNode);
            if (string.IsNullOrEmpty(cleanText)) return;

            _lastAnnouncedRawText = textNode;

            DebugLogger.Log(LogCategory.State, "NomaiTextHandler",
                "SwitchTextNode fallback: " + cleanText);
            ScreenReader.Say(cleanText);
        }

        #endregion

        #region Private helpers

        /// <summary>
        /// Core announcement logic for Nomai text blocks.
        /// Gets text, adds tree context, announces, and auto-translates.
        /// </summary>
        private static void AnnounceNomaiText(NomaiText text, int textID)
        {
            if (text == null) return;

            // Skip ghost text (throws NotSupportedException on GetTextNode)
            if (text is GhostWallText) return;

            string rawText = null;
            try { rawText = text.GetTextNode(textID); }
            catch (System.Exception ex)
            {
                DebugLogger.Log(LogCategory.State, "NomaiTextHandler",
                    "GetTextNode failed: " + ex.Message);
                return;
            }

            if (string.IsNullOrEmpty(rawText)) return;

            string cleanText = TextUtils.CleanText(rawText);
            if (string.IsNullOrEmpty(cleanText)) return;

            // Tree context for wall text (root vs reply)
            string context = GetTreeContext(text, textID);
            string announcement = context + cleanText;

            _lastNomaiText = text;
            _lastTextID = textID;
            _lastAnnouncedRawText = rawText;

            DebugLogger.Log(LogCategory.State, "NomaiTextHandler",
                "Text [" + textID + "]: " + announcement);
            ScreenReader.Say(announcement);

            // Auto-translate so ship log updates
            try
            {
                if (textID >= 0 && !text.IsTranslated(textID))
                    text.SetAsTranslated(textID);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log(LogCategory.State, "NomaiTextHandler",
                    "Auto-translate failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Returns "Message : " for root nodes, "Réponse : " for replies.
        /// Only applies to NomaiWallText (tree structure).
        /// </summary>
        private static string GetTreeContext(NomaiText text, int textID)
        {
            if (!(text is NomaiWallText)) return "";
            if (textID < 0) return "";

            int parentID = GetParentID(text, textID);
            return parentID == -1
                ? Loc.Get("nomai_root") + " "
                : Loc.Get("nomai_reply") + " ";
        }

        /// <summary>
        /// Gets the ParentID for a text block via Reflection on _dictNomaiTextData.
        /// Returns -1 if root or if Reflection fails.
        /// </summary>
        private static int GetParentID(NomaiText text, int textID)
        {
            if (_dictField == null || _parentIDField == null) return -1;

            try
            {
                var dict = _dictField.GetValue(text);
                if (dict == null) return -1;

                var dictType = dict.GetType();
                var containsKey = dictType.GetMethod("ContainsKey");
                var itemProp = dictType.GetProperty("Item");
                if (containsKey == null || itemProp == null) return -1;

                if (!(bool)containsKey.Invoke(dict, new object[] { textID })) return -1;

                var textData = itemProp.GetValue(dict, new object[] { textID });
                if (textData == null) return -1;

                return (int)_parentIDField.GetValue(textData);
            }
            catch
            {
                return -1;
            }
        }

        #endregion
    }
}
