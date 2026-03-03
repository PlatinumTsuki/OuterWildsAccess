using System.Collections.Generic;
using UnityEngine;

namespace OuterWildsAccess
{
    /// <summary>
    /// Scans the environment and organizes results into 6 categories:
    ///   Ship, NPCs, Interactables, Nomai Texts, Locations, Signs.
    ///
    /// Key bindings:
    ///   Début (Home)      — fresh scan of all categories
    ///   Ctrl+Page suiv.   — switch to next category
    ///   Ctrl+Page préc.   — switch to previous category
    ///   Page suiv.        — cycle to next item (auto-targets it)
    ///   Page préc.        — cycle to previous item (auto-targets it)
    ///   Fin (End)         — announce direction + distance to current target
    /// </summary>
    public class NavigationHandler
    {
        #region Inner Types

        private enum NavCategory
        {
            Ship          = 0,
            NPCs          = 1,
            Interactables = 2,
            NomaiTexts    = 3,
            Locations     = 4,
            Signs         = 5
        }

        private const int CategoryCount = 6;

        private class NavTarget
        {
            public string    Name;
            public Transform Transform;
            public bool      IsInteractable;

            public NavTarget(string name, Transform transform, bool isInteractable = false)
            {
                Name           = name;
                Transform      = transform;
                IsInteractable = isInteractable;
            }
        }

        #endregion

        #region Constants

        private const float StaleThreshold = 120f;
        private const float InteractRange  = 3f;

        // Object scan ranges and limits
        private const float NpcRadius       = 150f;
        private const float LocationRadius  = 100f;
        private const float InteractRadius  = 40f;
        private const float NomaiTextRadius = 60f;
        private const int   MaxNpcs         = 8;
        private const int   MaxLocations    = 8;
        private const int   MaxInteracts    = 5;
        private const int   MaxNomaiWall    = 5;
        private const int   MaxNomaiComp    = 3;

        // Location scan ranges and limits
        private const float LocationEntryRadius = 500f;
        private const float CampfireRadius      = 300f;
        private const int   MaxSectors          = 10;
        private const int   MaxLocationEntries  = 10;
        private const int   MaxCampfires        = 5;

        // Category localization keys (indexed by NavCategory)
        private static readonly string[] CategoryLocKeys =
        {
            "nav_cat_ship",
            "nav_cat_npcs",
            "nav_cat_interactables",
            "nav_cat_nomai",
            "nav_cat_locations",
            "nav_cat_signs"
        };

        #endregion

        #region State

        // Per-category result lists and item indices
        private readonly List<NavTarget>[] _categories = new List<NavTarget>[CategoryCount];
        private readonly int[] _catItemIdx = new int[CategoryCount];
        private int   _currentCatIdx = 0;
        private float _lastScanTime  = -999f;
        private bool  _staleWarned   = false;
        private bool  _hasScanned    = false;

        // Active navigation target (set automatically on cycle)
        private NavTarget _activeTarget;

        // True when path guidance (G key) is active — enables live tracking
        private bool _guidanceActive;

        // Live tracking — re-announces direction every LiveInterval seconds
        private const float LiveInterval = 5f;
        private float _nextLiveAnnounce = 0f;

        // Waypoint override — when set, direction points to A* waypoint instead of target
        private System.Func<Vector3?> _waypointOverride;

        // Reflection cache for Sector._subsectors
        private static System.Reflection.FieldInfo _subsectorsField;

        #endregion

        #region Lifecycle

        /// <summary>Initializes the handler. Call from Main.Start().</summary>
        public void Initialize()
        {
            for (int i = 0; i < CategoryCount; i++)
                _categories[i] = new List<NavTarget>();

            _subsectorsField = typeof(Sector).GetField(
                "_subsectors",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        }

        /// <summary>
        /// Sets a delegate that provides the current A* waypoint position.
        /// When it returns a value, direction announcements use that waypoint
        /// instead of the target position directly.
        /// </summary>
        public void SetWaypointOverride(System.Func<Vector3?> waypointFunc)
        {
            _waypointOverride = waypointFunc;
        }

        /// <summary>Cleans up resources. Call from Main.OnDestroy().</summary>
        public void Cleanup() { _activeTarget = null; }

        /// <summary>
        /// Re-announces direction to active target every LiveInterval seconds.
        /// Call from Main.Update().
        /// </summary>
        public void Update()
        {
            if (!ModSettings.NavigationEnabled) return;
            if (!_guidanceActive) return;
            if (_activeTarget == null || _activeTarget.Transform == null) return;
            if (Time.time < _nextLiveAnnounce) return;
            if (!OWInput.IsInputMode(InputMode.Character)) return;

            Transform playerTr = Locator.GetPlayerTransform();
            if (playerTr == null) return;

            float dist = Vector3.Distance(playerTr.position, _activeTarget.Transform.position);
            if (dist <= 2f) return;

            _nextLiveAnnounce = Time.time + LiveInterval;
            Vector3? wp = _waypointOverride?.Invoke();
            string dir = wp.HasValue
                ? BuildDirectionString(playerTr, wp.Value)
                : BuildDirectionString(playerTr, _activeTarget.Transform);
            ScreenReader.Say(Loc.Get("nav_live", dir, Mathf.RoundToInt(dist)));
        }

        #endregion

        #region Public Properties

        /// <summary>The Transform of the currently active navigation target, or null.</summary>
        public Transform ActiveTargetTransform => _activeTarget?.Transform;

        /// <summary>The display name of the currently active navigation target, or null.</summary>
        public string ActiveTargetName => _activeTarget?.Name;

        /// <summary>True if the active navigation target is an interactable object or NPC.</summary>
        public bool ActiveTargetIsInteractable => _activeTarget?.IsInteractable ?? false;

        /// <summary>True if a navigation target is currently anchored.</summary>
        public bool HasActiveTarget => _activeTarget != null && _activeTarget.Transform != null;

        /// <summary>
        /// Transform of the item currently highlighted in the current category, or null.
        /// Used by Main.cs for End key anchor/drop logic.
        /// </summary>
        public Transform SelectedTargetTransform
        {
            get
            {
                var list = _categories[_currentCatIdx];
                int idx  = _catItemIdx[_currentCatIdx];
                if (list.Count > 0 && idx >= 0 && idx < list.Count)
                    return list[idx].Transform;
                return null;
            }
        }

        /// <summary>
        /// Name of the item currently highlighted in the current category, or null.
        /// </summary>
        public string SelectedTargetName
        {
            get
            {
                var list = _categories[_currentCatIdx];
                int idx  = _catItemIdx[_currentCatIdx];
                if (list.Count > 0 && idx >= 0 && idx < list.Count)
                    return list[idx].Name;
                return null;
            }
        }

        /// <summary>True if the currently highlighted item is interactable (NPC or object).</summary>
        public bool SelectedTargetIsInteractable
        {
            get
            {
                var list = _categories[_currentCatIdx];
                int idx  = _catItemIdx[_currentCatIdx];
                if (list.Count > 0 && idx >= 0 && idx < list.Count)
                    return list[idx].IsInteractable;
                return false;
            }
        }

        #endregion

        #region Target Management

        /// <summary>
        /// Enables or disables live tracking (re-announce direction every 5s).
        /// Live tracking only runs while path guidance is active.
        /// </summary>
        public void SetGuidanceActive(bool active)
        {
            _guidanceActive = active;
            if (active)
                _nextLiveAnnounce = Time.time + LiveInterval;
        }

        /// <summary>
        /// Clears the active target and announces it.
        /// Stops live tracking. Beacon and auto-walk must be cleared by the caller.
        /// </summary>
        public void ClearTarget()
        {
            _activeTarget     = null;
            _guidanceActive   = false;
            _nextLiveAnnounce = 0f;
            ScreenReader.Say(Loc.Get("nav_target_cleared"));
            DebugLogger.LogState("[NavigationHandler] Target cleared.");
        }

        #endregion

        #region Scan (Home key)

        /// <summary>Performs a fresh scan of all categories and announces the first result.</summary>
        public void Scan()
        {
            if (!ModSettings.NavigationEnabled) return;
            if (PlayerState.AtFlightConsole()) return;
            if (Locator.GetPlayerTransform() == null) return;
            PerformFullScan();
        }

        private void PerformFullScan()
        {
            for (int i = 0; i < CategoryCount; i++)
            {
                _categories[i].Clear();
                _catItemIdx[i] = 0;
            }
            _lastScanTime  = Time.time;
            _staleWarned   = false;
            _hasScanned    = true;

            Transform playerTr  = Locator.GetPlayerTransform();
            Vector3   playerPos = playerTr.position;

            // ── Category 0: Ship ───────────────────────────────────────────
            Transform shipTr = Locator.GetShipTransform();
            if (shipTr != null)
                _categories[0].Add(new NavTarget(Loc.Get("nav_ship"), shipTr));

            // ── Category 1: NPCs + Category 5: Signs ─────────────────────
            ScanNpcs(playerPos);

            // ── Category 2: Interactables ─────────────────────────────────
            ScanInteractables(playerPos);

            // ── Category 3: Nomai Texts ───────────────────────────────────
            ScanNomaiTexts(playerPos);

            // ── Category 4: Locations ─────────────────────────────────────
            ScanLocations(playerPos);

            // Sort each category by distance (closest first)
            for (int i = 0; i < CategoryCount; i++)
            {
                _categories[i].Sort((a, b) =>
                {
                    float dA = Vector3.Distance(playerPos, a.Transform.position);
                    float dB = Vector3.Distance(playerPos, b.Transform.position);
                    return dA.CompareTo(dB);
                });
            }

            // Find first non-empty category
            _currentCatIdx = 0;
            for (int i = 0; i < CategoryCount; i++)
            {
                if (_categories[i].Count > 0) { _currentCatIdx = i; break; }
            }

            // Count total results
            int total = 0;
            for (int i = 0; i < CategoryCount; i++)
                total += _categories[i].Count;

            if (total == 0)
            {
                ScreenReader.Say(Loc.Get("nav_nothing_found"));
                DebugLogger.LogState("[NavigationHandler] Scan: nothing found.");
                return;
            }

            // Announce total + first item
            var   first     = _categories[_currentCatIdx][0];
            float firstDist = Vector3.Distance(playerPos, first.Transform.position);
            ScreenReader.Say(Loc.Get("nav_scan_first",
                total, first.Name, Mathf.RoundToInt(firstDist)));

            DebugLogger.LogState($"[NavigationHandler] Scan: {total} results across {CategoryCount} categories.");
        }

        private void ScanNpcs(Vector3 playerPos)
        {
            var npcs = new List<CharacterDialogueTree>(
                Object.FindObjectsOfType<CharacterDialogueTree>());
            npcs.Sort((a, b) =>
                Vector3.Distance(playerPos, a.transform.position)
                    .CompareTo(Vector3.Distance(playerPos, b.transform.position)));

            int count = 0;
            foreach (var npc in npcs)
            {
                if (count >= MaxNpcs) break;
                if (!npc.gameObject.activeInHierarchy) continue;
                if (Vector3.Distance(playerPos, npc.transform.position) > NpcRadius) continue;

                // Signs use CharacterDialogueTree but are not NPCs — separate category
                if (IsSign(npc))
                {
                    string signName = GetSignName(npc);
                    _categories[(int)NavCategory.Signs].Add(
                        new NavTarget(signName, npc.transform, isInteractable: true));
                    continue;
                }

                string npcName = GetNpcName(npc);
                if (npcName == null) continue;
                _categories[1].Add(new NavTarget(npcName, npc.transform, isInteractable: true));
                count++;
            }
        }

        private void ScanInteractables(Vector3 playerPos)
        {
            // Inside the ship (walking): scan ship-specific interactables only
            if (PlayerState.IsInsideShip() && !PlayerState.AtFlightConsole())
            {
                ScanShipInterior(playerPos);
                return;
            }

            // ── InteractReceiver (raycast-based) ──
            var recvs = new List<InteractReceiver>(
                Object.FindObjectsOfType<InteractReceiver>());
            recvs.Sort((a, b) =>
                Vector3.Distance(playerPos, a.transform.position)
                    .CompareTo(Vector3.Distance(playerPos, b.transform.position)));

            int count = 0;
            foreach (var recv in recvs)
            {
                if (count >= MaxInteracts) break;
                if (!recv.gameObject.activeInHierarchy) continue;
                if (Vector3.Distance(playerPos, recv.transform.position) > InteractRadius) continue;
                if (recv.GetComponentInParent<CharacterDialogueTree>() != null) continue;
                if (IsChildOfShip(recv.transform)) continue;
                string recvName = GetInteractName(recv);
                if (recvName == null) continue;
                _categories[2].Add(new NavTarget(recvName, recv.transform, isInteractable: true));
                count++;
            }

            // ── InteractZone (trigger volume-based: elevators, consoles, etc.) ──
            var zones = new List<InteractZone>(
                Object.FindObjectsOfType<InteractZone>());
            zones.Sort((a, b) =>
                Vector3.Distance(playerPos, a.transform.position)
                    .CompareTo(Vector3.Distance(playerPos, b.transform.position)));

            foreach (var zone in zones)
            {
                if (count >= MaxInteracts) break;
                if (!zone.gameObject.activeInHierarchy) continue;
                if (Vector3.Distance(playerPos, zone.transform.position) > InteractRadius) continue;
                if (zone.GetComponentInParent<CharacterDialogueTree>() != null) continue;
                if (IsChildOfShip(zone.transform)) continue;
                if (zone.GetComponentInParent<NomaiText>() != null) continue;
                if (zone.GetComponentInParent<RemoteFlightConsole>() != null) continue;
                string zoneName = GetInteractName(zone);
                if (zoneName == null) continue;
                _categories[2].Add(new NavTarget(zoneName, zone.transform, isInteractable: true));
                count++;
            }

            // RemoteFlightConsole (model rocket — uses InteractZone but needs a specific name)
            foreach (var console in Object.FindObjectsOfType<RemoteFlightConsole>())
            {
                if (!console.gameObject.activeInHierarchy) continue;
                if (Vector3.Distance(playerPos, console.transform.position) > InteractRadius) continue;
                _categories[2].Add(new NavTarget(Loc.Get("nav_model_rocket"), console.transform, isInteractable: true));
            }
        }

        /// <summary>
        /// Scans interactable volumes inside the ship only.
        /// Called when the player is walking inside the ship (not at flight console).
        /// </summary>
        private void ScanShipInterior(Vector3 playerPos)
        {
            Transform shipTr = Locator.GetShipTransform();
            if (shipTr == null) return;

            var volumes = shipTr.GetComponentsInChildren<SingleInteractionVolume>(false);
            foreach (var vol in volumes)
            {
                if (!vol.gameObject.activeInHierarchy) continue;
                if (Vector3.Distance(playerPos, vol.transform.position) > InteractRadius) continue;
                string name = GetInteractName(vol);
                if (string.IsNullOrEmpty(name)) continue;
                _categories[2].Add(new NavTarget(name, vol.transform, isInteractable: true));
            }
        }

        private void ScanNomaiTexts(Vector3 playerPos)
        {
            var wallTexts = new List<NomaiWallText>(
                Object.FindObjectsOfType<NomaiWallText>());
            wallTexts.Sort((a, b) =>
                Vector3.Distance(playerPos, a.transform.position)
                    .CompareTo(Vector3.Distance(playerPos, b.transform.position)));

            int wallCount = 0;
            foreach (var wt in wallTexts)
            {
                if (wallCount >= MaxNomaiWall) break;
                if (!wt.gameObject.activeInHierarchy) continue;
                if (Vector3.Distance(playerPos, wt.transform.position) > NomaiTextRadius) continue;
                string label  = Loc.Get("nav_nomai_wall");
                string sector = GetNomaiSectorContext(wt.transform);
                if (!string.IsNullOrEmpty(sector))
                    label += " (" + sector + ")";
                _categories[3].Add(new NavTarget(label, wt.transform, isInteractable: true));
                wallCount++;
            }

            var computers = new List<NomaiComputer>(
                Object.FindObjectsOfType<NomaiComputer>());
            computers.Sort((a, b) =>
                Vector3.Distance(playerPos, a.transform.position)
                    .CompareTo(Vector3.Distance(playerPos, b.transform.position)));

            int compCount = 0;
            foreach (var comp in computers)
            {
                if (compCount >= MaxNomaiComp) break;
                if (!comp.gameObject.activeInHierarchy) continue;
                if (Vector3.Distance(playerPos, comp.transform.position) > NomaiTextRadius) continue;
                string label  = Loc.Get("nav_nomai_computer");
                string sector = GetNomaiSectorContext(comp.transform);
                if (!string.IsNullOrEmpty(sector))
                    label += " (" + sector + ")";
                _categories[3].Add(new NavTarget(label, comp.transform, isInteractable: true));
                compCount++;
            }
        }

        private void ScanLocations(Vector3 playerPos)
        {
            // ShipLogEntryLocation — discovered places from the ship log map.
            // These are the real points of interest that sighted players navigate to.
            var entries = new List<ShipLogEntryLocation>(
                Object.FindObjectsOfType<ShipLogEntryLocation>());
            entries.Sort((a, b) =>
                Vector3.Distance(playerPos, a.transform.position)
                    .CompareTo(Vector3.Distance(playerPos, b.transform.position)));

            var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            int entryCount = 0;
            foreach (var entry in entries)
            {
                if (entryCount >= MaxLocationEntries) break;
                if (!entry.gameObject.activeInHierarchy) continue;
                if (Vector3.Distance(playerPos, entry.transform.position) > LocationEntryRadius) continue;
                string name = null;
                try { name = entry.GetEntryName(false); } catch { }
                if (string.IsNullOrEmpty(name)) continue;
                if (!seen.Add(name)) continue;
                _categories[4].Add(new NavTarget(name, entry.transform));
                entryCount++;
            }

            // Campfires (300m)
            var campfires = new List<Campfire>(
                Object.FindObjectsOfType<Campfire>());
            int campCount = 0;
            foreach (var fire in campfires)
            {
                if (campCount >= MaxCampfires) break;
                if (!fire.gameObject.activeInHierarchy) continue;
                if (Vector3.Distance(playerPos, fire.transform.position) > CampfireRadius) continue;
                string state = GetCampfireStateLabel(fire.GetState());
                string name  = Loc.Get("nav_campfire") + " (" + state + ")";
                _categories[4].Add(new NavTarget(name, fire.transform));
                campCount++;
            }
        }

        #endregion

        #region Category Switching (Alt+PageUp/Down)

        /// <summary>Switches to the next category and announces it.</summary>
        public void CategoryNext()
        {
            if (!ModSettings.NavigationEnabled) return;
            if (PlayerState.AtFlightConsole()) return;
            if (!_hasScanned)
            {
                ScreenReader.Say(Loc.Get("nav_no_scan"));
                return;
            }
            _currentCatIdx = (_currentCatIdx + 1) % CategoryCount;
            AnnounceCategory();
        }

        /// <summary>Switches to the previous category and announces it.</summary>
        public void CategoryPrev()
        {
            if (!ModSettings.NavigationEnabled) return;
            if (PlayerState.AtFlightConsole()) return;
            if (!_hasScanned)
            {
                ScreenReader.Say(Loc.Get("nav_no_scan"));
                return;
            }
            _currentCatIdx = ((_currentCatIdx - 1) + CategoryCount) % CategoryCount;
            AnnounceCategory();
        }

        private void AnnounceCategory()
        {
            string catName = Loc.Get(CategoryLocKeys[_currentCatIdx]);
            var    list    = _categories[_currentCatIdx];

            if (list.Count == 0)
            {
                ScreenReader.Say(Loc.Get("nav_cat_empty", catName));
                DebugLogger.LogState($"[NavigationHandler] Category: {catName} — empty.");
                return;
            }

            // Reset item index to 0 when entering a category
            _catItemIdx[_currentCatIdx] = 0;
            NavTarget first = list[0];
            float dist = Vector3.Distance(
                Locator.GetPlayerTransform().position, first.Transform.position);

            ScreenReader.Say(Loc.Get("nav_cat_announce",
                catName, list.Count, first.Name, Mathf.RoundToInt(dist)));
            DebugLogger.LogState($"[NavigationHandler] Category: {catName}, {list.Count} results.");
        }

        #endregion

        #region Item Cycling (PageUp/Down)

        /// <summary>Cycles to the next item in the current category (Page suivante).</summary>
        public void CycleNext()
        {
            if (!ModSettings.NavigationEnabled) return;
            if (PlayerState.AtFlightConsole()) return;
            if (!_hasScanned)
            {
                ScreenReader.Say(Loc.Get("nav_no_scan"));
                return;
            }
            var list = _categories[_currentCatIdx];
            if (list.Count == 0)
            {
                string catName = Loc.Get(CategoryLocKeys[_currentCatIdx]);
                ScreenReader.Say(Loc.Get("nav_cat_empty", catName));
                return;
            }
            _catItemIdx[_currentCatIdx] = (_catItemIdx[_currentCatIdx] + 1) % list.Count;
            _activeTarget = list[_catItemIdx[_currentCatIdx]];
            AnnounceCurrentItem();
        }

        /// <summary>Cycles to the previous item in the current category (Page précédente).</summary>
        public void CyclePrev()
        {
            if (!ModSettings.NavigationEnabled) return;
            if (PlayerState.AtFlightConsole()) return;
            if (!_hasScanned)
            {
                ScreenReader.Say(Loc.Get("nav_no_scan"));
                return;
            }
            var list = _categories[_currentCatIdx];
            if (list.Count == 0)
            {
                string catName = Loc.Get(CategoryLocKeys[_currentCatIdx]);
                ScreenReader.Say(Loc.Get("nav_cat_empty", catName));
                return;
            }
            _catItemIdx[_currentCatIdx] = ((_catItemIdx[_currentCatIdx] - 1) + list.Count) % list.Count;
            _activeTarget = list[_catItemIdx[_currentCatIdx]];
            AnnounceCurrentItem();
        }

        private void AnnounceCurrentItem()
        {
            var list = _categories[_currentCatIdx];
            if (list.Count == 0) return;
            int       idx  = _catItemIdx[_currentCatIdx];
            NavTarget t    = list[idx];
            float     dist = Vector3.Distance(
                Locator.GetPlayerTransform().position, t.Transform.position);

            string text = Loc.Get("nav_item",
                idx + 1, list.Count, t.Name, Mathf.RoundToInt(dist));

            // Warn once when cycling through stale results
            if (!_staleWarned && (Time.time - _lastScanTime) > StaleThreshold)
            {
                text         = Loc.Get("nav_stale") + " " + text;
                _staleWarned = true;
            }

            ScreenReader.Say(text);
        }

        #endregion

        #region Navigate (End key)

        /// <summary>
        /// Announces the direction and distance to the active navigation target.
        /// The target is already set by CycleNext/CyclePrev (auto-target on cycle).
        /// </summary>
        public void NavigateToTarget()
        {
            if (!ModSettings.NavigationEnabled) return;
            if (PlayerState.AtFlightConsole()) return;
            Transform playerTr = Locator.GetPlayerTransform();
            if (playerTr == null) return;

            if (_activeTarget == null)
            {
                ScreenReader.Say(Loc.Get("nav_no_target"));
                return;
            }

            // Guard against destroyed Unity objects
            if (_activeTarget.Transform == null)
            {
                ScreenReader.Say(Loc.Get("nav_target_lost"));
                _activeTarget = null;
                return;
            }

            Vector3? wp = _waypointOverride?.Invoke();
            string dir  = wp.HasValue
                ? BuildDirectionString(playerTr, wp.Value)
                : BuildDirectionString(playerTr, _activeTarget.Transform);
            float  dist = Vector3.Distance(playerTr.position, _activeTarget.Transform.position);
            string msg  = Loc.Get("nav_navigate", _activeTarget.Name, dir, Mathf.RoundToInt(dist));

            ScreenReader.Say(msg);
            DebugLogger.LogState($"[NavigationHandler] Navigate → {_activeTarget.Name}: {dir}, {Mathf.RoundToInt(dist)}m");
        }

        #endregion

        #region Direction Calculation

        /// <summary>Builds a cardinal direction string from the player to a target Transform.</summary>
        private string BuildDirectionString(Transform playerTr, Transform target)
        {
            return BuildDirectionString(playerTr, target.position);
        }

        /// <summary>
        /// Builds a cardinal direction string from the player to a world position.
        /// Uses the player's current facing direction: forward = "devant", right = "à droite".
        /// Each component is only included if its magnitude meets the threshold
        /// (15% of horizontal distance and at least 2m).
        /// </summary>
        private string BuildDirectionString(Transform playerTr, Vector3 targetPos)
        {
            Vector3 toTarget = targetPos - playerTr.position;
            Vector3 north = playerTr.forward;
            Vector3 east  = playerTr.right;

            float northDist = Vector3.Dot(toTarget, north);
            float eastDist  = Vector3.Dot(toTarget, east);

            float horizDist  = Mathf.Sqrt(northDist * northDist + eastDist * eastDist);
            float hThreshold = Mathf.Max(2f, horizDist * 0.15f);

            var parts = new List<string>();

            if (Mathf.Abs(northDist) >= hThreshold)
            {
                string dir = northDist > 0f ? Loc.Get("nav_north") : Loc.Get("nav_south");
                parts.Add(Mathf.RoundToInt(Mathf.Abs(northDist)) + " " + dir);
            }

            if (Mathf.Abs(eastDist) >= hThreshold)
            {
                string dir = eastDist > 0f ? Loc.Get("nav_east") : Loc.Get("nav_west");
                parts.Add(Mathf.RoundToInt(Mathf.Abs(eastDist)) + " " + dir);
            }

            return parts.Count > 0 ? string.Join(", ", parts) : Loc.Get("nav_here");
        }

        #endregion

        #region Helpers — Object Scan

        private static string GetNpcName(CharacterDialogueTree npc)
        {
            try
            {
                var field = typeof(CharacterDialogueTree).GetField(
                    "_characterName",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

                if (field != null)
                {
                    string raw = field.GetValue(npc) as string;
                    if (!string.IsNullOrEmpty(raw))
                        return TextTranslation.Translate(raw);
                }
            }
            catch { /* fall back to GameObject name */ }

            return npc.gameObject.name;
        }

        /// <summary>
        /// Returns true if this CharacterDialogueTree is a sign/plaque, not an NPC.
        /// </summary>
        private static bool IsSign(CharacterDialogueTree npc)
        {
            try
            {
                var field = typeof(CharacterDialogueTree).GetField(
                    "_characterName",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                string raw = field?.GetValue(npc) as string;
                return raw == "SIGN";
            }
            catch { return false; }
        }

        /// <summary>
        /// Returns a display name for a sign. Climbs the transform hierarchy
        /// for a meaningful name; falls back to "Panneau" if nothing better is found.
        /// </summary>
        private static string GetSignName(CharacterDialogueTree sign)
        {
            // Try current object, then parent, then grandparent
            Transform t = sign.transform;
            for (int i = 0; i < 3 && t != null; i++)
            {
                string name = t.gameObject.name;
                if (!string.IsNullOrEmpty(name)
                    && !name.StartsWith("Interact")
                    && name != "Interaction"
                    && name != "InteractVolume")
                {
                    return name.Replace("_", " ").Trim();
                }
                t = t.parent;
            }

            return Loc.Get("nav_cat_signs");
        }

        private static bool IsChildOfShip(Transform t)
        {
            Transform shipTr = Locator.GetShipTransform();
            return shipTr != null && t.IsChildOf(shipTr);
        }

        /// <summary>
        /// Returns a display name for an interactable volume, or null for
        /// objects that should be filtered out (decorative props, etc.).
        /// </summary>
        private static string GetInteractName(Component vol)
        {
            string name = vol.gameObject.name;

            if (name.StartsWith("Interact") || name == "Interaction" || name == "InteractVolume")
            {
                Transform parent = vol.transform.parent;
                if (parent != null) name = parent.gameObject.name;
            }

            return name.Replace("_", " ").Trim();
        }

        /// <summary>
        /// Gets the nearest parent sector's display name for a Nomai text object.
        /// Walks up the transform hierarchy looking for a Sector component.
        /// </summary>
        private static string GetNomaiSectorContext(Transform t)
        {
            Transform current = t.parent;
            while (current != null)
            {
                Sector s = current.GetComponent<Sector>();
                if (s != null)
                {
                    string name = GetSectorDisplayName(s);
                    if (!string.IsNullOrEmpty(name)) return name;
                }
                current = current.parent;
            }
            return null;
        }

        #endregion

        #region Helpers — Location Scan

        /// <summary>Gets the player's current root sector, or null.</summary>
        private static Sector GetPlayerRootSector()
        {
            PlayerSectorDetector det = Locator.GetPlayerSectorDetector();
            if (det == null) return null;

            Sector lastSector = det.GetLastEnteredSector();
            if (lastSector == null) return null;

            return lastSector.IsRootSector() ? lastSector : lastSector.GetRootSector();
        }

        /// <summary>
        /// Recursively collects all sub-sectors of a root sector (up to 2 levels deep).
        /// Uses reflection on _subsectors (private List&lt;Sector&gt;).
        /// </summary>
        private static List<Sector> GetSubsectorsRecursive(Sector root)
        {
            var result = new List<Sector>();
            if (_subsectorsField == null) return result;

            try
            {
                var subs = _subsectorsField.GetValue(root) as List<Sector>;
                if (subs == null) return result;

                foreach (var sub in subs)
                {
                    if (sub == null) continue;
                    result.Add(sub);

                    var deeper = _subsectorsField.GetValue(sub) as List<Sector>;
                    if (deeper == null) continue;
                    foreach (var d in deeper)
                    {
                        if (d != null) result.Add(d);
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.LogState("[NavigationHandler] GetSubsectorsRecursive failed: " + ex.Message);
            }

            return result;
        }

        /// <summary>
        /// Returns a display name for a sector. Uses Loc mapping for named sectors,
        /// GetIDString() for unnamed sub-sectors, and cleaned gameObject.name as fallback.
        /// </summary>
        private static string GetSectorDisplayName(Sector sector)
        {
            Sector.Name sectorName = sector.GetName();

            if (sectorName != Sector.Name.Unnamed && sectorName != Sector.Name.Ship)
            {
                string locKey = SectorNameToLocKey(sectorName);
                if (!string.IsNullOrEmpty(locKey))
                {
                    string translated = Loc.Get(locKey);
                    if (translated != locKey) return translated;
                }
            }

            string idStr = sector.GetIDString();
            if (!string.IsNullOrEmpty(idStr))
            {
                string sectorKey  = "sector_" + idStr.ToLowerInvariant().Replace(" ", "").Replace("_", "");
                string translated = Loc.Get(sectorKey);
                if (translated != sectorKey) return translated;
                return idStr.Replace("_", " ");
            }

            string goName = sector.gameObject.name;
            if (goName.StartsWith("Sector_"))
                goName = goName.Substring(7);
            else if (goName.StartsWith("Sector"))
                goName = goName.Substring(6);
            goName = goName.Replace("_", " ").Trim();
            return string.IsNullOrEmpty(goName) ? null : goName;
        }

        /// <summary>Maps a Sector.Name enum value to the loc_ key used in Loc.cs.</summary>
        private static string SectorNameToLocKey(Sector.Name name)
        {
            switch (name)
            {
                case Sector.Name.Sun:                return "loc_sun";
                case Sector.Name.HourglassTwin_A:    return "loc_ash_twin";
                case Sector.Name.HourglassTwin_B:    return "loc_ember_twin";
                case Sector.Name.HourglassTwins:     return "loc_hourglass_twins";
                case Sector.Name.TimberHearth:       return "loc_timber_hearth";
                case Sector.Name.BrittleHollow:      return "loc_brittle_hollow";
                case Sector.Name.GiantsDeep:         return "loc_giants_deep";
                case Sector.Name.DarkBramble:        return "loc_dark_bramble";
                case Sector.Name.Comet:              return "loc_comet";
                case Sector.Name.QuantumMoon:        return "loc_quantum_moon";
                case Sector.Name.TimberMoon:         return "loc_timber_moon";
                case Sector.Name.VolcanicMoon:       return "loc_volcanic_moon";
                case Sector.Name.BrambleDimension:   return "loc_bramble_dimension";
                case Sector.Name.OrbitalProbeCannon: return "loc_probe_cannon";
                case Sector.Name.EyeOfTheUniverse:   return "loc_eye";
                case Sector.Name.SunStation:         return "loc_sun_station";
                case Sector.Name.WhiteHole:          return "loc_white_hole";
                case Sector.Name.TimeLoopDevice:     return "loc_time_loop_device";
                case Sector.Name.Vessel:             return "loc_vessel";
                case Sector.Name.VesselDimension:    return "loc_vessel_dimension";
                case Sector.Name.DreamWorld:         return "loc_dream_world";
                case Sector.Name.InvisiblePlanet:    return "loc_invisible_planet";
                default:                             return null;
            }
        }

        /// <summary>Returns a localized label for a campfire state.</summary>
        private static string GetCampfireStateLabel(Campfire.State state)
        {
            switch (state)
            {
                case Campfire.State.LIT:        return Loc.Get("nav_campfire_lit");
                case Campfire.State.SMOLDERING: return Loc.Get("nav_campfire_smoldering");
                case Campfire.State.UNLIT:      return Loc.Get("nav_campfire_unlit");
                default:                        return "";
            }
        }

        #endregion
    }
}
