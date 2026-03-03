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

        // Shared pathfinding instance — used by AutoWalk + Guidance
        private PathScanner          _sharedPathScanner;

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

            _signalscopeHandler = new SignalscopeHandler();
            _signalscopeHandler.Initialize();

            _shipPilotHandler = new ShipPilotHandler();
            _shipPilotHandler.Initialize();

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
            _signalscopeHandler?.Cleanup();
            _shipPilotHandler?.Cleanup();
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

            // Delete — repeat last announcement
            if (Keyboard.current.deleteKey.wasPressedThisFrame)
            {
                DebugLogger.LogInput("Delete", "RepeatLast");
                ScreenReader.RepeatLast();
                return;
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

            // O — toggle auto-walk toward navigation target
            if (Keyboard.current.oKey.wasPressedThisFrame)
            {
                DebugLogger.LogInput("O", "AutoWalk toggle");
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

        /// <summary>
        /// Announces the time remaining in the current loop via screen reader.
        /// Uses TimeLoop.GetSecondsRemaining() — only available in gameplay scenes.
        /// </summary>
        private const float MaxTeleportDistance = 500f;

        private void TeleportToSelected()
        {
            if (PlayerState.AtFlightConsole())
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

            float dist = Vector3.Distance(playerBody.GetPosition(), target.position);
            if (dist > MaxTeleportDistance)
            {
                ScreenReader.Say(Loc.Get("teleport_too_far"));
                return;
            }

            // Calculate up direction at target (away from planet center)
            Vector3 targetPos = target.position;
            Vector3 upDir = playerBody.transform.up;
            OWRigidbody groundBody = null;
            try { groundBody = target.GetComponentInParent<OWRigidbody>(); }
            catch { }

            if (groundBody != null)
                upDir = (targetPos - groundBody.GetWorldCenterOfMass()).normalized;

            // Offset direction: 2m beside the target (from current player direction)
            Vector3 offsetDir = playerBody.GetPosition() - targetPos;
            offsetDir = offsetDir - Vector3.Project(offsetDir, upDir);  // horizontal only
            if (offsetDir.sqrMagnitude < 0.1f)
            {
                // Player is directly above/below — pick arbitrary horizontal direction
                offsetDir = Vector3.Cross(upDir, Vector3.forward);
                if (offsetDir.sqrMagnitude < 0.01f)
                    offsetDir = Vector3.Cross(upDir, Vector3.right);
            }
            offsetDir = offsetDir.normalized;

            // Teleport 2m beside target, then raycast down to find real ground
            Vector3 candidatePos = targetPos + offsetDir * 2f + upDir * 10f;
            RaycastHit hit;
            if (Physics.Raycast(candidatePos, -upDir, out hit, 20f,
                    OWLayerMask.physicalMask, QueryTriggerInteraction.Ignore))
                candidatePos = hit.point + upDir * 1.5f;
            Vector3 teleportPos = candidatePos;

            // Face toward the target
            Vector3 faceDir = -offsetDir;
            Quaternion faceRot = Quaternion.LookRotation(faceDir, upDir);

            WarpHelper.WarpAndMatchVelocity(
                playerBody, teleportPos, faceRot,
                groundBody, Vector3.zero);

            // Kill any residual angular velocity to prevent tumbling on arrival
            playerBody.SetAngularVelocity(Vector3.zero);

            ScreenReader.Say(Loc.Get("teleport_success", targetName));
            DebugLogger.Log(LogCategory.State, "Teleport",
                $"Teleported to {targetName} ({Mathf.RoundToInt(dist)}m)");

            // Start alignment + sweep toward the target
            _autoWalkHandler?.StartAlignment(target, targetName, isInteractable);
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
