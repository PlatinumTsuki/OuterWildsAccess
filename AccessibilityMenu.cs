using System;
using UnityEngine.InputSystem;

namespace OuterWildsAccess
{
    /// <summary>
    /// In-game accessibility settings menu.
    ///
    /// Key bindings (while menu is open):
    ///   F6          — close the menu and save
    ///   Page Down / Arrow Down — move to next item
    ///   Page Up   / Arrow Up   — move to previous item
    ///   Enter       — toggle current item on/off
    ///
    /// When closed, F6 opens the menu and announces the header + first item.
    /// All other mod hotkeys are blocked while the menu is open.
    /// </summary>
    public class AccessibilityMenu
    {
        #region Inner type

        private class MenuItem
        {
            public readonly string       LocKey;
            public readonly Func<bool>   Get;
            public readonly Action<bool> Set;
            public readonly bool         IsCheat;

            public MenuItem(string locKey, Func<bool> get, Action<bool> set, bool isCheat = false)
            {
                LocKey  = locKey;
                Get     = get;
                Set     = set;
                IsCheat = isCheat;
            }
        }

        #endregion

        #region State

        private const string CheatCode = "ADVHikari";

        /// <summary>
        /// When true, cheat items are hidden until the code is entered.
        /// Set to true in a future release when pathfinding is more reliable.
        /// </summary>
        private const bool CheatSystemActive = false;

        private readonly MenuItem[] _items;
        private int  _currentIndex;
        private bool _isOpen;
        private bool _cheatsUnlocked;
        private string _cheatBuffer = "";

        /// <summary>True while the settings menu is open.</summary>
        public bool IsOpen => _isOpen;

        #endregion

        #region Constructor

        public AccessibilityMenu()
        {
            _items = new MenuItem[]
            {
                new MenuItem("menu_item_beacon",
                    () => ModSettings.BeaconEnabled,     v => ModSettings.BeaconEnabled     = v),
                new MenuItem("menu_item_navigation",
                    () => ModSettings.NavigationEnabled, v => ModSettings.NavigationEnabled = v),
                new MenuItem("menu_item_collision",
                    () => ModSettings.CollisionEnabled,  v => ModSettings.CollisionEnabled  = v),
                new MenuItem("menu_item_autowalk",
                    () => ModSettings.AutoWalkEnabled,   v => ModSettings.AutoWalkEnabled   = v),
                new MenuItem("menu_item_proximity",
                    () => ModSettings.ProximityEnabled,     v => ModSettings.ProximityEnabled     = v),
                new MenuItem("menu_item_gauge",
                    () => ModSettings.GaugeEnabled,         v => ModSettings.GaugeEnabled         = v),
                new MenuItem("menu_item_guidance",
                    () => ModSettings.GuidanceEnabled,       v => ModSettings.GuidanceEnabled       = v),
                new MenuItem("menu_item_meditation",
                    () => ModSettings.MeditationEnabled,     v => ModSettings.MeditationEnabled     = v, isCheat: true),
                new MenuItem("menu_item_ghostmatterprotection",
                    () => ModSettings.GhostMatterProtectionEnabled, v => ModSettings.GhostMatterProtectionEnabled = v, isCheat: true),
                new MenuItem("menu_item_shiprecall",
                    () => ModSettings.ShipRecallEnabled,      v => ModSettings.ShipRecallEnabled      = v),
                new MenuItem("menu_item_autopilot",
                    () => ModSettings.AutopilotEnabled,       v => ModSettings.AutopilotEnabled       = v),
                new MenuItem("menu_item_peacefulghosts",
                    () => ModSettings.PeacefulGhostsEnabled,  v => ModSettings.PeacefulGhostsEnabled  = v, isCheat: true),
                new MenuItem("menu_item_nvdadirect",
                    () => ModSettings.NvdaDirectEnabled,       v => ModSettings.NvdaDirectEnabled       = v),
            };
        }

        #endregion

        #region Lifecycle

        /// <summary>Initializes the menu. Call from Main.Start().</summary>
        public void Initialize() { }

        #endregion

        #region Public API

        /// <summary>
        /// Opens the menu and announces the header followed by the first item.
        /// Resets focus to the first item each time.
        /// </summary>
        public void Open()
        {
            _isOpen       = true;
            _currentIndex = FindNextVisible(-1, forward: true);
            _cheatBuffer  = "";

            if (Keyboard.current != null)
                Keyboard.current.onTextInput += OnTextInput;

            ScreenReader.Say(Loc.Get("menu_open") + " " + BuildItemAnnouncement(_currentIndex));
            DebugLogger.LogInput("F6", "Menu opened");
        }

        /// <summary>Saves settings and closes the menu.</summary>
        public void Close()
        {
            if (Keyboard.current != null)
                Keyboard.current.onTextInput -= OnTextInput;

            Save();
            _isOpen = false;
            ScreenReader.Say(Loc.Get("menu_closed"));
            DebugLogger.LogInput("F6", "Menu closed");
        }

        /// <summary>Moves focus to the next visible item (wraps around).</summary>
        public void NavigateNext()
        {
            if (!_isOpen) return;
            _currentIndex = FindNextVisible(_currentIndex, forward: true);
            ScreenReader.Say(BuildItemAnnouncement(_currentIndex));
            DebugLogger.LogInput("PageDown/ArrowDown", $"Menu → item {_currentIndex}");
        }

        /// <summary>Moves focus to the previous visible item (wraps around).</summary>
        public void NavigatePrev()
        {
            if (!_isOpen) return;
            _currentIndex = FindNextVisible(_currentIndex, forward: false);
            ScreenReader.Say(BuildItemAnnouncement(_currentIndex));
            DebugLogger.LogInput("PageUp/ArrowUp", $"Menu → item {_currentIndex}");
        }

        /// <summary>
        /// Toggles the currently focused menu item and announces the new state.
        /// No-op if the menu is closed.
        /// </summary>
        public void ToggleCurrent()
        {
            if (!_isOpen) return;

            MenuItem item   = _items[_currentIndex];
            bool     newVal = !item.Get();
            item.Set(newVal);

            ScreenReader.Say(BuildItemAnnouncement(_currentIndex));
            DebugLogger.LogInput("Enter", $"Menu toggle [{item.LocKey}] → {newVal}");
        }

        /// <summary>Persists current settings to disk via ModSettings.</summary>
        public void Save()
        {
            ModSettings.Save();
            DebugLogger.LogState("[AccessibilityMenu] Settings saved.");
        }

        #endregion

        #region Private helpers

        private string BuildItemAnnouncement(int index)
        {
            MenuItem item   = _items[index];
            string   label  = Loc.Get(item.LocKey);
            string   status = item.Get() ? Loc.Get("toggle_on") : Loc.Get("toggle_off");
            return Loc.Get("menu_item_status", label, status);
        }

        /// <summary>Returns true if the item at the given index is currently visible.</summary>
        private bool IsVisible(int index)
        {
            if (!CheatSystemActive) return true;
            return !_items[index].IsCheat || _cheatsUnlocked;
        }

        /// <summary>
        /// Finds the next visible item index starting from current,
        /// stepping forward or backward with wrap-around.
        /// </summary>
        private int FindNextVisible(int current, bool forward)
        {
            int dir  = forward ? 1 : -1;
            int next = current;
            for (int i = 0; i < _items.Length; i++)
            {
                next = (next + dir + _items.Length) % _items.Length;
                if (IsVisible(next)) return next;
            }
            return current; // fallback (shouldn't happen)
        }

        /// <summary>
        /// Callback for keyboard text input. Builds the cheat buffer
        /// and checks for the unlock code.
        /// </summary>
        private void OnTextInput(char c)
        {
            if (_cheatsUnlocked) return;

            _cheatBuffer += c;
            if (_cheatBuffer.Length > CheatCode.Length)
                _cheatBuffer = _cheatBuffer.Substring(_cheatBuffer.Length - CheatCode.Length);

            if (_cheatBuffer == CheatCode)
            {
                _cheatsUnlocked = true;
                _cheatBuffer    = "";
                ScreenReader.Say(Loc.Get("cheats_unlocked"));
                DebugLogger.LogState("[AccessibilityMenu] Cheat code entered — advanced options unlocked.");
            }
        }

        #endregion
    }
}
