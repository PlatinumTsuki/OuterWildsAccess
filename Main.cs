using OWML.Common;
using OWML.ModHelper;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OuterWildsAccess
{
    /// <summary>
    /// Main mod entry point for OuterWildsAccess.
    /// Extends ModBehaviour (MonoBehaviour) — OWML entry point.
    ///
    /// Keep this class SMALL:
    ///   - Lifecycle only (Awake, Start, Update, OnDestroy)
    ///   - Global hotkey dispatch
    ///   - Handler instantiation
    ///
    /// All feature logic goes in separate Handler classes.
    /// </summary>
    public class Main : ModBehaviour
    {
        #region Fields

        /// <summary>
        /// Debug mode. When true, logs all screen reader output.
        /// Toggle with F12.
        /// </summary>
        public static bool DebugMode = false;

        /// <summary>
        /// Master toggle. When false, the entire mod is silent and inactive.
        /// Toggle with F5. Only F5 is processed while disabled.
        /// </summary>
        public static bool ModEnabled = true;

        // Handlers — one per feature
        private MenuHandler       _menuHandler;
        private StateHandler      _stateHandler;
        private PromptHandler     _promptHandler;
        private LocationHandler   _locationHandler;
        private NavigationHandler _navigationHandler;
        private BeaconHandler        _beaconHandler;
        private CollisionBeepHandler _collisionBeepHandler;
        private AutoWalkHandler      _autoWalkHandler;
        private DialogueHandler      _dialogueHandler;
        private AccessibilityMenu    _accessibilityMenu;
        private HelpMenu             _helpMenu;
        private ProximityHandler     _proximityHandler;
        private ResourceMonitor      _resourceMonitor;
        private ShipLogHandler       _shipLogHandler;
        private GhostMatterHandler   _ghostMatterHandler;
        private PathGuidanceHandler  _pathGuidanceHandler;
        private ShipRecallHandler    _shipRecallHandler;
        private AutopilotHandler     _autopilotHandler;
        private ShipLogReader        _shipLogReader;
        private NomaiTextHandler     _nomaiTextHandler;
        private SignalscopeHandler   _signalscopeHandler;
        private ShipPilotHandler     _shipPilotHandler;
        private StatusHandler         _statusHandler;
        private ScoutHandler          _scoutHandler;
        private ModelRocketHandler    _modelRocketHandler;
        private QuantumHandler        _quantumHandler;
        private DarkBrambleHandler    _darkBrambleHandler;
        private ElevatorHandler       _elevatorHandler;
        private GravityHandler        _gravityHandler;
        private RepairHandler         _repairHandler;

        // Shared pathfinding instance — used by AutoWalk + Guidance
        private PathScanner          _sharedPathScanner;

        // Cached hazard references for teleport safety checks.
        // Refreshed per TP call — avoids depending on disabled colliders.
        private Campfire[]      _cachedCampfires;
        private HazardVolume[]  _cachedHazardVolumes;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            // ModHelper is NOT yet set in Awake() — OWML injects it after AddComponent.
            // Do NOT use ModHelper here.
        }

        private void Start()
        {
            // ModHelper is available from Start() onwards.
            DebugLogger.Initialize(ModHelper);
            ScreenReader.Initialize(ModHelper);
            Loc.Initialize();
            ModSettings.Initialize(Application.persistentDataPath);

            // Sync ini → OWML config so the MODS menu reflects current settings.
            SyncModSettingsToOwml();

            ModHelper.Console.WriteLine("OuterWildsAccess Start OK", MessageType.Success);

            // Subscribe to game events
            LoadManager.OnCompleteSceneLoad += OnSceneLoaded;

            // Poll until TextTranslation is ready, then re-detect language and subscribe
            StartCoroutine(WaitForTextTranslation());

            // Initialize handlers
            _menuHandler = new MenuHandler();
            _menuHandler.Initialize();

            _stateHandler = new StateHandler();
            _stateHandler.Initialize();

            _promptHandler = new PromptHandler();
            _promptHandler.Initialize();

            _locationHandler = new LocationHandler();
            _locationHandler.Initialize();

            _navigationHandler = new NavigationHandler();
            _navigationHandler.Initialize();

            _beaconHandler = new BeaconHandler();
            _beaconHandler.Initialize();

            _collisionBeepHandler = new CollisionBeepHandler();
            _collisionBeepHandler.Initialize();

            _sharedPathScanner = new PathScanner();

            _autoWalkHandler = new AutoWalkHandler();
            _autoWalkHandler.Initialize(ModHelper, _sharedPathScanner, () => _promptHandler?.OnArrival());

            _dialogueHandler = new DialogueHandler();
            _dialogueHandler.Initialize();

            _accessibilityMenu = new AccessibilityMenu();
            _accessibilityMenu.Initialize();

            _helpMenu = new HelpMenu();
            _helpMenu.Initialize();

            _proximityHandler = new ProximityHandler();
            _proximityHandler.Initialize(() => _autoWalkHandler?.IsActive ?? false);

            _resourceMonitor = new ResourceMonitor();
            _statusHandler = new StatusHandler();

            _shipLogHandler = new ShipLogHandler();
            _shipLogHandler.Initialize();

            _ghostMatterHandler = new GhostMatterHandler();
            _ghostMatterHandler.Initialize(
                stopAutoWalk: () => { if (_autoWalkHandler?.IsActive == true) _autoWalkHandler.Toggle(); });

            _pathGuidanceHandler = new PathGuidanceHandler();
            _pathGuidanceHandler.Initialize(_sharedPathScanner);

            // Meditation unlock (no need to find Gabbro)
            MeditationUnlocker.Initialize();

            _shipRecallHandler = new ShipRecallHandler();
            _shipRecallHandler.Initialize();

            _autopilotHandler = new AutopilotHandler();
            _autopilotHandler.Initialize();

            _shipLogReader = new ShipLogReader();
            _shipLogReader.Initialize();

            _nomaiTextHandler = new NomaiTextHandler();
            _nomaiTextHandler.Initialize();

            _modelRocketHandler = new ModelRocketHandler();
            _modelRocketHandler.Initialize();

            _signalscopeHandler = new SignalscopeHandler();
            _signalscopeHandler.Initialize();

            _shipPilotHandler = new ShipPilotHandler();
            _shipPilotHandler.Initialize();

            _scoutHandler = new ScoutHandler();
            _scoutHandler.Initialize();

            _quantumHandler = new QuantumHandler();
            _quantumHandler.Initialize();

            _darkBrambleHandler = new DarkBrambleHandler();
            _darkBrambleHandler.Initialize();

            _elevatorHandler = new ElevatorHandler();
            _elevatorHandler.Initialize();

            _gravityHandler = new GravityHandler();
            _gravityHandler.Initialize();

            _repairHandler = new RepairHandler();
            _repairHandler.Initialize();

            // Peaceful ghosts (DLC hostile AI disabled)
            PeacefulGhostsHandler.Initialize();

            // Wire navigation direction to use A* waypoint when guidance is active
            _navigationHandler.SetWaypointOverride(
                () => _pathGuidanceHandler?.CurrentWaypointPosition);

            // Announce mod loaded (delayed so screen reader is ready)
            StartCoroutine(AnnounceLoadedDelayed());
        }

        private void Update()
        {
            // F5 toggle is always processed, even when mod is disabled
            if (Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame)
            {
                ToggleMod();
                return;
            }

            if (!ModEnabled) return;

            ProcessHotkeys();
            _menuHandler?.Update();
            _promptHandler?.Update();
            _navigationHandler?.Update();
            _beaconHandler?.Update();
            _collisionBeepHandler?.Update();
            _autoWalkHandler?.Update();
            _pathGuidanceHandler?.Update();
            _proximityHandler?.Update();
            _resourceMonitor?.Update();
            _ghostMatterHandler?.Update();
            _signalscopeHandler?.Update();
            _shipPilotHandler?.Update();
            _autopilotHandler?.Update();
            _scoutHandler?.Update();
            _modelRocketHandler?.Update();
            _darkBrambleHandler?.Update();
            _elevatorHandler?.Update();
            _gravityHandler?.Update();
            _repairHandler?.Update();
        }

        private void OnDestroy()
        {
            _menuHandler?.Cleanup();
            _stateHandler?.Cleanup();
            _locationHandler?.Cleanup();
            _navigationHandler?.Cleanup();
            _beaconHandler?.Cleanup();
            _collisionBeepHandler?.Cleanup();
            _autoWalkHandler?.Cleanup();
            _dialogueHandler?.Cleanup();
            _shipLogHandler?.Cleanup();
            _ghostMatterHandler?.Cleanup();
            _pathGuidanceHandler?.Cleanup();
            _shipRecallHandler?.Cleanup();
            _autopilotHandler?.Cleanup();
            _shipLogReader?.Cleanup();
            _nomaiTextHandler?.Cleanup();
            _modelRocketHandler?.Cleanup();
            _signalscopeHandler?.Cleanup();
            _shipPilotHandler?.Cleanup();
            _scoutHandler?.Cleanup();
            _quantumHandler?.Cleanup();
            _darkBrambleHandler?.Cleanup();
            _elevatorHandler?.Cleanup();
            _gravityHandler?.Cleanup();
            _repairHandler?.Cleanup();
            LoadManager.OnCompleteSceneLoad -= OnSceneLoaded;
            if (_languageListenerRegistered)
            {
                try
                {
                    var tt = TextTranslation.Get();
                    if (tt != null) tt.OnLanguageChanged -= OnGameLanguageChanged;
                }
                catch { }
            }
            ScreenReader.Shutdown();
        }

        #endregion

        #region OWML Settings Sync

        /// <summary>
        /// Called by OWML when the player saves settings from the MODS menu.
        /// Syncs OWML config → ModSettings and persists to ini.
        /// </summary>
        public override void Configure(IModConfig config)
        {
            try
            {
                ModSettings.BeaconEnabled       = config.GetSettingsValue<bool>("BeaconEnabled");
                ModSettings.NavigationEnabled   = config.GetSettingsValue<bool>("NavigationEnabled");
                ModSettings.CollisionEnabled    = config.GetSettingsValue<bool>("CollisionEnabled");
                ModSettings.AutoWalkEnabled     = config.GetSettingsValue<bool>("AutoWalkEnabled");
                ModSettings.ProximityEnabled    = config.GetSettingsValue<bool>("ProximityEnabled");
                ModSettings.GaugeEnabled        = config.GetSettingsValue<bool>("GaugeEnabled");
                ModSettings.GuidanceEnabled     = config.GetSettingsValue<bool>("GuidanceEnabled");
                ModSettings.MeditationEnabled   = config.GetSettingsValue<bool>("MeditationEnabled");
                ModSettings.GhostMatterProtectionEnabled = config.GetSettingsValue<bool>("GhostMatterProtectionEnabled");
                ModSettings.ShipRecallEnabled   = config.GetSettingsValue<bool>("ShipRecallEnabled");
                ModSettings.AutopilotEnabled   = config.GetSettingsValue<bool>("AutopilotEnabled");
                ModSettings.PeacefulGhostsEnabled = config.GetSettingsValue<bool>("PeacefulGhostsEnabled");
                ModSettings.Save();
                DebugLogger.Log(LogCategory.State, "Main", "Settings synced from OWML config");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log(LogCategory.State, "Main", $"Configure() failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Pushes current ModSettings values into the OWML config so the MODS menu
        /// shows the correct state (e.g. after loading from ini on startup).
        /// </summary>
        private void SyncModSettingsToOwml()
        {
            try
            {
                ModHelper.Config.SetSettingsValue("BeaconEnabled",       ModSettings.BeaconEnabled);
                ModHelper.Config.SetSettingsValue("NavigationEnabled",   ModSettings.NavigationEnabled);
                ModHelper.Config.SetSettingsValue("CollisionEnabled",    ModSettings.CollisionEnabled);
                ModHelper.Config.SetSettingsValue("AutoWalkEnabled",     ModSettings.AutoWalkEnabled);
                ModHelper.Config.SetSettingsValue("ProximityEnabled",    ModSettings.ProximityEnabled);
                ModHelper.Config.SetSettingsValue("GaugeEnabled",        ModSettings.GaugeEnabled);
                ModHelper.Config.SetSettingsValue("GuidanceEnabled",     ModSettings.GuidanceEnabled);
                ModHelper.Config.SetSettingsValue("MeditationEnabled",  ModSettings.MeditationEnabled);
                ModHelper.Config.SetSettingsValue("GhostMatterProtectionEnabled", ModSettings.GhostMatterProtectionEnabled);
                ModHelper.Config.SetSettingsValue("ShipRecallEnabled",  ModSettings.ShipRecallEnabled);
                ModHelper.Config.SetSettingsValue("AutopilotEnabled",  ModSettings.AutopilotEnabled);
                ModHelper.Config.SetSettingsValue("PeacefulGhostsEnabled", ModSettings.PeacefulGhostsEnabled);
                ModHelper.Storage.Save(ModHelper.Config, "config.json");
                DebugLogger.Log(LogCategory.State, "Main", "ModSettings synced to OWML config");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log(LogCategory.State, "Main", $"SyncModSettingsToOwml() failed: {ex.Message}");
            }
        }

        #endregion

        #region Scene Loading

        private bool _languageListenerRegistered = false;
        private bool _languageReady = false;

        /// <summary>
        /// Polls until TextTranslation is ready, then subscribes to OnLanguageChanged.
        /// The game fires OnLanguageChanged after the player presses a key at the title
        /// screen and the profile settings are loaded — that's when the real language
        /// is known and _languageReady is set.
        /// </summary>
        private IEnumerator WaitForTextTranslation()
        {
            TextTranslation tt = null;
            float elapsed = 0f;
            while (elapsed < 30f)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
                try { tt = TextTranslation.Get(); } catch { }
                if (tt != null) break;
            }

            if (tt == null)
            {
                _languageReady = true;
                yield break;
            }

            if (!_languageListenerRegistered)
            {
                tt.OnLanguageChanged += OnGameLanguageChanged;
                _languageListenerRegistered = true;
            }
        }

        /// <summary>
        /// Called when the game applies language settings (after player presses key
        /// at title screen and profile is loaded).
        /// </summary>
        private void OnGameLanguageChanged()
        {
            Loc.Initialize();
            _languageReady = true;
        }

        private void OnSceneLoaded(OWScene oldScene, OWScene newScene)
        {
            DebugLogger.LogState($"Scene loaded: {newScene}");

            // Safety re-detect and ensure _languageReady is set
            Loc.Initialize();
            _languageReady = true;

            _promptHandler?.OnSceneLoaded();
            _resourceMonitor?.OnSceneLoaded();
            _statusHandler?.OnSceneLoaded();
            _autopilotHandler?.OnSceneLoaded();
            _shipPilotHandler?.OnSceneLoaded();

            if (newScene == OWScene.SolarSystem || newScene == OWScene.EyeOfTheUniverse)
                StartCoroutine(AnnounceSpawnLocationDelayed());
        }

        private IEnumerator AnnounceSpawnLocationDelayed()
        {
            // Short delay so sectors are fully registered before reading location
            yield return new WaitForSeconds(2f);
            _locationHandler?.AnnounceCurrentLocation();
        }

        #endregion

        #region Hotkeys

        private void ProcessHotkeys()
        {
            if (Keyboard.current == null) return;

            // ── Help menu open: intercept all keys ──
            if (_helpMenu != null && _helpMenu.IsOpen)
            {
                // Escape — close from any level
                if (Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    _helpMenu.Close();
                    return;
                }

                // Backspace — go back one level or close
                if (Keyboard.current.backspaceKey.wasPressedThisFrame)
                {
                    _helpMenu.GoBack();
                    return;
                }

                // Enter — drill into category
                if (Keyboard.current.enterKey.wasPressedThisFrame)
                {
                    _helpMenu.DrillDown();
                    return;
                }

                // Arrow Down — next item/category
                if (Keyboard.current.downArrowKey.wasPressedThisFrame)
                {
                    _helpMenu.CycleNext();
                    return;
                }

                // Arrow Up — previous item/category
                if (Keyboard.current.upArrowKey.wasPressedThisFrame)
                {
                    _helpMenu.CyclePrev();
                    return;
                }

                return; // block all other keys
            }

            // ── Ship log reader open: intercept all keys ──
            if (_shipLogReader != null && _shipLogReader.IsOpen)
            {
                // Escape — close reader entirely
                if (Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    _shipLogReader.OnF4Pressed();
                    return;
                }
                // Backspace — go back one level
                if (Keyboard.current.backspaceKey.wasPressedThisFrame)
                {
                    _shipLogReader.OnF6Pressed();
                    return;
                }
                if (Keyboard.current.pageDownKey.wasPressedThisFrame ||
                    Keyboard.current.downArrowKey.wasPressedThisFrame)
                {
                    _shipLogReader.CycleNext();
                    return;
                }
                if (Keyboard.current.pageUpKey.wasPressedThisFrame ||
                    Keyboard.current.upArrowKey.wasPressedThisFrame)
                {
                    _shipLogReader.CyclePrev();
                    return;
                }
                if (Keyboard.current.enterKey.wasPressedThisFrame)
                {
                    _shipLogReader.DrillDown();
                    return;
                }
                // All other keys blocked while reader is open
                return;
            }

            // ── Menu open: all keys are intercepted here, nothing else fires ──
            if (_accessibilityMenu != null && _accessibilityMenu.IsOpen)
            {
                // Escape — close menu and save
                if (Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    _accessibilityMenu.Close();
                    return;
                }

                // Page Down / Arrow Down — next item
                if (Keyboard.current.pageDownKey.wasPressedThisFrame ||
                    Keyboard.current.downArrowKey.wasPressedThisFrame)
                {
                    _accessibilityMenu.NavigateNext();
                    return;
                }

                // Page Up / Arrow Up — previous item
                if (Keyboard.current.pageUpKey.wasPressedThisFrame ||
                    Keyboard.current.upArrowKey.wasPressedThisFrame)
                {
                    _accessibilityMenu.NavigatePrev();
                    return;
                }

                // Enter — toggle current item
                if (Keyboard.current.enterKey.wasPressedThisFrame)
                {
                    _accessibilityMenu.ToggleCurrent();
                    return;
                }

                // All other keys blocked while menu is open
                return;
            }

            // ── Menu closed: normal hotkeys ──

            // F12 — toggle debug mode
            if (Keyboard.current.f12Key.wasPressedThisFrame)
            {
                DebugMode = !DebugMode;
                DebugLogger.LogInput("F12", "DebugMode");
                ScreenReader.Say(DebugMode ? Loc.Get("debug_on") : Loc.Get("debug_off"));
                return;
            }

            // ── All keys below require an active gameplay scene ──
            OWScene currentScene = LoadManager.GetCurrentScene();
            if (currentScene != OWScene.SolarSystem && currentScene != OWScene.EyeOfTheUniverse)
                return;

            // F1 — open help menu
            if (Keyboard.current.f1Key.wasPressedThisFrame)
            {
                _helpMenu?.Open();
                return;
            }

            // F2 — loop timer (time remaining before supernova)
            if (Keyboard.current.f2Key.wasPressedThisFrame)
            {
                DebugLogger.LogInput("F2", "Loop timer");
                AnnounceLoopTimer();
                return;
            }

            // F3 — recall ship above player
            if (Keyboard.current.f3Key.wasPressedThisFrame)
            {
                DebugLogger.LogInput("F3", "RecallShip");
                _shipRecallHandler?.RecallShip();
                return;
            }

            // T — teleport to selected navigation target
            if (Keyboard.current.tKey.wasPressedThisFrame)
            {
                DebugLogger.LogInput("T", "Teleport");
                TeleportToSelected();
                return;
            }

            // F4 — open ship log reader
            if (Keyboard.current.f4Key.wasPressedThisFrame)
            {
                DebugLogger.LogInput("F4", "ShipLogReader");
                _shipLogReader?.OnF4Pressed();
                return;
            }

            // U — signalscope status
            if (Keyboard.current.uKey.wasPressedThisFrame)
            {
                DebugLogger.LogInput("U", "Signalscope status");
                _signalscopeHandler?.ReadStatus();
                return;
            }

            // I — ship flight status
            if (Keyboard.current.iKey.wasPressedThisFrame)
            {
                DebugLogger.LogInput("I", "Ship flight status");
                _shipPilotHandler?.ReadFlightStatus();
                return;
            }

            // O — scout probe status
            if (Keyboard.current.oKey.wasPressedThisFrame)
            {
                DebugLogger.LogInput("O", "Scout status");
                _scoutHandler?.ReadStatus();
                return;
            }

            // Delete — repeat last announcement
            if (Keyboard.current.deleteKey.wasPressedThisFrame)
            {
                DebugLogger.LogInput("Delete", "RepeatLast");
                ScreenReader.RepeatLast();
                return;
            }

            // ── Model rocket console: End → autopilot to geyser ──
            if (_modelRocketHandler != null && _modelRocketHandler.AtConsole)
            {
                if (Keyboard.current.endKey.wasPressedThisFrame)
                {
                    DebugLogger.LogInput("End", "Model rocket autopilot toggle");
                    _modelRocketHandler.ToggleAutopilot();
                }
                return; // no other nav keys at model rocket console
            }

            // ── Flight console: Home/PageUp/PageDown/End → autopilot ──
            if (PlayerState.AtFlightConsole())
            {
                // Home — enter autopilot destination selection
                if (Keyboard.current.homeKey.wasPressedThisFrame)
                {
                    DebugLogger.LogInput("Home", "Autopilot selection");
                    _autopilotHandler?.StartSelection();
                    return;
                }

                // PageDown — next destination
                if (Keyboard.current.pageDownKey.wasPressedThisFrame)
                {
                    _autopilotHandler?.CycleNext();
                    return;
                }

                // PageUp — previous destination
                if (Keyboard.current.pageUpKey.wasPressedThisFrame)
                {
                    _autopilotHandler?.CyclePrev();
                    return;
                }

                // End — confirm destination and launch autopilot
                if (Keyboard.current.endKey.wasPressedThisFrame)
                {
                    DebugLogger.LogInput("End", "Autopilot confirm");
                    _autopilotHandler?.ConfirmSelection();
                    return;
                }

                return; // no further nav keys while at console
            }

            // ── On foot: Home/PageUp/PageDown/End → navigation ──

            // Début (Home) — fresh navigation scan
            if (Keyboard.current.homeKey.wasPressedThisFrame)
            {
                DebugLogger.LogInput("Home", "Navigation scan");
                _navigationHandler?.Scan();
                return;
            }

            // ── Alt+PageUp/Down — category switching (must come before plain PageUp/Down) ──
            bool altHeld = Keyboard.current.leftAltKey.isPressed
                        || Keyboard.current.rightAltKey.isPressed;

            if (altHeld && Keyboard.current.pageDownKey.wasPressedThisFrame)
            {
                DebugLogger.LogInput("Alt+PageDown", "Category next");
                _navigationHandler?.CategoryNext();
                return;
            }
            if (altHeld && Keyboard.current.pageUpKey.wasPressedThisFrame)
            {
                DebugLogger.LogInput("Alt+PageUp", "Category prev");
                _navigationHandler?.CategoryPrev();
                return;
            }

            // Page suivante — next scanned object
            if (Keyboard.current.pageDownKey.wasPressedThisFrame)
            {
                DebugLogger.LogInput("PageDown", "Navigation next");
                _navigationHandler?.CycleNext();
                SyncHandlersToNavTarget();
                return;
            }

            // Page précédente — previous scanned object
            if (Keyboard.current.pageUpKey.wasPressedThisFrame)
            {
                DebugLogger.LogInput("PageUp", "Navigation prev");
                _navigationHandler?.CyclePrev();
                SyncHandlersToNavTarget();
                return;
            }

            // Fin (End) — announce direction + distance to current target
            if (Keyboard.current.endKey.wasPressedThisFrame)
            {
                DebugLogger.LogInput("End", "Distance query");
                _navigationHandler?.NavigateToTarget();
                return;
            }

            // B — toggle auto-walk toward navigation target
            if (Keyboard.current.bKey.wasPressedThisFrame)
            {
                DebugLogger.LogInput("B", "AutoWalk toggle");
                _autoWalkHandler?.Toggle();
                return;
            }

            // G — toggle path guidance (audio ticks along A* path)
            if (Keyboard.current.gKey.wasPressedThisFrame)
            {
                DebugLogger.LogInput("G", "Guidance toggle");
                _pathGuidanceHandler?.Toggle();
                _navigationHandler?.SetGuidanceActive(_pathGuidanceHandler?.IsActive ?? false);
                return;
            }

            // F6 — open accessibility menu
            if (Keyboard.current.f6Key.wasPressedThisFrame)
            {
                DebugLogger.LogInput("F6", "Menu open");
                _accessibilityMenu?.Open();
                return;
            }

            // L — current location (L = Lieu/Location)
            if (Keyboard.current.lKey.wasPressedThisFrame)
            {
                DebugLogger.LogInput("L", "Location");
                _locationHandler?.AnnounceCurrentLocation();
                return;
            }

            // H — personal status (health, oxygen, fuel, boost, suit)
            if (Keyboard.current.hKey.wasPressedThisFrame)
            {
                DebugLogger.LogInput("H", "Personal status");
                _statusHandler?.ReadPersonalStatus();
                return;
            }

            // J — ship status (fuel, oxygen, hull, damage)
            if (Keyboard.current.jKey.wasPressedThisFrame)
            {
                DebugLogger.LogInput("J", "Ship status");
                _statusHandler?.ReadShipStatus();
                return;
            }

            // K — environment (sector, hazards, gravity)
            if (Keyboard.current.kKey.wasPressedThisFrame)
            {
                DebugLogger.LogInput("K", "Environment status");
                _statusHandler?.ReadEnvironment();
                return;
            }

            // Backspace — mute/unmute beacon for current target (per-target, not global)
            if (Keyboard.current.backspaceKey.wasPressedThisFrame)
            {
                DebugLogger.LogInput("Backspace", "Beacon mute toggle");
                _beaconHandler?.ToggleMute();
                return;
            }
        }

        #endregion

        #region Handler Sync

        /// <summary>
        /// Syncs beacon, auto-walk and guidance handlers to the current navigation target.
        /// Skips guidance if active (avoid recomputing path on every cycle).
        /// Skips auto-walk if active (don't redirect mid-walk).
        /// </summary>
        private void SyncHandlersToNavTarget()
        {
            if (_navigationHandler == null) return;
            Transform t            = _navigationHandler.ActiveTargetTransform;
            string    name         = _navigationHandler.ActiveTargetName;
            bool      interactable = _navigationHandler.ActiveTargetIsInteractable;

            _beaconHandler?.SetTarget(t);

            if (_autoWalkHandler != null && !_autoWalkHandler.IsActive)
                _autoWalkHandler.SetTarget(t, name, interactable);

            if (_pathGuidanceHandler != null && !_pathGuidanceHandler.IsActive)
                _pathGuidanceHandler.SetTarget(t, name);
        }

        #endregion

        #region Startup

        /// <summary>
        /// Toggles the entire mod on/off. When disabled, stops all active
        /// features and silences screen reader output.
        /// </summary>
        private void ToggleMod()
        {
            if (ModEnabled)
            {
                // Stop active features before disabling
                if (_autoWalkHandler?.IsActive == true) _autoWalkHandler.Toggle();
                if (_pathGuidanceHandler?.IsActive == true)
                {
                    _pathGuidanceHandler.Toggle();
                    _navigationHandler?.SetGuidanceActive(false);
                }
                _beaconHandler?.SetMuted(true);

                // Close any open menus
                if (_accessibilityMenu?.IsOpen == true) _accessibilityMenu.Close();
                if (_helpMenu?.IsOpen == true) _helpMenu.Close();
                if (_shipLogReader?.IsOpen == true) _shipLogReader.OnF4Pressed();

                // Announce THEN disable (force bypasses ModEnabled check)
                ModEnabled = false;
                ScreenReader.SayForce(Loc.Get("mod_disabled"));
                DebugLogger.LogState("Mod DISABLED via F5");
            }
            else
            {
                ModEnabled = true;
                ScreenReader.SayForce(Loc.Get("mod_enabled"));
                DebugLogger.LogState("Mod ENABLED via F5");
            }
        }

        private IEnumerator AnnounceLoadedDelayed()
        {
            // Wait for OnGameLanguageChanged to fire (player presses key at title)
            while (!_languageReady)
                yield return null;

            // Small extra delay so the main menu is settled
            yield return new WaitForSeconds(0.5f);

            ScreenReader.Say(Loc.Get("mod_loaded"));

            // Announce which speech backend is active
            string backend;
            if (ScreenReader.HasPriorities && ModSettings.NvdaDirectEnabled)
                backend = "NVDA direct";
            else
                backend = "Tolk";
            ScreenReader.SayQueued(Loc.Get("backend_speech", backend));
        }

        private const float MaxTeleportDistance = 500f;
        private const float TpProbeRadius  = 0.35f;  // SphereCast radius (match PathScanner)
        private const float TpProbeUp      = 3f;     // start probe this far above candidate
        private const float TpProbeLength  = 6f;     // max downward probe distance
        private const float TpMaxSlope     = 45f;    // reject slopes steeper than this (single normal)
        private const float TpMaxSlopePair = 115f;   // multi-slope pair threshold (game's _maxAngleBetweenSlopes)
        private const float TpOffset       = 2f;     // horizontal offset from target
        private const float TpFootClear    = 1.2f;   // clearance above ground for feet
        private const int   TpCandidates   = 8;      // positions to try around target
        private static readonly RaycastHit[] _tpCastBuffer = new RaycastHit[32];
        private static readonly Vector3[]    _tpProjectedNormals = new Vector3[16];


        private void TeleportToSelected()
        {
            // Block teleport while at the flight console OR anywhere inside
            // the ship cabin. Teleporting while the game still considers the
            // player to be in the ship leaves stale state that can persist
            // after arrival and confuse the screen reader narrative.
            if (PlayerState.AtFlightConsole() || PlayerState.IsInsideShip())
            {
                ScreenReader.Say(Loc.Get("teleport_not_on_foot"));
                return;
            }

            if (_navigationHandler == null || _navigationHandler.SelectedTargetTransform == null)
            {
                ScreenReader.Say(Loc.Get("teleport_no_target"));
                return;
            }

            Transform target = _navigationHandler.SelectedTargetTransform;
            string targetName = _navigationHandler.SelectedTargetName;
            bool isInteractable = _navigationHandler.SelectedTargetIsInteractable;

            OWRigidbody playerBody = Locator.GetPlayerBody();
            if (playerBody == null) return;

            // Special case: teleport to ship via the tractor beam.
            //   - A direct warp into the cabin bypasses the HatchController
            //     PlayerDetector trigger, so "EnterShip" never fires →
            //     ShipResources never feeds cabin oxygen → asphyxiation on
            //     takeoff.
            //   - Solution: arm the tractor beam manually and warp the
            //     player into its fluid volume. The beam fluid lifts the
            //     player up through the airlock trigger naturally — the
            //     game itself fires "EnterShip" via its own physics
            //     trigger, leaving all state perfectly consistent.
            Transform shipTr = Locator.GetShipTransform();
            if (shipTr != null && target == shipTr)
            {
                if (TryTeleportToShipViaBeam(playerBody, targetName))
                    return;
                // Fallback: if beam unavailable (ship system failure, etc.),
                // continue with the regular probe logic. We bias the offset
                // larger so the player lands clear of the hull instead of
                // inside it.
            }
            bool targetIsShip = (shipTr != null && target == shipTr);

            float dist = Vector3.Distance(playerBody.GetPosition(), target.position);
            if (dist > MaxTeleportDistance)
            {
                ScreenReader.Say(Loc.Get("teleport_too_far"));
                return;
            }

            // Calculate up direction at target using the game's gravity system.
            // Search for a GravityVolume on the target's body or its parents
            // (e.g., island → planet) and use its actual force calculation.
            // This handles spherical, directional, and all other gravity types.
            Vector3 targetPos = target.position;
            Vector3 upDir = playerBody.transform.up;
            OWRigidbody groundBody = null;
            try { groundBody = target.GetComponentInParent<OWRigidbody>(); }
            catch { }

            if (groundBody != null)
            {
                GravityVolume gv = groundBody.GetComponentInChildren<GravityVolume>();

                // If no GravityVolume on this body, walk up the hierarchy.
                // Handles islands on Giant's Deep (island has no gravity,
                // the planet body above it does).
                if (gv == null)
                {
                    Transform cur = groundBody.transform.parent;
                    while (cur != null && gv == null)
                    {
                        gv = cur.GetComponent<GravityVolume>();
                        if (gv == null)
                            gv = cur.GetComponentInChildren<GravityVolume>();
                        cur = cur.parent;
                    }
                }

                if (gv != null)
                {
                    try
                    {
                        Vector3 gravAccel = gv.CalculateForceAccelerationAtPoint(targetPos);
                        if (gravAccel.sqrMagnitude > 0.01f)
                            upDir = -gravAccel.normalized;
                        else
                            upDir = (targetPos - groundBody.GetWorldCenterOfMass()).normalized;
                    }
                    catch
                    {
                        upDir = (targetPos - groundBody.GetWorldCenterOfMass()).normalized;
                    }
                }
                else
                {
                    // No GravityVolume found — geometric fallback
                    upDir = (targetPos - groundBody.GetWorldCenterOfMass()).normalized;
                }
            }

            DebugLogger.Log(LogCategory.State, "Teleport",
                $"[TP-DEBUG] upDir=({upDir.x:F2},{upDir.y:F2},{upDir.z:F2}) groundBody={groundBody?.name ?? "null"}");

            bool isLocation = _navigationHandler.SelectedTargetIsLocation;
            Vector3 markerPos = targetPos;

            // ── Smart TP: find the interaction collider and probe
            // around its center instead of the raw transform. ─────────
            // For interactables, this means we land in front of the
            // collider at the right distance for interaction. For NPCs,
            // standing in front (target.forward) avoids obstacles
            // behind them (campfires, walls, etc.).
            Vector3 probeCenter = targetPos;
            Collider targetCol = target.GetComponent<Collider>();
            if (targetCol == null)
                targetCol = target.GetComponentInChildren<Collider>();
            if (targetCol != null)
                probeCenter = targetCol.bounds.center;

            // Use the collider center's horizontal position but at
            // the target's height for the probe ring center.
            Vector3 probeCenterGround = targetPos
                + (probeCenter - targetPos)
                - Vector3.Project(probeCenter - targetPos, upDir);

            // Log nearby campfires for fire debugging
            var nearCampfires = Object.FindObjectsOfType<Campfire>();
            if (nearCampfires != null)
            {
                foreach (var cf in nearCampfires)
                {
                    float cfDist = Vector3.Distance(cf.transform.position, targetPos);
                    if (cfDist < 15f)
                    {
                        DebugLogger.Log(LogCategory.State, "Teleport",
                            $"[TP-DEBUG] Nearby campfire: {cf.gameObject.name} dist={cfDist:F1}m"
                            + $" pos=({cf.transform.position.x:F1},{cf.transform.position.y:F1},{cf.transform.position.z:F1})"
                            + $" state={cf.GetState()}");
                    }
                }
            }

            DebugLogger.Log(LogCategory.State, "Teleport",
                $"[TP-DEBUG] target={targetName} pos=({targetPos.x:F1},{targetPos.y:F1},{targetPos.z:F1})"
                + $" collider={targetCol?.GetType().Name ?? "null"} probeCenter=({probeCenter.x:F1},{probeCenter.y:F1},{probeCenter.z:F1})"
                + $" probeCenterGround=({probeCenterGround.x:F1},{probeCenterGround.y:F1},{probeCenterGround.z:F1})"
                + $" playerPos=({playerBody.GetPosition().x:F1},{playerBody.GetPosition().y:F1},{playerBody.GetPosition().z:F1})"
                + $" isInteractable={isInteractable} isLocation={isLocation}");

            // Build base offset direction.
            // For interactable targets: use target.forward (NPCs face
            // the open conversation area). For locations/other: use
            // direction from target toward player.
            Vector3 baseDir;
            if (isInteractable || !isLocation)
            {
                Vector3 tgtFwd = target.forward;
                tgtFwd = tgtFwd - Vector3.Project(tgtFwd, upDir);
                DebugLogger.Log(LogCategory.State, "Teleport",
                    $"[TP-DEBUG] target.forward=({target.forward.x:F2},{target.forward.y:F2},{target.forward.z:F2})"
                    + $" tgtFwd_horiz=({tgtFwd.x:F2},{tgtFwd.y:F2},{tgtFwd.z:F2})");
                if (tgtFwd.sqrMagnitude < 0.01f)
                {
                    tgtFwd = Vector3.Cross(upDir, Vector3.forward);
                    if (tgtFwd.sqrMagnitude < 0.01f)
                        tgtFwd = Vector3.Cross(upDir, Vector3.right);
                }
                baseDir = tgtFwd.normalized;
            }
            else
            {
                // Location: prefer landing on the side closest to player
                Vector3 playerToTarget = playerBody.GetPosition() - targetPos;
                playerToTarget = playerToTarget - Vector3.Project(playerToTarget, upDir);
                if (playerToTarget.sqrMagnitude < 0.1f)
                {
                    playerToTarget = Vector3.Cross(upDir, Vector3.forward);
                    if (playerToTarget.sqrMagnitude < 0.01f)
                        playerToTarget = Vector3.Cross(upDir, Vector3.right);
                }
                baseDir = playerToTarget.normalized;
            }

            // For locations, find the ground surface first (markers are
            // often inside terrain).
            if (isLocation)
            {
                Vector3 highStart = targetPos + upDir * 50f;
                if (Physics.SphereCast(highStart, TpProbeRadius, -upDir, out RaycastHit groundHit,
                    100f, OWLayerMask.physicalMask, QueryTriggerInteraction.Ignore))
                {
                    targetPos = groundHit.point;
                    probeCenterGround = targetPos;
                    // Update markerPos so fallback uses ground-adjusted position
                    markerPos = groundHit.point + upDir * TpFootClear;
                }
            }

            // Cache hazard sources for the three-layer detection below.
            // FindObjectsOfType is acceptable here (called once per TP, not per frame).
            _cachedCampfires = Object.FindObjectsOfType<Campfire>();
            _cachedHazardVolumes = Object.FindObjectsOfType<HazardVolume>();
            bool hasSuit = PlayerState.IsWearingSuit();
            DebugLogger.Log(LogCategory.State, "Teleport",
                $"[TP-DEBUG] Hazard cache: {_cachedCampfires?.Length ?? 0} campfires, {_cachedHazardVolumes?.Length ?? 0} hazard volumes");

            // Probe for safe ground around the interaction point.
            // Try expanding rings: 2 m, 4 m, 6 m offset.
            Vector3 bestPos = Vector3.zero;
            Vector3 bestOffsetDir = baseDir;
            bool foundSafe = false;
            bool rejectedDarkMatter = false;

            float tpProbeUp     = targetIsShip ? 8f  : TpProbeUp;
            float tpProbeLength = targetIsShip ? 16f : TpProbeLength;

            float[] offsetRings = targetIsShip
                ? new float[] { 12f }
                : new float[] { TpOffset, TpOffset * 2f, TpOffset * 3f };

            for (int ring = 0; ring < offsetRings.Length && !foundSafe; ring++)
            {
                float tpOffset = offsetRings[ring];

            int candidates = TpCandidates;
            for (int i = 0; i < candidates; i++)
            {
                float angle = i * (360f / TpCandidates);
                Vector3 offsetDir = Quaternion.AngleAxis(angle, upDir) * baseDir;
                Vector3 probeStart = probeCenterGround + offsetDir * tpOffset + upDir * tpProbeUp;

                // Multi-hit SphereCast — lets us run the game's 3-step
                // grounding logic (single normal, any-hit normal, paired normals)
                // instead of rejecting on the closest hit alone.
                int hitCount = Physics.SphereCastNonAlloc(
                    probeStart, TpProbeRadius, -upDir, _tpCastBuffer,
                    tpProbeLength - TpProbeRadius,
                    OWLayerMask.physicalMask, QueryTriggerInteraction.Ignore);

                // Find closest valid hit (skip player rigidbody)
                RaycastHit hit = default;
                float bestDist = float.MaxValue;
                bool foundAny = false;
                for (int h = 0; h < hitCount; h++)
                {
                    var rh = _tpCastBuffer[h];
                    if (rh.collider == null) continue;
                    if (rh.collider.GetComponentInParent<PlayerCharacterController>() != null) continue;
                    if (rh.distance < bestDist)
                    {
                        bestDist = rh.distance;
                        hit = rh;
                        foundAny = true;
                    }
                }

                if (!foundAny)
                {
                    DebugLogger.Log(LogCategory.State, "Teleport",
                        $"[TP-PROBE] ring={ring} i={i} angle={angle:F0}° offset={tpOffset}m NO_GROUND");
                    continue;
                }

                // Step 1: single-normal check on closest hit
                float slopeAngle = Vector3.Angle(upDir, hit.normal);
                bool accepted = slopeAngle <= TpMaxSlope;
                string acceptReason = accepted ? "WALK" : null;

                // Step 2: try any valid hit with normal ≤ 45°
                if (!accepted)
                {
                    for (int h = 0; h < hitCount && !accepted; h++)
                    {
                        var rh = _tpCastBuffer[h];
                        if (rh.collider == null) continue;
                        if (rh.collider.GetComponentInParent<PlayerCharacterController>() != null) continue;
                        float alt = Vector3.Angle(upDir, rh.normal);
                        if (alt <= TpMaxSlope)
                        {
                            hit = rh;
                            slopeAngle = alt;
                            accepted = true;
                            acceptReason = "WALK_ALT";
                        }
                    }
                }

                // Step 3: multi-slope paired-normal check (game's 115° rule)
                if (!accepted)
                {
                    int validN = 0;
                    for (int h = 0; h < hitCount && validN < _tpProjectedNormals.Length; h++)
                    {
                        var rh = _tpCastBuffer[h];
                        if (rh.collider == null) continue;
                        if (rh.collider.GetComponentInParent<PlayerCharacterController>() != null) continue;
                        _tpProjectedNormals[validN] = Vector3.ProjectOnPlane(rh.normal, upDir);
                        validN++;
                    }

                    for (int k = 0; k < validN && !accepted; k++)
                    {
                        for (int l = k + 1; l < validN; l++)
                        {
                            if (Vector3.Angle(_tpProjectedNormals[k], _tpProjectedNormals[l]) > TpMaxSlopePair)
                            {
                                accepted = true;
                                acceptReason = "WALK_MULTISLOPE";
                                break;
                            }
                        }
                    }
                }

                if (!accepted)
                {
                    DebugLogger.Log(LogCategory.State, "Teleport",
                        $"[TP-PROBE] ring={ring} i={i} angle={angle:F0}° offset={tpOffset}m STEEP={slopeAngle:F0}°");
                    continue;
                }

                Vector3 landingPoint = hit.point + upDir * TpFootClear;

                // ── Hazard detection at landing point ────────────────
                // Three-layer approach:
                //  1. OverlapSphere (catches active collider volumes)
                //  2. Campfire heat cone (geometric, independent of collider state)
                //  3. HazardVolume penetration (bypasses disabled colliders)
                bool hasDarkMatter = false;
                bool hasHazard = false;
                string hazardReason = "";

                // Layer 1: OverlapSphere (fast, catches most active volumes)
                var cols = Physics.OverlapSphere(
                    landingPoint, 1.5f,
                    OWLayerMask.effectVolumeMask, QueryTriggerInteraction.Collide);
                if (cols != null)
                {
                    for (int c = 0; c < cols.Length; c++)
                    {
                        var hv = cols[c].GetComponent<HazardVolume>();
                        if (hv != null)
                        {
                            var hvType = hv.GetHazardType();
                            if (hvType == HazardVolume.HazardType.DARKMATTER)
                            { hasDarkMatter = true; hazardReason = "overlap_darkmatter"; break; }
                            if (hvType == HazardVolume.HazardType.ELECTRICITY)
                            { hasHazard = true; hazardReason = "overlap_ELECTRICITY"; break; }
                            // RAPIDS/SANDFALL: environmental, safe on ground
                            if (hvType == HazardVolume.HazardType.RAPIDS
                                || hvType == HazardVolume.HazardType.SANDFALL)
                                continue;
                            // HEAT/FIRE/GENERAL: suit protects
                            if (!hasSuit)
                            { hasHazard = true; hazardReason = "overlap_" + hvType; break; }
                            continue;
                        }

                        var fv = cols[c].GetComponent<FluidVolume>();
                        if (fv != null)
                        {
                            var fType = fv.GetFluidType();
                            // AIR, TRACTOR_BEAM, WATER, CLOUD: always safe
                            // (player lands on solid ground, not inside the fluid)
                            if (fType == FluidVolume.Type.AIR
                                || fType == FluidVolume.Type.TRACTOR_BEAM
                                || fType == FluidVolume.Type.WATER
                                || fType == FluidVolume.Type.CLOUD)
                                continue;
                            // SAND: suit protects
                            if (fType == FluidVolume.Type.SAND && hasSuit)
                                continue;
                            hasHazard = true;
                            hazardReason = "overlap_fluid_" + fType;
                            break;
                        }
                    }
                }

                // Layer 2: Campfire proximity + heat check.
                // Proximity < 1.5m: always blocked (player would land IN the fire).
                // Heat cone 1.5-4m: blocked only without suit (suit protects from heat).
                if (!hasDarkMatter && !hasHazard && _cachedCampfires != null)
                {
                    for (int cf = 0; cf < _cachedCampfires.Length; cf++)
                    {
                        var campfire = _cachedCampfires[cf];
                        if (campfire == null) continue;
                        if (campfire.GetState() != Campfire.State.LIT) continue;

                        float cfDist = Vector3.Distance(
                            campfire.transform.position, landingPoint);

                        if (cfDist > 4f) continue;

                        // Direct proximity — always dangerous (inside the fire)
                        if (cfDist < 1.5f)
                        {
                            hasHazard = true;
                            hazardReason = $"campfire_proximity={cfDist:F1}m";
                            break;
                        }

                        // Heat cone 1.5-4m — suit protects
                        if (!hasSuit)
                        {
                            float heat = campfire.GetHeatAtPosition(landingPoint);
                            if (heat > 0f)
                            {
                                hasHazard = true;
                                hazardReason = $"campfire_heat={heat:F1}_dist={cfDist:F1}m";
                                break;
                            }
                        }
                    }
                }

                // Layer 3: HazardVolume penetration check (bypasses
                // disabled colliders — catches volumes that OverlapSphere misses).
                // Same suit-aware logic as Layer 1.
                if (!hasDarkMatter && !hasHazard && _cachedHazardVolumes != null)
                {
                    for (int hvi = 0; hvi < _cachedHazardVolumes.Length; hvi++)
                    {
                        var hv = _cachedHazardVolumes[hvi];
                        if (hv == null) continue;

                        var htype = hv.GetHazardType();
                        // Environmental mega-volumes: always safe on ground
                        if (htype == HazardVolume.HazardType.RAPIDS
                            || htype == HazardVolume.HazardType.SANDFALL)
                            continue;
                        // Suit-survivable hazards: skip when wearing suit
                        if (hasSuit
                            && htype != HazardVolume.HazardType.DARKMATTER
                            && htype != HazardVolume.HazardType.ELECTRICITY)
                            continue;

                        float hvDist = Vector3.Distance(
                            hv.transform.position, landingPoint);
                        if (hvDist > 10f) continue;

                        try
                        {
                            var trigVol = hv.GetOWTriggerVolume();
                            if (trigVol == null) continue;
                            float pen = trigVol.GetPenetrationDistance(landingPoint);
                            if (pen > -0.5f)
                            {
                                if (htype == HazardVolume.HazardType.DARKMATTER)
                                { hasDarkMatter = true; hazardReason = $"penetration_darkmatter_pen={pen:F2}"; }
                                else
                                { hasHazard = true; hazardReason = $"penetration_{htype}_pen={pen:F2}"; }
                                break;
                            }
                        }
                        catch { }
                    }
                }

                // Ghost matter: always blocked
                if (hasDarkMatter)
                {
                    DebugLogger.Log(LogCategory.State, "Teleport",
                        $"[TP-PROBE] ring={ring} i={i} angle={angle:F0}° offset={tpOffset}m DARK_MATTER reason={hazardReason}");
                    rejectedDarkMatter = true; continue;
                }
                // Lethal or suit-unprotected hazard — blocked
                if (hasHazard)
                {
                    DebugLogger.Log(LogCategory.State, "Teleport",
                        $"[TP-PROBE] ring={ring} i={i} angle={angle:F0}° offset={tpOffset}m HAZARD suit={hasSuit} reason={hazardReason}");
                    continue;
                }

                DebugLogger.Log(LogCategory.State, "Teleport",
                    $"[TP-PROBE] ring={ring} i={i} angle={angle:F0}° offset={tpOffset}m SAFE({acceptReason}) land=({landingPoint.x:F1},{landingPoint.y:F1},{landingPoint.z:F1})");

                bestPos = landingPoint;
                bestOffsetDir = offsetDir;
                foundSafe = true;
                break;
            }
            } // end ring loop

            if (!foundSafe)
            {
                if (rejectedDarkMatter)
                {
                    ScreenReader.Say(Loc.Get("teleport_dark_matter"));
                    return;
                }

                // With suit: fallback for locations only (trust marker).
                // For other targets (NPCs, interactables): if all 3 rings
                // failed, the area is genuinely dangerous — don't drop
                // the player from above into an unknown hazard.
                if (PlayerState.IsWearingSuit() && isLocation)
                {
                    bestPos = markerPos;
                    bestOffsetDir = baseDir;
                    foundSafe = true;
                }
                else
                {
                    ScreenReader.Say(Loc.Get("teleport_hazard_blocked"));
                    return;
                }
            }

            // Face toward the interaction point (collider center),
            // not just toward the target transform.
            Vector3 aimPoint = (targetCol != null)
                ? targetCol.bounds.center
                : target.position;
            Vector3 faceDir = aimPoint - bestPos;
            faceDir = faceDir - Vector3.Project(faceDir, upDir);
            if (faceDir.sqrMagnitude < 0.01f)
                faceDir = -bestOffsetDir;
            faceDir = faceDir.normalized;

            Quaternion targetRot = Quaternion.LookRotation(faceDir, upDir);

            WarpHelper.WarpAndMatchVelocity(
                playerBody, bestPos, targetRot,
                groundBody, Vector3.zero);

            // Kill any residual angular velocity to prevent tumbling on arrival
            playerBody.SetAngularVelocity(Vector3.zero);

            ScreenReader.Say(Loc.Get("teleport_success", targetName));
            DebugLogger.Log(LogCategory.State, "Teleport",
                $"Teleported to {targetName} ({Mathf.RoundToInt(dist)}m)");

            // Start alignment + sweep toward the target
            _autoWalkHandler?.StartAlignment(target, targetName, isInteractable);
        }

        /// <summary>
        /// Teleport the player into the ship's tractor beam fluid volume
        /// after manually arming it. The beam then lifts the player up
        /// through the HatchController airlock trigger, which fires the
        /// game's own "EnterShip" event so all state stays consistent
        /// (cabin oxygen, audio, beam auto-deactivation, etc.).
        /// Returns false if the beam component cannot be found or is
        /// non-functional (ship system failure), so the caller can
        /// fall back to the regular ground-probe logic.
        /// </summary>
        private bool TryTeleportToShipViaBeam(OWRigidbody playerBody, string targetName)
        {
            try
            {
                Transform shipTr = Locator.GetShipTransform();
                if (shipTr == null) return false;

                ShipTractorBeamSwitch beamSwitch =
                    shipTr.GetComponentInChildren<ShipTractorBeamSwitch>(true);
                if (beamSwitch == null)
                {
                    DebugLogger.Log(LogCategory.State, "Teleport",
                        "Ship tractor beam switch not found");
                    return false;
                }

                // Read private _functional flag — if the ship has suffered
                // a system failure the beam is permanently dead and we
                // must not warp the player into a non-existent column.
                var functionalField = typeof(ShipTractorBeamSwitch).GetField(
                    "_functional",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (functionalField != null)
                {
                    object val = functionalField.GetValue(beamSwitch);
                    if (val is bool b && !b)
                    {
                        DebugLogger.Log(LogCategory.State, "Teleport",
                            "Tractor beam not functional (ship system failure)");
                        return false;
                    }
                }

                // Pull the private _beamFluid reference. Its transform sits
                // inside the aspiration column under the open hatch.
                var fluidField = typeof(ShipTractorBeamSwitch).GetField(
                    "_beamFluid",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (fluidField == null) return false;

                FluidVolume beamFluid = fluidField.GetValue(beamSwitch) as FluidVolume;
                if (beamFluid == null || beamFluid.transform == null) return false;

                // Open the hatch programmatically. The hatch GameObject is a
                // physical obstruction when closed; if we don't open it the
                // beam can't lift the player through. OpenHatch() is private,
                // so reflection. The HatchController is on a separate
                // GameObject from the beam switch.
                HatchController hatch = shipTr.GetComponentInChildren<HatchController>(true);
                if (hatch != null)
                {
                    var openMethod = typeof(HatchController).GetMethod(
                        "OpenHatch",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (openMethod != null)
                        openMethod.Invoke(hatch, null);
                }

                // Arm the beam fluid so it's active when the player arrives.
                beamSwitch.ActivateTractorBeam();

                // Compute warp position INSIDE the beam capsule:
                //   - beamFluid.transform.position is the hatch end (pull
                //     destination) — warping there lands inside the cabin.
                //   - The capsule extends along +beamFluid.transform.up away
                //     from the ship (radius 2m, height 20m).
                //   - We warp to position + up * 8m: well inside the capsule
                //     (so OnTriggerStay catches the FluidDetector immediately)
                //     and ~8m below the hatch so the player has time to feel
                //     the lift.
                OWRigidbody shipBody = Locator.GetShipBody();
                Vector3 beamUp = beamFluid.transform.up;
                Vector3 warpPos = beamFluid.transform.position + beamUp * 8f;

                // Face the player upward toward the ship (opposite of beamUp,
                // since beamUp points away from the cabin). Use ship forward
                // as the look direction so the player isn't disoriented on
                // arrival in the cabin.
                Vector3 playerUpAfterWarp = -beamUp;
                Quaternion warpRot = Quaternion.LookRotation(
                    shipTr.forward, playerUpAfterWarp);

                WarpHelper.WarpAndMatchVelocity(
                    playerBody, warpPos, warpRot,
                    shipBody, Vector3.zero);

                playerBody.SetAngularVelocity(Vector3.zero);

                ScreenReader.Say(Loc.Get("teleport_success", targetName));
                DebugLogger.Log(LogCategory.State, "Teleport",
                    $"Teleported to {targetName} via tractor beam");
                return true;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log(LogCategory.State, "Teleport",
                    $"Beam teleport failed: {ex.Message}");
                return false;
            }
        }

        private void AnnounceLoopTimer()
        {
            try
            {
                float remaining = TimeLoop.GetSecondsRemaining();
                if (remaining <= 0f)
                {
                    ScreenReader.Say(Loc.Get("timer_expired"));
                    return;
                }
                int minutes = (int)(remaining / 60f);
                int seconds = (int)(remaining % 60f);
                ScreenReader.Say(Loc.Get("timer_remaining", minutes, seconds));
            }
            catch
            {
                ScreenReader.Say(Loc.Get("timer_unavailable"));
            }
        }

        #endregion
    }
}
