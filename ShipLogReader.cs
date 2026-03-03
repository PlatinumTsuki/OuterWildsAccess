using System.Collections.Generic;

namespace OuterWildsAccess
{
    /// <summary>
    /// Accessible ship log reader usable anywhere (no need to be at the ship computer).
    ///
    /// Three-level navigation:
    ///   Level 1: Planets — celestial bodies with discovered entries
    ///   Level 2: Entries — entries for the selected planet
    ///   Level 3: Facts  — facts for the selected entry
    ///
    /// Key bindings:
    ///   F4              — open / close the reader
    ///   PageDown / PageUp — navigate within current level
    ///   Enter           — drill down (planet → entries → facts)
    ///   Backspace       — go back one level (facts → entries → planets → close)
    ///
    /// All other mod hotkeys are blocked while the reader is open.

    /// </summary>
    public class ShipLogReader
    {
        #region Inner types

        private enum Level { Closed, Planets, Entries, Facts }

        private class PlanetData
        {
            public readonly string AstroObjectID;
            public readonly string DisplayName;
            public readonly List<ShipLogEntry> Entries;

            public PlanetData(string id, string name, List<ShipLogEntry> entries)
            {
                AstroObjectID = id;
                DisplayName   = name;
                Entries       = entries;
            }
        }

        #endregion

        #region State

        private Level _level = Level.Closed;
        private List<PlanetData> _planets;
        private List<ShipLogEntry> _currentEntries;
        private List<ShipLogFact> _currentFacts;
        private int _planetIndex;
        private int _entryIndex;
        private int _factIndex;

        /// <summary>True while the ship log reader is open.</summary>
        public bool IsOpen => _level != Level.Closed;

        #endregion

        #region Lifecycle

        /// <summary>Initializes the reader. Call from Main.Start().</summary>
        public void Initialize() { }

        /// <summary>Cleans up resources. Call from Main.OnDestroy().</summary>
        public void Cleanup() { }

        #endregion

        #region Public API

        /// <summary>
        /// Handles F4 press: opens the reader if closed, closes if open.
        /// </summary>
        public void OnF4Pressed()
        {


            if (_level == Level.Closed)
                Open();
            else
                Close();
        }

        /// <summary>
        /// Handles Backspace press: goes back one level, or closes if at planet level.
        /// </summary>
        public void OnF6Pressed()
        {
            if (!IsOpen) return;

            switch (_level)
            {
                case Level.Facts:
                    _level = Level.Entries;
                    ScreenReader.Say(Loc.Get("logreader_back_entries",
                        _planets[_planetIndex].DisplayName));
                    AnnounceCurrentItem();
                    break;

                case Level.Entries:
                    _level = Level.Planets;
                    ScreenReader.Say(Loc.Get("logreader_back_planets"));
                    AnnounceCurrentItem();
                    break;

                case Level.Planets:
                    Close();
                    break;
            }
        }

        /// <summary>Moves to the next item in the current level (wraps).</summary>
        public void CycleNext()
        {
            if (!IsOpen) return;

            switch (_level)
            {
                case Level.Planets:
                    if (_planets.Count == 0) return;
                    _planetIndex = (_planetIndex + 1) % _planets.Count;
                    break;
                case Level.Entries:
                    if (_currentEntries == null || _currentEntries.Count == 0) return;
                    _entryIndex = (_entryIndex + 1) % _currentEntries.Count;
                    break;
                case Level.Facts:
                    if (_currentFacts == null || _currentFacts.Count == 0) return;
                    _factIndex = (_factIndex + 1) % _currentFacts.Count;
                    break;
            }

            AnnounceCurrentItem();
        }

        /// <summary>Moves to the previous item in the current level (wraps).</summary>
        public void CyclePrev()
        {
            if (!IsOpen) return;

            switch (_level)
            {
                case Level.Planets:
                    if (_planets.Count == 0) return;
                    _planetIndex = (_planetIndex - 1 + _planets.Count) % _planets.Count;
                    break;
                case Level.Entries:
                    if (_currentEntries == null || _currentEntries.Count == 0) return;
                    _entryIndex = (_entryIndex - 1 + _currentEntries.Count) % _currentEntries.Count;
                    break;
                case Level.Facts:
                    if (_currentFacts == null || _currentFacts.Count == 0) return;
                    _factIndex = (_factIndex - 1 + _currentFacts.Count) % _currentFacts.Count;
                    break;
            }

            AnnounceCurrentItem();
        }

        /// <summary>
        /// Handles Enter press: drills down into the selected item.
        /// Planet → Entries, Entry → Facts.
        /// </summary>
        public void DrillDown()
        {
            if (!IsOpen) return;

            switch (_level)
            {
                case Level.Planets:
                    if (_planets.Count == 0) return;
                    _currentEntries = _planets[_planetIndex].Entries;
                    _entryIndex = 0;
                    _level = Level.Entries;
                    AnnounceCurrentItem();
                    break;

                case Level.Entries:
                    if (_currentEntries == null || _currentEntries.Count == 0) return;
                    ShipLogEntry entry = _currentEntries[_entryIndex];
                    _currentFacts = entry.GetFactsForDisplay();
                    entry.MarkAsRead();
                    _factIndex = 0;
                    if (_currentFacts.Count == 0)
                    {
                        ScreenReader.Say(Loc.Get("logreader_no_facts"));
                        return;
                    }
                    _level = Level.Facts;
                    AnnounceCurrentItem();
                    break;

                case Level.Facts:
                    // Deepest level — nothing to drill into
                    break;
            }
        }

        #endregion

        #region Private helpers

        private void Open()
        {
            ShipLogManager manager = Locator.GetShipLogManager();
            if (manager == null)
            {
                ScreenReader.Say(Loc.Get("logreader_unavailable"));
                return;
            }

            BuildPlanetList(manager);

            if (_planets.Count == 0)
            {
                ScreenReader.Say(Loc.Get("logreader_no_entries"));
                return;
            }

            _level = Level.Planets;
            _planetIndex = 0;

            string firstPlanet = Loc.Get("logreader_planet",
                _planets[0].DisplayName, _planets[0].Entries.Count);
            ScreenReader.Say(Loc.Get("logreader_open", _planets.Count) + " " + firstPlanet);
            DebugLogger.LogInput("F4", "ShipLogReader opened");
        }

        private void Close()
        {
            _level = Level.Closed;
            _planets = null;
            _currentEntries = null;
            _currentFacts = null;
            ScreenReader.Say(Loc.Get("logreader_closed"));
            DebugLogger.LogInput("F4", "ShipLogReader closed");
        }

        private void BuildPlanetList(ShipLogManager manager)
        {
            List<ShipLogEntry> allEntries = manager.GetEntryList();
            var groups = new Dictionary<string, List<ShipLogEntry>>();

            foreach (ShipLogEntry entry in allEntries)
            {
                if (entry.GetState() == ShipLogEntry.State.Hidden) continue;

                string aoID = entry.GetAstroObjectID();
                if (string.IsNullOrEmpty(aoID)) continue;

                if (!groups.ContainsKey(aoID))
                    groups[aoID] = new List<ShipLogEntry>();

                groups[aoID].Add(entry);
            }

            _planets = new List<PlanetData>();
            foreach (var kvp in groups)
            {
                string displayName = GetPlanetName(kvp.Key);
                if (string.IsNullOrEmpty(displayName) || displayName == "Location Unknown")
                {
                    // Fallback: use the raw ID formatted as readable text
                    displayName = kvp.Key.Replace("_", " ");
                }
                _planets.Add(new PlanetData(kvp.Key, displayName, kvp.Value));
            }

            _planets.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName,
                System.StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Converts an astro object string ID to a localized display name
        /// using the game's built-in conversion methods.
        /// </summary>
        private static string GetPlanetName(string astroObjectID)
        {
            AstroObject.Name name = AstroObject.StringIDToAstroObjectName(astroObjectID);
            if (name == AstroObject.Name.None)
                return null;

            return AstroObject.AstroObjectNameToString(name);
        }

        private void AnnounceCurrentItem()
        {
            switch (_level)
            {
                case Level.Planets:
                    if (_planets.Count == 0) return;
                    PlanetData planet = _planets[_planetIndex];
                    ScreenReader.Say(Loc.Get("logreader_planet",
                        planet.DisplayName, planet.Entries.Count));
                    break;

                case Level.Entries:
                    if (_currentEntries == null || _currentEntries.Count == 0) return;
                    ShipLogEntry entry = _currentEntries[_entryIndex];
                    string entryName = entry.GetName(false);
                    string state = entry.GetState() == ShipLogEntry.State.Explored
                        ? Loc.Get("shiplog_explored")
                        : Loc.Get("shiplog_rumored");
                    int factCount = entry.GetFactsForDisplay().Count;
                    ScreenReader.Say(Loc.Get("logreader_entry", entryName, state, factCount));
                    break;

                case Level.Facts:
                    if (_currentFacts == null || _currentFacts.Count == 0) return;
                    ShipLogFact fact = _currentFacts[_factIndex];
                    string factText = fact.GetText();
                    if (string.IsNullOrEmpty(factText))
                        factText = "...";
                    ScreenReader.Say(factText);
                    break;
            }
        }

        #endregion
    }
}
