namespace OuterWildsAccess
{
    /// <summary>
    /// Returns user-friendly display names for input commands.
    ///
    /// Priority chain:
    ///   1. Physical button name(s) joined from all UI textures  (e.g. "L2 + R2", "Cross")
    ///   2. Localized action name from Loc._keyLabels            (e.g. "Interact")
    ///   3. Sanitized raw GetLabel result                        (e.g. "Match Velocity")
    ///
    /// Texture naming conventions observed in this game:
    ///   Keyboard : "Keyboard_Black_E", "Keyboard_Black_Return", …
    ///   Xbox     : "XboxOne_A", "XboxOne_B", "XboxOne_Menu", …
    ///   PS4/PS5  : "PS4_Cross", "PS5_Circle", "DualSense_L2", …
    ///
    /// Debug mode logs raw texture names AND raw GetLabel values — use them to
    /// expand Loc._keyLabels when encountering unmapped actions in-game.
    /// </summary>
    public static class InputHelper
    {
        /// <summary>
        /// Returns the best available display label for the given command,
        /// or null if nothing meaningful could be determined.
        /// Supports multi-button combos (all textures joined with " + ").
        /// </summary>
        public static string GetCommandLabel(IInputCommands cmd)
        {
            if (cmd == null) return null;
            try
            {
                bool gamepad = OWInput.UsingGamepad();
                var  textures = cmd.GetUITextures(gamepad);

                if (textures != null && textures.Count > 0)
                {
                    // Collect a label for every texture (multi-button combo support)
                    var parts = new System.Collections.Generic.List<string>(textures.Count);
                    foreach (var tex in textures)
                    {
                        if (tex == null) continue;
                        DebugLogger.Log(LogCategory.State, "InputHelper", $"Texture: {tex.name}");
                        string p = GetPhysicalButtonName(tex.name);
                        if (!string.IsNullOrEmpty(p)) parts.Add(p);
                    }
                    if (parts.Count > 0)
                        return string.Join(" + ", parts);
                }

                // Fallback 1: localized action name from our dictionary
                string rawLabel = InputTransitionUtil.GetLabel(cmd.CommandType);
                DebugLogger.Log(LogCategory.State, "InputHelper", $"GetLabel: {rawLabel}");
                string localized = Loc.LocalizeKeyLabel(rawLabel);
                if (!string.IsNullOrEmpty(localized) && localized != rawLabel)
                    return TextUtils.CleanText(localized);

                // Fallback 2: sanitize the raw label so the screen reader reads something useful
                // e.g. "MATCH_VELOCITY" → "Match Velocity"
                return SanitizeRawLabel(rawLabel);
            }
            catch { return null; }
        }

        /// <summary>
        /// Converts an all-caps underscored label into a readable title-case string.
        /// Returns null if the result would be empty.
        /// </summary>
        private static string SanitizeRawLabel(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string spaced = raw.Replace("_", " ").Trim();
            if (spaced.Length == 0) return null;
            // Title case: uppercase first letter of each word, lowercase the rest
            var words = spaced.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length == 0) continue;
                words[i] = char.ToUpper(words[i][0]) +
                           (words[i].Length > 1 ? words[i].Substring(1).ToLower() : "");
            }
            return string.Join(" ", words);
        }

        /// <summary>
        /// Maps a texture asset name to a localized button label.
        /// Returns null if the name is unknown.
        /// </summary>
        public static string GetPhysicalButtonName(string textureName)
        {
            if (string.IsNullOrEmpty(textureName)) return null;

            if (textureName.StartsWith("Keyboard_"))
            {
                // Strip colour prefixes: "Keyboard_Black_", "Keyboard_White_", "Keyboard_"
                string key = textureName;
                foreach (string prefix in new[] { "Keyboard_Black_", "Keyboard_White_", "Keyboard_" })
                {
                    if (key.StartsWith(prefix)) { key = key.Substring(prefix.Length); break; }
                }
                return MapKeyboardKey(key);
            }

            if (textureName.StartsWith("XboxOne_"))
                return MapXboxButton(textureName.Substring("XboxOne_".Length));

            // All known PlayStation prefixes (PS4, PS5, DualSense, DualShock)
            foreach (string psPrefix in new[] { "PS5_", "PS4_", "DualSense_", "DualShock4_", "DualShock_", "PS_" })
            {
                if (textureName.StartsWith(psPrefix))
                    return MapPSButton(textureName.Substring(psPrefix.Length));
            }

            // Unknown prefix — log in debug mode so we can add a mapping later
            DebugLogger.Log(LogCategory.State, "InputHelper", $"Unknown texture: {textureName}");
            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Keyboard
        // ─────────────────────────────────────────────────────────────────────

        private static string MapKeyboardKey(string key)
        {
            switch (key)
            {
                case "Return":
                case "Enter":       return Loc.Get("btn_enter");
                case "Space":       return Loc.Get("btn_space");
                case "Escape":      return Loc.Get("btn_escape");
                case "Backspace":   return Loc.Get("btn_backspace");
                case "Tab":         return "Tab";
                case "Delete":      return Loc.Get("btn_delete");
                case "Up":          return Loc.Get("btn_up");
                case "Down":        return Loc.Get("btn_down");
                case "Left":        return Loc.Get("btn_left");
                case "Right":       return Loc.Get("btn_right");
                case "LeftShift":
                case "RightShift":  return "Shift";
                case "LeftControl":
                case "RightControl":return "Ctrl";
                case "LeftAlt":
                case "RightAlt":    return "Alt";
                default:            return key;   // single letter, F1-F12, etc.
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Xbox One / Xbox Series
        // ─────────────────────────────────────────────────────────────────────

        private static string MapXboxButton(string btn)
        {
            switch (btn)
            {
                case "A":           return "A";
                case "B":           return "B";
                case "X":           return "X";
                case "Y":           return "Y";
                case "LB":          return "LB";
                case "RB":          return "RB";
                case "LT":          return "LT";
                case "RT":          return "RT";
                case "Menu":        return "Menu";
                case "View":        return Loc.Get("btn_xbox_view");
                case "LS":          return "LS";
                case "RS":          return "RS";
                case "DPad_Up":     return Loc.Get("btn_dpad_up");
                case "DPad_Down":   return Loc.Get("btn_dpad_down");
                case "DPad_Left":   return Loc.Get("btn_dpad_left");
                case "DPad_Right":  return Loc.Get("btn_dpad_right");
                default:            return btn;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // PlayStation 4 / 5
        // ─────────────────────────────────────────────────────────────────────

        private static string MapPSButton(string btn)
        {
            switch (btn)
            {
                case "Cross":       return Loc.Get("btn_ps_cross");
                case "Circle":      return Loc.Get("btn_ps_circle");
                case "Square":      return Loc.Get("btn_ps_square");
                case "Triangle":    return "Triangle";
                case "L1":          return "L1";
                case "R1":          return "R1";
                case "L2":          return "L2";
                case "R2":          return "R2";
                case "L3":          return "L3";
                case "R3":          return "R3";
                case "Options":     return "Options";
                case "Share":       return Loc.Get("btn_ps_share");
                case "Create":      return Loc.Get("btn_ps_create");
                case "Touchpad":    return Loc.Get("btn_ps_touchpad");
                case "DPad_Up":     return Loc.Get("btn_dpad_up");
                case "DPad_Down":   return Loc.Get("btn_dpad_down");
                case "DPad_Left":   return Loc.Get("btn_dpad_left");
                case "DPad_Right":  return Loc.Get("btn_dpad_right");
                default:            return btn;
            }
        }
    }
}
