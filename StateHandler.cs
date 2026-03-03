namespace OuterWildsAccess
{
    /// <summary>
    /// Announces player state changes via GlobalMessenger events.
    /// Covers: death, respawn, ship, equipment, map, time, environment.
    /// All announcements are always active — events fire and announce via ScreenReader.
    /// </summary>
    public class StateHandler
    {
        /// <summary>
        /// Subscribes to all GlobalMessenger state events.
        /// Call from Main.Start().
        /// </summary>
        public void Initialize()
        {
            // Death and respawn
            GlobalMessenger<DeathType>.AddListener("PlayerDeath", OnPlayerDeath);
            GlobalMessenger.AddListener("PlayerResurrection", OnPlayerResurrection);

            // Ship
            GlobalMessenger.AddListener("EnterShip", OnEnterShip);
            GlobalMessenger.AddListener("ExitShip", OnExitShip);
            GlobalMessenger<OWRigidbody>.AddListener("EnterFlightConsole", OnEnterFlightConsole);
            GlobalMessenger.AddListener("ExitFlightConsole", OnExitFlightConsole);
            // ShipHullBreach removed — ShipPilotHandler announces specific hull part damage
            GlobalMessenger.AddListener("EnterShipComputer", OnEnterShipComputer);
            GlobalMessenger.AddListener("ExitShipComputer", OnExitShipComputer);
            GlobalMessenger.AddListener("EnterLandingView", OnEnterLandingView);
            GlobalMessenger.AddListener("ExitLandingView", OnExitLandingView);

            // Equipment
            GlobalMessenger.AddListener("SuitUp", OnSuitUp);
            GlobalMessenger.AddListener("RemoveSuit", OnRemoveSuit);
            GlobalMessenger.AddListener("TurnOnFlashlight", OnFlashlightOn);
            GlobalMessenger.AddListener("TurnOffFlashlight", OnFlashlightOff);

            // Map
            GlobalMessenger.AddListener("EnterMapView", OnEnterMap);
            GlobalMessenger.AddListener("ExitMapView", OnExitMap);

            // Time
            GlobalMessenger.AddListener("StartFastForward", OnFastForwardStart);
            GlobalMessenger.AddListener("EndFastForward", OnFastForwardEnd);

            // Conversation
            GlobalMessenger.AddListener("EnterConversation", OnEnterConversation);
            GlobalMessenger.AddListener("ExitConversation", OnExitConversation);

            // Tools (equip/unequip)
            GlobalMessenger<Signalscope>.AddListener("EquipSignalscope", OnEquipSignalscope);
            GlobalMessenger.AddListener("UnequipSignalscope", OnUnequipSignalscope);
            GlobalMessenger.AddListener("EquipTranslator", OnEquipTranslator);
            GlobalMessenger.AddListener("UnequipTranslator", OnUnequipTranslator);

            // Signalscope zoom
            GlobalMessenger<Signalscope>.AddListener("EnterSignalscopeZoom", OnEnterSignalscope);
            GlobalMessenger.AddListener("ExitSignalscopeZoom", OnExitSignalscope);

            // Water, attachment, undertow
            GlobalMessenger<float>.AddListener("PlayerCameraEnterWater", OnCameraEnterWater);
            GlobalMessenger<OWRigidbody>.AddListener("AttachPlayerToPoint", OnAttachToPoint);
            GlobalMessenger.AddListener("DetachPlayerFromPoint", OnDetachFromPoint);
            GlobalMessenger.AddListener("PlayerEnterUndertowVolume", OnEnterUndertow);
            GlobalMessenger.AddListener("PlayerExitUndertowVolume", OnExitUndertow);

            // Environment
            GlobalMessenger.AddListener("EnterDarkZone", OnEnterDarkZone);
            GlobalMessenger.AddListener("ExitDarkZone", OnExitDarkZone);
            GlobalMessenger.AddListener("EnterDreamWorld", OnEnterDreamWorld);
            GlobalMessenger.AddListener("ExitDreamWorld", OnExitDreamWorld);
            GlobalMessenger.AddListener("PlayerGrabbedByGhost", OnPlayerGrabbedByGhost);
            GlobalMessenger.AddListener("PlayerReleasedByGhost", OnPlayerReleasedByGhost);

            DebugLogger.Log(LogCategory.State, "StateHandler", "Initialisé");
        }

        /// <summary>
        /// Unsubscribes from all events.
        /// Call from Main.OnDestroy().
        /// </summary>
        public void Cleanup()
        {
            GlobalMessenger<DeathType>.RemoveListener("PlayerDeath", OnPlayerDeath);
            GlobalMessenger.RemoveListener("PlayerResurrection", OnPlayerResurrection);

            GlobalMessenger.RemoveListener("EnterShip", OnEnterShip);
            GlobalMessenger.RemoveListener("ExitShip", OnExitShip);
            GlobalMessenger<OWRigidbody>.RemoveListener("EnterFlightConsole", OnEnterFlightConsole);
            GlobalMessenger.RemoveListener("ExitFlightConsole", OnExitFlightConsole);
            // ShipHullBreach removed — ShipPilotHandler handles hull damage
            GlobalMessenger.RemoveListener("EnterShipComputer", OnEnterShipComputer);
            GlobalMessenger.RemoveListener("ExitShipComputer", OnExitShipComputer);
            GlobalMessenger.RemoveListener("EnterLandingView", OnEnterLandingView);
            GlobalMessenger.RemoveListener("ExitLandingView", OnExitLandingView);

            GlobalMessenger.RemoveListener("SuitUp", OnSuitUp);
            GlobalMessenger.RemoveListener("RemoveSuit", OnRemoveSuit);
            GlobalMessenger.RemoveListener("TurnOnFlashlight", OnFlashlightOn);
            GlobalMessenger.RemoveListener("TurnOffFlashlight", OnFlashlightOff);

            GlobalMessenger.RemoveListener("EnterMapView", OnEnterMap);
            GlobalMessenger.RemoveListener("ExitMapView", OnExitMap);

            GlobalMessenger.RemoveListener("StartFastForward", OnFastForwardStart);
            GlobalMessenger.RemoveListener("EndFastForward", OnFastForwardEnd);

            GlobalMessenger.RemoveListener("EnterConversation", OnEnterConversation);
            GlobalMessenger.RemoveListener("ExitConversation", OnExitConversation);

            GlobalMessenger<Signalscope>.RemoveListener("EquipSignalscope", OnEquipSignalscope);
            GlobalMessenger.RemoveListener("UnequipSignalscope", OnUnequipSignalscope);
            GlobalMessenger.RemoveListener("EquipTranslator", OnEquipTranslator);
            GlobalMessenger.RemoveListener("UnequipTranslator", OnUnequipTranslator);

            GlobalMessenger<Signalscope>.RemoveListener("EnterSignalscopeZoom", OnEnterSignalscope);
            GlobalMessenger.RemoveListener("ExitSignalscopeZoom", OnExitSignalscope);

            GlobalMessenger<float>.RemoveListener("PlayerCameraEnterWater", OnCameraEnterWater);
            GlobalMessenger<OWRigidbody>.RemoveListener("AttachPlayerToPoint", OnAttachToPoint);
            GlobalMessenger.RemoveListener("DetachPlayerFromPoint", OnDetachFromPoint);
            GlobalMessenger.RemoveListener("PlayerEnterUndertowVolume", OnEnterUndertow);
            GlobalMessenger.RemoveListener("PlayerExitUndertowVolume", OnExitUndertow);

            GlobalMessenger.RemoveListener("EnterDarkZone", OnEnterDarkZone);
            GlobalMessenger.RemoveListener("ExitDarkZone", OnExitDarkZone);
            GlobalMessenger.RemoveListener("EnterDreamWorld", OnEnterDreamWorld);
            GlobalMessenger.RemoveListener("ExitDreamWorld", OnExitDreamWorld);
            GlobalMessenger.RemoveListener("PlayerGrabbedByGhost", OnPlayerGrabbedByGhost);
            GlobalMessenger.RemoveListener("PlayerReleasedByGhost", OnPlayerReleasedByGhost);
        }

        #region Death and Respawn

        private void OnPlayerDeath(DeathType deathType)
        {

            string key = "death_" + deathType.ToString().ToLower();
            DebugLogger.Log(LogCategory.State, "StateHandler", $"Mort: {deathType}");
            ScreenReader.Say(Loc.Get(key), SpeechPriority.Now);
        }

        private void OnPlayerResurrection()
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "Respawn");
            ScreenReader.Say(Loc.Get("player_respawn"), SpeechPriority.Now);
        }

        #endregion

        #region Ship

        private void OnEnterShip()
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "EnterShip");
            ScreenReader.Say(Loc.Get("enter_ship"));
        }

        private void OnExitShip()
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "ExitShip");
            ScreenReader.Say(Loc.Get("exit_ship"));
        }

        private void OnEnterFlightConsole(OWRigidbody _)
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "EnterFlightConsole");
            ScreenReader.Say(Loc.Get("enter_flight_console"));
        }

        private void OnExitFlightConsole()
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "ExitFlightConsole");
            ScreenReader.Say(Loc.Get("exit_flight_console"));
        }

        private void OnEnterShipComputer()
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "EnterShipComputer");
            ScreenReader.Say(Loc.Get("enter_ship_computer"));
        }

        private void OnExitShipComputer()
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "ExitShipComputer");
            ScreenReader.Say(Loc.Get("exit_ship_computer"));
        }

        private void OnEnterLandingView()
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "EnterLandingView");
            ScreenReader.Say(Loc.Get("enter_landing_view"));
        }

        private void OnExitLandingView()
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "ExitLandingView");
            ScreenReader.Say(Loc.Get("exit_landing_view"));
        }

        #endregion

        #region Equipment

        private void OnSuitUp()
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "SuitUp");
            ScreenReader.Say(Loc.Get("suit_on"));
        }

        private void OnRemoveSuit()
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "RemoveSuit");
            ScreenReader.Say(Loc.Get("suit_off"));
        }

        private void OnFlashlightOn()
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "FlashlightOn");
            ScreenReader.Say(Loc.Get("flashlight_on"));
        }

        private void OnFlashlightOff()
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "FlashlightOff");
            ScreenReader.Say(Loc.Get("flashlight_off"));
        }

        #endregion

        #region Map

        private void OnEnterMap()
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "EnterMap");
            ScreenReader.Say(Loc.Get("enter_map"));
        }

        private void OnExitMap()
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "ExitMap");
            ScreenReader.Say(Loc.Get("exit_map"));
        }

        #endregion

        #region Time

        private void OnFastForwardStart()
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "FastForwardStart");
            ScreenReader.Say(Loc.Get("fast_forward_start"));
        }

        private void OnFastForwardEnd()
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "FastForwardEnd");
            ScreenReader.Say(Loc.Get("fast_forward_end"));
        }

        #endregion

        #region Conversation

        private void OnEnterConversation()
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "EnterConversation");
            ScreenReader.Say(Loc.Get("enter_conversation"));
        }

        private void OnExitConversation()
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "ExitConversation");
            ScreenReader.Say(Loc.Get("exit_conversation"));
        }

        #endregion

        #region Tools (equip/unequip)

        private void OnEquipSignalscope(Signalscope _)
        {
            DebugLogger.Log(LogCategory.State, "StateHandler", "EquipSignalscope");
            ScreenReader.Say(Loc.Get("equip_signalscope"));
        }

        private void OnUnequipSignalscope()
        {
            DebugLogger.Log(LogCategory.State, "StateHandler", "UnequipSignalscope");
            ScreenReader.Say(Loc.Get("unequip_signalscope"));
        }

        private void OnEquipTranslator()
        {
            DebugLogger.Log(LogCategory.State, "StateHandler", "EquipTranslator");
            ScreenReader.Say(Loc.Get("equip_translator"));
        }

        private void OnUnequipTranslator()
        {
            DebugLogger.Log(LogCategory.State, "StateHandler", "UnequipTranslator");
            ScreenReader.Say(Loc.Get("unequip_translator"));
        }

        #endregion

        #region Signalscope zoom

        private void OnEnterSignalscope(Signalscope _)
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "EnterSignalscope");
            ScreenReader.Say(Loc.Get("enter_signalscope"));
        }

        private void OnExitSignalscope()
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "ExitSignalscope");
            ScreenReader.Say(Loc.Get("exit_signalscope"));
        }

        #endregion

        #region Water, attachment, undertow

        private void OnCameraEnterWater(float _)
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "CameraEnterWater");
            ScreenReader.Say(Loc.Get("camera_enter_water"));
        }

        private void OnAttachToPoint(OWRigidbody _)
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "AttachToPoint");
            ScreenReader.Say(Loc.Get("attach_to_point"));
        }

        private void OnDetachFromPoint()
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "DetachFromPoint");
            ScreenReader.Say(Loc.Get("detach_from_point"));
        }

        private void OnEnterUndertow()
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "EnterUndertow");
            ScreenReader.Say(Loc.Get("enter_undertow"));
        }

        private void OnExitUndertow()
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "ExitUndertow");
            ScreenReader.Say(Loc.Get("exit_undertow"));
        }

        #endregion

        #region Environment

        private void OnEnterDarkZone()
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "EnterDarkZone");
            ScreenReader.Say(Loc.Get("enter_dark_zone"));
        }

        private void OnExitDarkZone()
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "ExitDarkZone");
            ScreenReader.Say(Loc.Get("exit_dark_zone"));
        }

        private void OnEnterDreamWorld()
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "EnterDreamWorld");
            ScreenReader.Say(Loc.Get("enter_dream_world"));
        }

        private void OnExitDreamWorld()
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "ExitDreamWorld");
            ScreenReader.Say(Loc.Get("exit_dream_world"));
        }

        private void OnPlayerGrabbedByGhost()
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "GrabbedByGhost");
            ScreenReader.Say(Loc.Get("player_grabbed_ghost"), SpeechPriority.Now);
        }

        private void OnPlayerReleasedByGhost()
        {

            DebugLogger.Log(LogCategory.State, "StateHandler", "ReleasedByGhost");
            ScreenReader.Say(Loc.Get("player_released_ghost"));
        }

        #endregion
    }
}
