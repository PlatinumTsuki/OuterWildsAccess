using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace OuterWildsAccess
{
    /// <summary>
    /// Announces on-screen button prompts from PromptManager.
    ///
    /// Monitored lists: bottomLeft, bottomCenter, center.
    /// Announces newly visible prompts with their associated button name appended.
    ///
    /// Command labels come from InputTransitionUtil.GetLabel (action names like "Interagir").
    /// In debug mode, also logs the raw texture name to help map physical button names later.
    /// </summary>
    public class PromptHandler
    {
        // Two pre-allocated sets swapped each frame — avoids per-frame heap allocation
        private HashSet<string> _currentVisible = new HashSet<string>();
        private HashSet<string> _prevVisible    = new HashSet<string>();

        // Cooldown: don't re-announce the same text within N seconds
        private Dictionary<string, float> _announcedAt = new Dictionary<string, float>();
        private const float COOLDOWN = 4f;

        // Reflection field cache (populated once in Initialize)
        private FieldInfo _pmBottomLeftField;
        private FieldInfo _pmBottomCenterField;
        private FieldInfo _pmCenterField;
        private FieldInfo _pmTopRightField;
        private FieldInfo _pmTopLeftField;
        private FieldInfo _pmWorldListsField;   // List<ScreenPromptList>
        private FieldInfo _splPromptsField;
        private FieldInfo _spCommandListField;

        public void Initialize()
        {
            var pmType = typeof(PromptManager);
            var flags  = BindingFlags.NonPublic | BindingFlags.Instance;

            _pmBottomLeftField   = pmType.GetField("_bottomLeftList",   flags);
            _pmBottomCenterField = pmType.GetField("_bottomCenterList", flags);
            _pmCenterField       = pmType.GetField("_centerList",       flags);
            _pmTopRightField     = pmType.GetField("_topRightList",     flags);
            _pmTopLeftField      = pmType.GetField("_topLeftList",      flags);
            _pmWorldListsField   = pmType.GetField("_worldPromptLists", flags);
            _splPromptsField     = typeof(ScreenPromptList).GetField("_listPrompts", flags);
            _spCommandListField  = typeof(ScreenPrompt).GetField("_commandList",     flags);

            DebugLogger.Log(LogCategory.State, "PromptHandler", "Initialisé");
        }

        /// <summary>Call when the scene changes to reset tracking state.</summary>
        public void OnSceneLoaded()
        {
            _currentVisible.Clear();
            _prevVisible.Clear();
            _announcedAt.Clear();
        }

        /// <summary>
        /// Resets prompt tracking after auto-walk arrival so that any currently
        /// visible prompts are re-announced on the next frame.
        /// </summary>
        public void OnArrival()
        {
            _prevVisible.Clear();
            _announcedAt.Clear();
        }

        public void Update()
        {


            // Skip prompts in menus and rebinding — MenuHandler already announces menu items
            var inputMode = OWInput.GetInputMode();
            if (inputMode == InputMode.Menu || inputMode == InputMode.Rebinding
                || inputMode == InputMode.KeyboardInput)
                return;

            PromptManager pm = Locator.GetPromptManager();
            if (pm == null) return;

            // Reuse pre-allocated set — no heap allocation per frame
            _currentVisible.Clear();
            CollectVisible(pm, _pmBottomLeftField,   _currentVisible);
            CollectVisible(pm, _pmBottomCenterField, _currentVisible);
            CollectVisible(pm, _pmCenterField,       _currentVisible);
            CollectVisible(pm, _pmTopRightField,     _currentVisible);
            CollectVisible(pm, _pmTopLeftField,      _currentVisible);
            CollectVisibleWorldLists(pm, _currentVisible);

            float now = Time.time;
            foreach (string text in _currentVisible)
            {
                // Already visible last frame — not new
                if (_prevVisible.Contains(text)) continue;

                // Still within cooldown from last announcement of this text
                if (_announcedAt.TryGetValue(text, out float t) && (now - t) < COOLDOWN)
                    continue;

                _announcedAt[text] = now;
                DebugLogger.Log(LogCategory.State, "PromptHandler", $"Prompt: {text}");
                ScreenReader.Say(text, SpeechPriority.Normal);
            }

            // Swap buffers: current becomes previous, previous is reused next frame
            var swap        = _prevVisible;
            _prevVisible    = _currentVisible;
            _currentVisible = swap;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Private helpers
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Collects prompts from the dynamic world-space prompt lists.</summary>
        private void CollectVisibleWorldLists(PromptManager pm, HashSet<string> result)
        {
            if (_pmWorldListsField == null || _splPromptsField == null) return;
            try
            {
                var worldLists = _pmWorldListsField.GetValue(pm) as System.Collections.IList;
                if (worldLists == null) return;
                foreach (object obj in worldLists)
                {
                    if (obj is ScreenPromptList spl)
                        CollectFromList(spl, result);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log(LogCategory.State, "PromptHandler", $"CollectVisibleWorldLists: {ex.Message}");
            }
        }

        private void CollectVisible(PromptManager pm, FieldInfo listField, HashSet<string> result)
        {
            if (listField == null) return;
            try
            {
                ScreenPromptList spl = listField.GetValue(pm) as ScreenPromptList;
                if (spl != null) CollectFromList(spl, result);
            }
            catch (Exception ex)
            {
                DebugLogger.Log(LogCategory.State, "PromptHandler", $"CollectVisible: {ex.Message}");
            }
        }

        private void CollectFromList(ScreenPromptList spl, HashSet<string> result)
        {
            if (_splPromptsField == null) return;
            // Skip lists that are deactivated by PromptManager
            if (!spl.gameObject.activeInHierarchy) return;

            List<ScreenPrompt> prompts = _splPromptsField.GetValue(spl) as List<ScreenPrompt>;
            if (prompts == null) return;

            // Snapshot to avoid issues if the list is modified mid-frame
            foreach (ScreenPrompt p in new List<ScreenPrompt>(prompts))
            {
                // IsDisplaying checks IsVisible + highest priority in list
                if (!spl.IsDisplaying(p)) continue;
                string text = BuildText(p);
                if (!string.IsNullOrEmpty(text))
                    result.Add(text);
            }
        }

        /// <summary>
        /// Builds the full announcement text for a prompt.
        /// Format: "Action, touche NomTouche" — action first, then the button.
        /// </summary>
        private string BuildText(ScreenPrompt prompt)
        {
            string text = prompt.GetText()?.Trim();
            if (string.IsNullOrEmpty(text)) return null;

            // Get command list via Reflection
            List<IInputCommands> cmds = null;
            try
            {
                if (_spCommandListField != null)
                    cmds = _spCommandListField.GetValue(prompt) as List<IInputCommands>;
            }
            catch { }

            // Build the button label string from all commands
            string buttonLabel = null;
            if (cmds != null && cmds.Count > 0)
            {
                var labels = new System.Text.StringBuilder();
                for (int i = 0; i < cmds.Count; i++)
                {
                    string lbl = GetCmdLabel(cmds[i]);
                    if (string.IsNullOrEmpty(lbl)) continue;
                    if (labels.Length > 0) labels.Append(Loc.Get("prompt_and"));
                    labels.Append(lbl);
                }
                if (labels.Length > 0)
                    buttonLabel = labels.ToString();
            }

            // Strip <CMD> / <CMD1> / <CMD2> markers from the text
            if (text.Contains("<"))
            {
                text = text.Replace("<CMD>",  "");
                text = text.Replace("<CMD1>", "");
                text = text.Replace("<CMD2>", "");
                text = TextUtils.StripTags(text)?.Trim();
            }

            // Clean up any double spaces left after marker removal
            text = TextUtils.NormalizeWhitespace(text)?.Trim();
            if (string.IsNullOrEmpty(text)) return null;

            // Append button: "Action, touche X" or "Action, touches X et Y"
            if (!string.IsNullOrEmpty(buttonLabel))
            {
                string prefix = cmds.Count > 1 ? Loc.Get("prompt_button_plural") : Loc.Get("prompt_button_single");
                text = text + ", " + prefix + " " + buttonLabel;
            }

            return text;
        }

        private string GetCmdLabel(List<IInputCommands> cmds, int index)
        {
            if (cmds == null || index >= cmds.Count) return null;
            return InputHelper.GetCommandLabel(cmds[index]);
        }

        private string GetCmdLabel(IInputCommands cmd) => InputHelper.GetCommandLabel(cmd);

    }
}
