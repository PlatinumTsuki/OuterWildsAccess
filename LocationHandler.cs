using UnityEngine;
using OWML.Common;

namespace OuterWildsAccess
{
    /// <summary>
    /// Announces the player's current planet/zone when entering a new sector.
    /// On-demand (L): announces planet + subsector + nearest named location.
    ///
    /// Uses PlayerSectorDetector.OnEnterSector / OnExitSector events.
    /// Only tracks root sectors (planets/major zones) for auto-announce — ignores subsectors.
    /// </summary>
    public class LocationHandler
    {
        #region Constants

        /// <summary>Max distance to search for a named ShipLogEntryLocation.</summary>
        private const float NearbyLocationRadius = 150f;

        /// <summary>Suffixes stripped from sector gameObject names to get readable names.</summary>
        private static readonly string[] SectorSuffixes = { "_Sector", "_Body", " Sector" };

        #endregion

        #region Fields

        private SectorDetector _sectorDetector;
        private Sector.Name _currentRootSector = Sector.Name.Unnamed;

        #endregion

        #region Lifecycle

        /// <summary>
        /// Subscribes to scene load events. Call from Main.Start().
        /// </summary>
        public void Initialize()
        {
            LoadManager.OnCompleteSceneLoad += OnSceneLoaded;
        }

        /// <summary>
        /// Unsubscribes from all events. Call from Main.OnDestroy().
        /// </summary>
        public void Cleanup()
        {
            UnsubscribeFromDetector();
            LoadManager.OnCompleteSceneLoad -= OnSceneLoaded;
        }

        #endregion

        #region Scene Loading

        /// <summary>
        /// Called on scene change. Re-subscribes to the sector detector for gameplay scenes.
        /// </summary>
        public void OnSceneLoaded(OWScene oldScene, OWScene newScene)
        {
            UnsubscribeFromDetector();
            _currentRootSector = Sector.Name.Unnamed;

            if (newScene != OWScene.SolarSystem && newScene != OWScene.EyeOfTheUniverse)
                return;

            PlayerSectorDetector detector = Locator.GetPlayerSectorDetector();
            if (detector == null)
            {
                DebugLogger.LogState("[LocationHandler] PlayerSectorDetector not found after scene load.");
                return;
            }

            _sectorDetector = detector;
            _sectorDetector.OnEnterSector += OnPlayerEnteredSector;
            _sectorDetector.OnExitSector  += OnPlayerExitedSector;
            DebugLogger.LogState("[LocationHandler] Subscribed to PlayerSectorDetector.");
        }

        private void UnsubscribeFromDetector()
        {
            if (_sectorDetector == null) return;
            _sectorDetector.OnEnterSector -= OnPlayerEnteredSector;
            _sectorDetector.OnExitSector  -= OnPlayerExitedSector;
            _sectorDetector = null;
        }

        #endregion

        #region Sector Events

        private void OnPlayerEnteredSector(Sector sector)
        {
            if (sector == null) return;

            Sector root = sector.IsRootSector() ? sector : sector.GetRootSector();
            if (root == null) return;

            Sector.Name rootName = root.GetName();

            // Skip sectors that are uninteresting or handled by other handlers
            if (rootName == Sector.Name.Unnamed) return;
            if (rootName == Sector.Name.Ship)    return; // StateHandler handles ship entry/exit

            // Only announce when the root location actually changes
            if (rootName == _currentRootSector) return;

            _currentRootSector = rootName;



            string locationName = GetLocationName(rootName);
            if (string.IsNullOrEmpty(locationName)) return;

            ScreenReader.Say(Loc.Get("location_enter", locationName));
            DebugLogger.LogState($"[LocationHandler] Root sector: {rootName}");
        }

        private void OnPlayerExitedSector(Sector sector)
        {
            // Only reset tracking when leaving a root sector
            if (sector == null || !sector.IsRootSector()) return;
            if (sector.GetName() == _currentRootSector)
                _currentRootSector = Sector.Name.Unnamed;
        }

        #endregion

        #region On-Demand (L)

        /// <summary>
        /// Announces the player's current location on demand.
        /// Format: "Planet, Subsector, near Named Location" (each part optional).
        /// </summary>
        public void AnnounceCurrentLocation()
        {
            PlayerSectorDetector detector = Locator.GetPlayerSectorDetector();
            if (detector == null)
            {
                ScreenReader.Say(Loc.Get("location_unknown"));
                return;
            }

            // Get deepest sector (no ignore list = most specific)
            Sector deepest = detector.GetLastEnteredSector();

            // Get root sector (with ignore list for planet name)
            Sector.Name[] ignoreList = new[]
            {
                Sector.Name.Unnamed,
                Sector.Name.Ship,
                Sector.Name.Sun,
                Sector.Name.HourglassTwins,
            };
            Sector rootCandidate = detector.GetLastEnteredSector(ignoreList);

            if (rootCandidate == null && deepest == null)
            {
                ScreenReader.Say(Loc.Get("location_space"));
                return;
            }

            var sb = new System.Text.StringBuilder();

            // ── Level 1: Planet (root sector) ──
            Sector root = null;
            if (rootCandidate != null)
            {
                root = rootCandidate.IsRootSector() ? rootCandidate : rootCandidate.GetRootSector();
            }
            Sector.Name rootName = (root != null) ? root.GetName() : Sector.Name.Unnamed;
            string planetName = GetLocationName(rootName);

            if (!string.IsNullOrEmpty(planetName))
                sb.Append(planetName);

            // ── Level 2: Deepest subsector (if different from root) ──
            if (deepest != null && root != null && deepest != root)
            {
                string subName = CleanSectorName(deepest.gameObject.name);
                // Avoid repeating the planet name
                if (!string.IsNullOrEmpty(subName) && subName != planetName)
                {
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(subName);
                }
            }

            // ── Level 3: Nearest ShipLogEntryLocation ──
            string nearbyName = FindNearestLocationName();
            if (!string.IsNullOrEmpty(nearbyName))
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(Loc.Get("location_near", nearbyName));
            }

            // Final announcement
            if (sb.Length == 0)
                ScreenReader.Say(Loc.Get("location_space"));
            else
                ScreenReader.Say(Loc.Get("location_current", sb.ToString()));

            DebugLogger.Log(LogCategory.State, "LocationHandler", $"Location: {sb}");
        }

        #endregion

        #region Nearby Location Search

        /// <summary>
        /// Finds the nearest ShipLogEntryLocation within NearbyLocationRadius.
        /// Returns its localized name or null.
        /// </summary>
        private string FindNearestLocationName()
        {
            try
            {
                Transform playerTr = Locator.GetPlayerTransform();
                if (playerTr == null) return null;

                Vector3 playerPos = playerTr.position;
                ShipLogEntryLocation[] locations = Object.FindObjectsOfType<ShipLogEntryLocation>();
                if (locations == null || locations.Length == 0) return null;

                ShipLogEntryLocation closest = null;
                float closestDist = NearbyLocationRadius;

                for (int i = 0; i < locations.Length; i++)
                {
                    float dist = Vector3.Distance(playerPos, locations[i].transform.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = locations[i];
                    }
                }

                if (closest == null) return null;

                string name = closest.GetEntryName(false);
                return string.IsNullOrEmpty(name) ? null : name;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Sector Name Cleaning

        /// <summary>
        /// Strips common suffixes like "_Sector" from a sector gameObject name
        /// and replaces underscores with spaces for readability.
        /// </summary>
        private static string CleanSectorName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return null;

            string cleaned = rawName;
            foreach (string suffix in SectorSuffixes)
            {
                if (cleaned.EndsWith(suffix))
                {
                    cleaned = cleaned.Substring(0, cleaned.Length - suffix.Length);
                    break;
                }
            }

            // Replace underscores with spaces
            cleaned = cleaned.Replace('_', ' ').Trim();

            return string.IsNullOrEmpty(cleaned) ? null : cleaned;
        }

        #endregion

        #region Location Name Mapping

        /// <summary>
        /// Maps a Sector.Name enum value to a localized French location name.
        /// Returns empty string for unmapped or uninteresting sectors.
        /// </summary>
        private static string GetLocationName(Sector.Name name)
        {
            switch (name)
            {
                case Sector.Name.Sun:                return Loc.Get("loc_sun");
                case Sector.Name.HourglassTwin_A:    return Loc.Get("loc_ash_twin");
                case Sector.Name.HourglassTwin_B:    return Loc.Get("loc_ember_twin");
                case Sector.Name.HourglassTwins:     return Loc.Get("loc_hourglass_twins");
                case Sector.Name.TimberHearth:       return Loc.Get("loc_timber_hearth");
                case Sector.Name.BrittleHollow:      return Loc.Get("loc_brittle_hollow");
                case Sector.Name.GiantsDeep:         return Loc.Get("loc_giants_deep");
                case Sector.Name.DarkBramble:        return Loc.Get("loc_dark_bramble");
                case Sector.Name.Comet:              return Loc.Get("loc_comet");
                case Sector.Name.QuantumMoon:        return Loc.Get("loc_quantum_moon");
                case Sector.Name.TimberMoon:         return Loc.Get("loc_timber_moon");
                case Sector.Name.VolcanicMoon:       return Loc.Get("loc_volcanic_moon");
                case Sector.Name.BrambleDimension:   return Loc.Get("loc_bramble_dimension");
                case Sector.Name.OrbitalProbeCannon: return Loc.Get("loc_probe_cannon");
                case Sector.Name.EyeOfTheUniverse:   return Loc.Get("loc_eye");
                case Sector.Name.SunStation:         return Loc.Get("loc_sun_station");
                case Sector.Name.WhiteHole:          return Loc.Get("loc_white_hole");
                case Sector.Name.TimeLoopDevice:     return Loc.Get("loc_time_loop_device");
                case Sector.Name.Vessel:             return Loc.Get("loc_vessel");
                case Sector.Name.VesselDimension:    return Loc.Get("loc_vessel_dimension");
                case Sector.Name.DreamWorld:         return Loc.Get("loc_dream_world");
                case Sector.Name.InvisiblePlanet:    return Loc.Get("loc_invisible_planet");
                default:                             return string.Empty;
            }
        }

        #endregion
    }
}
