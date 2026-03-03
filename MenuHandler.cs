using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OuterWildsAccess
{
    /// <summary>
    /// Handles menu accessibility announcements.
    ///
    /// On focus change: announces label + current state (e.g. "Volume musique, 7 sur 10").
    /// On value change with same focus: announces the new value only (e.g. "8 sur 10").
    ///
    /// Supported option types:
    ///   ToggleElement          → "Label, Activé / Désactivé"
    ///   OptionsSelectorElement → "Label, [option texte]"
    ///   SliderElement          → "Label, [n] sur 10"
    ///   MenuOption / Button    → "Label"
    /// </summary>
    public class MenuHandler
    {
        private GameObject _lastSelected;
        private string     _lastAnnouncedValue;
        private bool       _suppressNextFocus;
        private bool       _wasRebinding;
        private string     _preRebindKey;

        // Whether the next SayMenuText call should queue instead of interrupt
        // (used for tab name → first item cascade on menu open)
        private bool _queueNextMenuSay;


        public void Initialize()
        {
            MenuStackManager.SharedInstance.OnMenuPush += OnMenuPush;
            MenuStackManager.SharedInstance.OnMenuPop += OnMenuPop;

            DebugLogger.Log(LogCategory.State, "MenuHandler", "Initialisé");
        }

        public void Cleanup()
        {
            MenuStackManager.SharedInstance.OnMenuPush -= OnMenuPush;
            MenuStackManager.SharedInstance.OnMenuPop -= OnMenuPop;
        }

        public void Update()
        {
            if (EventSystem.current == null) return;

            GameObject current = EventSystem.current.currentSelectedGameObject;

            if (current != _lastSelected)
            {
                // Focus changed — announce label + state
                _lastSelected = current;
                _lastAnnouncedValue = null;
                _wasRebinding = false;
                _preRebindKey = null;

                if (current == null) return;

                if (_suppressNextFocus)
                {
                    _suppressNextFocus = false;
                    return;
                }

                string announcement = GetLabelWithState(current);
                if (!string.IsNullOrEmpty(announcement))
                {
                    DebugLogger.Log(LogCategory.State, "MenuHandler", $"Focus: {announcement}");
                    SayMenuText(announcement);
                    _lastAnnouncedValue = GetCurrentValue(current);
                }
            }
            else if (current != null)
            {
                // KeyRebindingElement — detect entering / exiting rebinding mode
                var rebindEl = current.GetComponent<KeyRebindingElement>();
                if (rebindEl != null)
                {
                    bool isRebinding = rebindEl.IsInRebindingMode();
                    if (isRebinding && !_wasRebinding)
                    {
                        _wasRebinding = true;
                        _preRebindKey = GetCurrentValue(current);
                        ScreenReader.Say(Loc.Get("rebinding_enter"));
                    }
                    else if (!isRebinding && _wasRebinding)
                    {
                        _wasRebinding = false;
                        string newKey = GetCurrentValue(current);
                        _lastAnnouncedValue = newKey;
                        ScreenReader.Say(newKey != _preRebindKey
                            ? Loc.Get("rebinding_done", newKey ?? "?")
                            : Loc.Get("rebinding_cancel"));
                        _preRebindKey = null;
                    }
                }

                // Same focus — announce if value changed (slider, toggle, selector)
                // Skip during active rebinding to avoid spurious announcements
                if (!_wasRebinding)
                {
                    string currentValue = GetCurrentValue(current);
                    if (currentValue != null && currentValue != _lastAnnouncedValue)
                    {
                        _lastAnnouncedValue = currentValue;
                        DebugLogger.Log(LogCategory.State, "MenuHandler", $"Valeur: {currentValue}");
                        ScreenReader.Say(currentValue);
                    }
                }
            }
        }

        private void OnMenuPush(Menu menu)
        {
            DebugLogger.LogState($"MenuHandler: menu ouvert — {menu.gameObject.name}");

            // PopupMenu: announce "message. Confirmer / Annuler" in one go, then suppress the button focus
            var popup = menu as PopupMenu;
            if (popup == null)
            {
                // If this is a tabbed menu, announce the active tab name first so the
                // user knows which tab they land on. The first item follows as a cascade.
                if (menu is TabbedMenu tm)
                {
                    TabButton activeTab = tm.GetLastSelectedTabButton();
                    if (activeTab != null)
                    {
                        string tabName = GetBaseLabel(activeTab.gameObject);
                        if (!string.IsNullOrEmpty(tabName))
                        {
                            DebugLogger.Log(LogCategory.State, "MenuHandler", $"Menu ouvert — onglet: {tabName}");
                            ScreenReader.Say(tabName);
                            _queueNextMenuSay = true;
                        }
                    }
                }

                // Regular menu (pause, options, title…)
                // Announce the default selected item so the user knows where focus is.
                try
                {
                    Selectable firstSel = menu.GetSelectOnActivate();
                    if (firstSel != null)
                    {
                        string itemLabel = GetLabelWithState(firstSel.gameObject);
                        if (!string.IsNullOrEmpty(itemLabel))
                        {
                            DebugLogger.Log(LogCategory.State, "MenuHandler", $"Menu ouvert — premier item: {itemLabel}");
                            SayMenuText(itemLabel); // cascade-aware: queues after tab name if applicable
                            _suppressNextFocus = true;
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.LogState($"MenuHandler: premier item échoué: {ex.Message}");
                }

                // Fallback: no default item found — announce navigation hint
                try
                {
                    string confirmKey = InputHelper.GetCommandLabel(InputLibrary.menuConfirm);
                    string cancelKey  = InputHelper.GetCommandLabel(InputLibrary.cancel);
                    if (string.IsNullOrEmpty(confirmKey)) confirmKey = "Confirmer";
                    if (string.IsNullOrEmpty(cancelKey))  cancelKey  = "Annuler";
                    SayMenuText(Loc.Get("menu_controls_hint", confirmKey, cancelKey));
                }
                catch (Exception ex)
                {
                    DebugLogger.LogState($"MenuHandler: hint controls échoué: {ex.Message}");
                }
                return;
            }
            if (popup != null)
            {
                string message = ReadPopupMessage(popup);
                if (!string.IsNullOrEmpty(message))
                {
                    // Confirm: GetSelectOnActivate() first, then _confirmButton via Reflection
                    string confirmLabel = null;
                    Selectable confirmButton = popup.GetSelectOnActivate();
                    if (confirmButton != null)
                        confirmLabel = GetBaseLabel(confirmButton.gameObject);
                    if (string.IsNullOrEmpty(confirmLabel))
                        confirmLabel = ReadPopupButtonLabel(popup, "_confirmButton");

                    string cancelLabel = ReadPopupButtonLabel(popup, "_cancelButton");

                    string confirmKey = ReadPopupKeyLabel(popup, "_okCommand");
                    string cancelKey  = ReadPopupKeyLabel(popup, "_cancelCommand");

                    string full = message + ".";
                    if (!string.IsNullOrEmpty(confirmLabel))
                    {
                        full += " " + confirmLabel;
                        if (!string.IsNullOrEmpty(confirmKey))
                            full += " (" + confirmKey + ")";
                    }
                    if (!string.IsNullOrEmpty(cancelLabel))
                    {
                        full += " / " + cancelLabel;
                        if (!string.IsNullOrEmpty(cancelKey))
                            full += " (" + cancelKey + ")";
                    }

                    DebugLogger.Log(LogCategory.State, "MenuHandler", $"Popup: {full}");
                    ScreenReader.Say(full);
                    _suppressNextFocus = true;
                }
            }
        }

        private void OnMenuPop(Menu menu)
        {
            DebugLogger.LogState($"MenuHandler: menu fermé — {menu.gameObject.name}");
            _lastSelected      = null;
            _suppressNextFocus = false;
        }

        /// <summary>
        /// Reads the label of a popup button field (_confirmButton / _cancelButton) via Reflection.
        /// Returns null if the field is absent, null, or its GameObject is inactive.
        /// </summary>
        private string ReadPopupButtonLabel(PopupMenu popup, string fieldName)
        {
            try
            {
                FieldInfo field = typeof(PopupMenu).GetField(
                    fieldName,
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Component btn = field?.GetValue(popup) as Component;
                if (btn != null && btn.gameObject.activeInHierarchy)
                    return GetBaseLabel(btn.gameObject);
                return null;
            }
            catch (Exception ex)
            {
                DebugLogger.Log(LogCategory.State, "MenuHandler", $"ReadPopupButtonLabel({fieldName}) échoué: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Returns the localized action name for a popup command field (_okCommand / _cancelCommand).
        /// Uses InputTransitionUtil.GetLabel() — returns null on failure.
        /// </summary>
        private string ReadPopupKeyLabel(PopupMenu popup, string fieldName)
        {
            try
            {
                FieldInfo field = typeof(PopupMenu).GetField(
                    fieldName,
                    BindingFlags.NonPublic | BindingFlags.Instance);
                IInputCommands cmd = field?.GetValue(popup) as IInputCommands;
                return InputHelper.GetCommandLabel(cmd);
            }
            catch (Exception ex)
            {
                DebugLogger.Log(LogCategory.State, "MenuHandler", $"ReadPopupKeyLabel({fieldName}) échoué: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Reads the protected _labelText field of a PopupMenu via Reflection.
        /// Normalizes whitespace so newlines in the UI text don't break TTS.
        /// </summary>
        private string ReadPopupMessage(PopupMenu popup)
        {
            try
            {
                FieldInfo field = typeof(PopupMenu).GetField(
                    "_labelText",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Text labelText = field?.GetValue(popup) as Text;
                return TextUtils.NormalizeWhitespace(labelText?.text);
            }
            catch (Exception ex)
            {
                DebugLogger.Log(LogCategory.State, "MenuHandler", $"ReadPopupMessage échoué: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Announces a menu focus text. Always interrupts (Say) except when
        /// _queueNextMenuSay is set (tab name → first item on menu open).
        /// </summary>
        /// <summary>
        /// Announces a menu focus text. Always interrupts (Say) except when
        /// _queueNextMenuSay is set (tab name → first item on menu open).
        /// </summary>
        private void SayMenuText(string text)
        {
            if (_queueNextMenuSay)
            {
                _queueNextMenuSay = false;
                ScreenReader.SayQueued(text);
            }
            else
            {
                ScreenReader.Say(text);
            }
        }

        /// <summary>
        /// Returns "Label, état" for options, or just "Label" for buttons.
        /// </summary>
        private string GetLabelWithState(GameObject go)
        {
            string label = GetBaseLabel(go);
            if (string.IsNullOrEmpty(label)) return null;

            string value = GetCurrentValue(go);
            if (!string.IsNullOrEmpty(value))
                return label + ", " + value;

            return label;
        }

        /// <summary>
        /// Returns the current value string for value-type options, or null for plain buttons.
        /// </summary>
        private string GetCurrentValue(GameObject go)
        {
            // ToggleElement — Activé / Désactivé
            var toggle = go.GetComponent<ToggleElement>();
            if (toggle != null)
                return toggle.GetValueAsBool() ? Loc.Get("toggle_on") : Loc.Get("toggle_off");

            // OptionsSelectorElement — option texte actuelle
            var selector = go.GetComponent<OptionsSelectorElement>();
            if (selector != null)
                return selector.GetSelectedOption();

            // SliderElement — valeur numérique
            var slider = go.GetComponent<SliderElement>();
            if (slider != null)
                return Loc.Get("slider_value", slider.GetValue());

            // KeyRebindingElement — touche assignée (affichée en image en jeu, lue via Reflection)
            var rebinding = go.GetComponent<KeyRebindingElement>();
            if (rebinding != null)
            {
                try
                {
                    var field = typeof(KeyRebindingElement).GetField(
                        "_rebindingInputCommand",
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);
                    DebugLogger.LogState($"[MenuHandler] KeyRebinding field: {(field == null ? "NULL" : "found")}");
                    object cmd = field?.GetValue(rebinding);
                    DebugLogger.LogState($"[MenuHandler] KeyRebinding cmd: {(cmd == null ? "NULL" : cmd.GetType().Name)}");
                    if (cmd != null)
                    {
                        bool gamepad = OWInput.UsingGamepad();
                        // Try (bool) first — IInputCommands.GetUITextures(bool gamepad)
                        // Try (bool, bool) as fallback — IRebindableInputAction.GetUITextures(bool, bool)
                        var flags = System.Reflection.BindingFlags.Public |
                                    System.Reflection.BindingFlags.NonPublic |
                                    System.Reflection.BindingFlags.Instance |
                                    System.Reflection.BindingFlags.FlattenHierarchy;
                        var method = cmd.GetType().GetMethod("GetUITextures", flags, null,
                                        new System.Type[] { typeof(bool) }, null)
                                  ?? cmd.GetType().GetMethod("GetUITextures", flags, null,
                                        new System.Type[] { typeof(bool), typeof(bool) }, null)
                                  ?? cmd.GetType().GetMethod("GetUITextures", flags, null,
                                        new System.Type[] { typeof(bool), typeof(bool), typeof(bool) }, null);
                        DebugLogger.LogState($"[MenuHandler] KeyRebinding method: {(method == null ? "NULL" : $"found ({method.GetParameters().Length}p)")}");
                        var invokeArgs = method?.GetParameters().Length == 1
                                        ? new object[] { gamepad }
                                        : method?.GetParameters().Length == 2
                                        ? new object[] { gamepad, false }
                                        : new object[] { gamepad, false, false };
                        var textures  = method?.Invoke(cmd, invokeArgs)
                                        as System.Collections.Generic.List<UnityEngine.Texture2D>;
                        DebugLogger.LogState($"[MenuHandler] KeyRebinding textures: {(textures == null ? "NULL" : textures.Count.ToString())}");
                        if (textures != null && textures.Count > 0)
                        {
                            var parts = new System.Collections.Generic.List<string>(textures.Count);
                            foreach (var tex in textures)
                            {
                                if (tex == null) continue;
                                string label = InputHelper.GetPhysicalButtonName(tex.name);
                                DebugLogger.LogState($"[MenuHandler] KeyRebinding tex={tex.name} label={label ?? "NULL"}");
                                if (!string.IsNullOrEmpty(label)) parts.Add(label);
                            }
                            if (parts.Count > 0)
                            {
                                string result = string.Join(" + ", parts);
                                DebugLogger.LogState($"[MenuHandler] KeyRebinding result: {result}");
                                return result;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.LogState($"[MenuHandler] KeyRebinding read failed: {ex.Message}");
                }
            }

            return null;
        }

        /// <summary>
        /// Reads the raw label text from a focused element.
        /// Strategy: MenuOption._label → child Text → child TextMeshProUGUI.
        /// </summary>
        private string GetBaseLabel(GameObject go)
        {
            // MenuOption has a dedicated _label field
            var menuOption = go.GetComponent<MenuOption>();
            if (menuOption != null)
            {
                Text labelField = menuOption.GetLabelField();
                if (labelField != null && !string.IsNullOrEmpty(labelField.text))
                    return labelField.text;
            }

            // Fallback: first Text in the hierarchy (title screen buttons)
            Text text = go.GetComponentInChildren<Text>();
            if (text != null && !string.IsNullOrEmpty(text.text))
                return text.text;

            // Fallback: TextMeshPro (OWML menus and some game UI) — strip rich-text tags
            TextMeshProUGUI tmp = go.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null && !string.IsNullOrEmpty(tmp.text))
                return TextUtils.CleanText(tmp.text);

            return null;
        }
    }
}
