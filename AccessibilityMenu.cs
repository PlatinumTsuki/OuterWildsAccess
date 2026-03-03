using System;

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

            public MenuItem(string locKey, Func<bool> get, Action<bool> set)
            {
                LocKey = locKey;
                Get    = get;
                Set    = set;
            }
        }

        #endregion

        #region State

        private readonly MenuItem[] _items;
        private int  _currentIndex;
        private bool _isOpen;

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
                    () => ModSettings.MeditationEnabled,     v => ModSettings.MeditationEnabled     = v),
                new MenuItem("menu_item_ghostmatterprotection",
                    () => ModSettings.GhostMatterProtectionEnabled, v => ModSettings.GhostMatterProtectionEnabled = v),
                new MenuItem("menu_item_shiprecall",
                    () => ModSettings.ShipRecallEnabled,      v => ModSettings.ShipRecallEnabled      = v),
                new MenuItem("menu_item_autopilot",
                    () => ModSettings.AutopilotEnabled,       v => ModSettings.AutopilotEnabled       = v),
                new MenuItem("menu_item_peacefulghosts",
                    () => ModSettings.PeacefulGhostsEnabled,  v => ModSettings.PeacefulGhostsEnabled  = v),
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
            _currentIndex = 0;
            ScreenReader.Say(Loc.Get("menu_open") + " " + BuildItemAnnouncement(0));
            DebugLogger.LogInput("F6", "Menu opened");
        }

        /// <summary>Saves settings and closes the menu.</summary>
        public void Close()
        {
            Save();
            _isOpen = false;
            ScreenReader.Say(Loc.Get("menu_closed"));
            DebugLogger.LogInput("F6", "Menu closed");
        }

        /// <summary>Moves focus to the next item (wraps around to first).</summary>
        public void NavigateNext()
        {
            if (!_isOpen) return;
            _currentIndex = (_currentIndex + 1) % _items.Length;
            ScreenReader.Say(BuildItemAnnouncement(_currentIndex));
            DebugLogger.LogInput("PageDown/ArrowDown", $"Menu → item {_currentIndex}");
        }

        /// <summary>Moves focus to the previous item (wraps around to last).</summary>
        public void NavigatePrev()
        {
            if (!_isOpen) return;
            _currentIndex = (_currentIndex - 1 + _items.Length) % _items.Length;
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

        #endregion
    }
}
