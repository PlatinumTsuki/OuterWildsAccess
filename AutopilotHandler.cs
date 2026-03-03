using UnityEngine;

namespace OuterWildsAccess
{
    /// <summary>
    /// Accessible autopilot: lets the player cycle through planets and fly to them
    /// using the game's built-in Autopilot system.
    ///
    /// F4 workflow:
    ///   - At flight console → enter planet selection mode
    ///   - PageUp/PageDown   → cycle planets (name + distance announced)
    ///   - F4 again          → confirm and launch autopilot
    ///   - F4 during flight  → abort autopilot
    ///
    /// If the ship is landed, it is warped 50m above before engaging autopilot.
    ///
    /// Guard: ModSettings.AutopilotEnabled
    /// </summary>
    public class AutopilotHandler
    {
        #region Destination definition

        private struct Destination
        {
            public readonly AstroObject.Name AstroName;
            public readonly string           LocKey;

            public Destination(AstroObject.Name astroName, string locKey)
            {
                AstroName = astroName;
                LocKey    = locKey;
            }
        }

        private static readonly Destination[] _destinations = new Destination[]
        {
            new Destination(AstroObject.Name.TimberHearth,  "loc_timber_hearth"),
            new Destination(AstroObject.Name.BrittleHollow, "loc_brittle_hollow"),
            new Destination(AstroObject.Name.GiantsDeep,    "loc_giants_deep"),
            new Destination(AstroObject.Name.DarkBramble,   "loc_dark_bramble"),
            new Destination(AstroObject.Name.CaveTwin,      "loc_ash_twin"),
            new Destination(AstroObject.Name.TowerTwin,     "loc_ember_twin"),
            new Destination(AstroObject.Name.TimberMoon,    "loc_timber_moon"),
            new Destination(AstroObject.Name.VolcanicMoon,  "loc_volcanic_moon"),
            new Destination(AstroObject.Name.QuantumMoon,   "loc_quantum_moon"),
            new Destination(AstroObject.Name.RingWorld,     "loc_invisible_planet"),
            new Destination(AstroObject.Name.SunStation,    "loc_sun_station"),
            new Destination(AstroObject.Name.Comet,         "loc_comet"),
            new Destination(AstroObject.Name.ProbeCannon,   "loc_probe_cannon"),
        };

        #endregion

        #region State

        private bool   _isSelecting;
        private bool   _isAutopiloting;
        private int    _currentIndex;
        private string _currentDestName;

        // Cached autopilot reference (resolved per-flight)
        private Autopilot _autopilot;

        // State tracking for polling-based stage transitions
        private bool _wasLiningUp;
        private bool _wasApproaching;

        /// <summary>True while the player is cycling through planet choices.</summary>
        public bool IsSelecting => _isSelecting;

        /// <summary>True while the autopilot is actively flying to a destination.</summary>
        public bool IsAutopiloting => _isAutopiloting;

        #endregion

        #region Lifecycle

        /// <summary>Call from Main.Start().</summary>
        public void Initialize()
        {
            GlobalMessenger.AddListener("ExitFlightConsole", OnExitFlightConsole);
            DebugLogger.Log(LogCategory.State, "AutopilotHandler", "Initialized");
        }

        /// <summary>Call from Main.OnDestroy().</summary>
        public void Cleanup()
        {
            GlobalMessenger.RemoveListener("ExitFlightConsole", OnExitFlightConsole);
            UnhookEvents();
            _isSelecting    = false;
            _isAutopiloting = false;
            _autopilot      = null;
        }

        /// <summary>Call when scene changes to reset state.</summary>
        public void OnSceneLoaded()
        {
            UnhookEvents();
            _isSelecting    = false;
            _isAutopiloting = false;
            _wasLiningUp    = false;
            _wasApproaching = false;
            _autopilot      = null;
        }

        /// <summary>
        /// Polls autopilot state for stage transitions (alignment → approach).
        /// These are not events — only detectable by checking booleans each frame.
        /// Call from Main.Update().
        /// </summary>
        public void Update()
        {
            if (!_isAutopiloting || _autopilot == null) return;

            bool liningUp    = _autopilot.IsLiningUpDestination();
            bool approaching = _autopilot.IsApproachingDestination();

            if (liningUp && !_wasLiningUp)
                ScreenReader.Say(Loc.Get("autopilot_aligning"));

            if (approaching && !_wasApproaching)
                ScreenReader.Say(Loc.Get("autopilot_accelerating"));

            _wasLiningUp    = liningUp;
            _wasApproaching = approaching;
        }

        #endregion

        #region Public API (called from Main hotkeys)

        /// <summary>
        /// Enters destination selection mode (Home key at flight console).
        /// Announces the first destination in the list.
        /// </summary>
        public void StartSelection()
        {
            if (!ModSettings.AutopilotEnabled) return;

            // If already autopiloting, abort first
            if (_isAutopiloting)
            {
                AbortAutopilot();
                return;
            }

            _isSelecting  = true;
            _currentIndex = 0;
            string intro = Loc.Get("autopilot_select") + " " + BuildPlanetAnnouncement(0);
            ScreenReader.Say(intro);
            DebugLogger.Log(LogCategory.State, "AutopilotHandler", "Selection mode entered");
        }

        /// <summary>Cycle to next planet in the list (PageDown at flight console).</summary>
        public void CycleNext()
        {
            if (!_isSelecting) return;
            _currentIndex = (_currentIndex + 1) % _destinations.Length;
            ScreenReader.Say(BuildPlanetAnnouncement(_currentIndex));
        }

        /// <summary>Cycle to previous planet in the list (PageUp at flight console).</summary>
        public void CyclePrev()
        {
            if (!_isSelecting) return;
            _currentIndex = (_currentIndex - 1 + _destinations.Length) % _destinations.Length;
            ScreenReader.Say(BuildPlanetAnnouncement(_currentIndex));
        }

        /// <summary>
        /// Confirms the current selection and launches autopilot (End key at flight console).
        /// </summary>
        public void ConfirmSelection()
        {
            if (!_isSelecting) return;
            ConfirmDestination();
        }

        /// <summary>Cancels selection without launching (F4 or Escape).</summary>
        public void CancelSelection()
        {
            if (!_isSelecting) return;
            _isSelecting = false;
            ScreenReader.Say(Loc.Get("autopilot_cancelled"));
            DebugLogger.Log(LogCategory.State, "AutopilotHandler", "Selection cancelled");
        }

        /// <summary>Aborts an active autopilot flight (F4 while flying).</summary>
        public void Abort()
        {
            if (_isAutopiloting)
                AbortAutopilot();
        }

        #endregion

        #region Flight console event

        private void OnExitFlightConsole()
        {
            if (_isSelecting)
            {
                _isSelecting = false;
                DebugLogger.Log(LogCategory.State, "AutopilotHandler",
                    "Selection cancelled — left flight console");
            }
        }

        #endregion

        #region Internal logic

        private void ConfirmDestination()
        {
            _isSelecting = false;

            Destination dest = _destinations[_currentIndex];
            _currentDestName = Loc.Get(dest.LocKey);

            // Get the target AstroObject
            AstroObject astroObj = Locator.GetAstroObject(dest.AstroName);
            if (astroObj == null)
            {
                ScreenReader.Say(Loc.Get("autopilot_failed"));
                DebugLogger.Log(LogCategory.State, "AutopilotHandler",
                    $"AstroObject not found: {dest.AstroName}");
                return;
            }

            // Get reference frame
            OWRigidbody astroBody = astroObj.GetOWRigidbody();
            if (astroBody == null)
            {
                ScreenReader.Say(Loc.Get("autopilot_failed"));
                return;
            }

            ReferenceFrame refFrame = astroBody.GetReferenceFrame();
            if (refFrame == null || !refFrame.GetAllowAutopilot())
            {
                ScreenReader.Say(Loc.Get("autopilot_failed"));
                DebugLogger.Log(LogCategory.State, "AutopilotHandler",
                    $"No valid reference frame for {dest.AstroName}");
                return;
            }

            // Get ship autopilot
            OWRigidbody shipBody = Locator.GetShipBody();
            if (shipBody == null)
            {
                ScreenReader.Say(Loc.Get("autopilot_failed"));
                return;
            }

            _autopilot = shipBody.GetComponent<Autopilot>();
            if (_autopilot == null)
            {
                ScreenReader.Say(Loc.Get("autopilot_failed"));
                return;
            }

            if (_autopilot.IsDamaged())
            {
                ScreenReader.Say(Loc.Get("autopilot_damaged"));
                return;
            }

            // If ship is landed, warp it 50m above to clear the surface
            var landingMgr = shipBody.GetComponentInChildren<LandingPadManager>();
            if (landingMgr != null && landingMgr.IsLanded())
            {
                Vector3 liftPos = shipBody.GetPosition() + shipBody.transform.up * 50f;
                WarpHelper.WarpAndSetVelocity(
                    shipBody, liftPos, shipBody.GetRotation(), shipBody.GetVelocity());

                DebugLogger.Log(LogCategory.State, "AutopilotHandler",
                    "Ship warped 50m above landing site");
            }

            // Lock reference frame so telemetry (approach warnings, altimeter) works
            try
            {
                var rfTracker = Locator.GetPlayerTransform().GetComponent<ReferenceFrameTracker>();
                if (rfTracker != null)
                {
                    rfTracker.TargetReferenceFrame(refFrame);
                    DebugLogger.Log(LogCategory.State, "AutopilotHandler",
                        "Reference frame locked: " + _currentDestName);
                }
            }
            catch { }

            // Hook events before launching
            HookEvents();

            // Launch autopilot
            bool success = _autopilot.FlyToDestination(refFrame);

            if (success)
            {
                _isAutopiloting = true;
                ScreenReader.Say(Loc.Get("autopilot_initiated", _currentDestName));
                DebugLogger.Log(LogCategory.State, "AutopilotHandler",
                    $"Autopilot to {dest.AstroName} initiated");
            }
            else
            {
                UnhookEvents();
                ScreenReader.Say(Loc.Get("autopilot_already_close", _currentDestName));
                DebugLogger.Log(LogCategory.State, "AutopilotHandler",
                    $"Autopilot to {dest.AstroName}: already at destination");
            }
        }

        private void AbortAutopilot()
        {
            if (_autopilot != null && _autopilot.IsFlyingToDestination())
            {
                _autopilot.Abort();
            }

            _isAutopiloting = false;
            _wasLiningUp    = false;
            _wasApproaching = false;
            UnhookEvents();
            ScreenReader.Say(Loc.Get("autopilot_aborted"));
            DebugLogger.Log(LogCategory.State, "AutopilotHandler", "Autopilot aborted by player");
        }

        #endregion

        #region Autopilot events

        private void HookEvents()
        {
            if (_autopilot == null) return;
            _autopilot.OnArriveAtDestination  += OnArrived;
            _autopilot.OnFireRetroRockets     += OnRetro;
            _autopilot.OnAbortAutopilot       += OnAborted;
            _autopilot.OnInitMatchVelocity    += OnInitMatchVelocity;
            _autopilot.OnMatchedVelocity      += OnMatchedVelocity;
            _autopilot.OnAlreadyAtDestination += OnAlreadyAtDestination;
        }

        private void UnhookEvents()
        {
            if (_autopilot == null) return;
            _autopilot.OnArriveAtDestination  -= OnArrived;
            _autopilot.OnFireRetroRockets     -= OnRetro;
            _autopilot.OnAbortAutopilot       -= OnAborted;
            _autopilot.OnInitMatchVelocity    -= OnInitMatchVelocity;
            _autopilot.OnMatchedVelocity      -= OnMatchedVelocity;
            _autopilot.OnAlreadyAtDestination -= OnAlreadyAtDestination;
        }

        private void OnArrived(float arrivalError)
        {
            _isAutopiloting = false;
            _wasLiningUp    = false;
            _wasApproaching = false;
            UnhookEvents();
            ScreenReader.Say(Loc.Get("autopilot_arrived", _currentDestName));
            DebugLogger.Log(LogCategory.State, "AutopilotHandler",
                $"Arrived at {_currentDestName} (error: {arrivalError:F0}m)");
        }

        private void OnRetro()
        {
            ScreenReader.Say(Loc.Get("autopilot_retro"));
        }

        private void OnAborted()
        {
            // Only announce if we didn't trigger the abort ourselves
            if (_isAutopiloting)
            {
                _isAutopiloting = false;
                _wasLiningUp    = false;
                _wasApproaching = false;
                UnhookEvents();
                ScreenReader.Say(Loc.Get("autopilot_aborted"));
                DebugLogger.Log(LogCategory.State, "AutopilotHandler",
                    "Autopilot aborted by game");
            }
        }

        private void OnInitMatchVelocity()
        {
            ScreenReader.Say(Loc.Get("autopilot_matching_velocity"));
        }

        private void OnMatchedVelocity()
        {
            ScreenReader.Say(Loc.Get("autopilot_velocity_matched"));
        }

        private void OnAlreadyAtDestination()
        {
            _isAutopiloting = false;
            _wasLiningUp    = false;
            _wasApproaching = false;
            UnhookEvents();
            ScreenReader.Say(Loc.Get("autopilot_already_close", _currentDestName));
        }

        #endregion

        #region Helpers

        private string BuildPlanetAnnouncement(int index)
        {
            Destination dest = _destinations[index];
            string name = Loc.Get(dest.LocKey);

            // Calculate distance from ship to planet
            AstroObject astroObj = Locator.GetAstroObject(dest.AstroName);
            OWRigidbody shipBody = Locator.GetShipBody();

            if (astroObj != null && shipBody != null)
            {
                float dist = Vector3.Distance(
                    shipBody.GetPosition(),
                    astroObj.GetOWRigidbody().GetPosition());
                int distInt = Mathf.RoundToInt(dist);
                return Loc.Get("autopilot_planet_item", index + 1, _destinations.Length, name, distInt);
            }

            return Loc.Get("autopilot_planet_item_no_dist", index + 1, _destinations.Length, name);
        }

        #endregion
    }
}
