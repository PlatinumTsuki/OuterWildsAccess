using UnityEngine;

namespace OuterWildsAccess
{
    /// <summary>
    /// Announces ship flight telemetry via screen reader:
    /// speed tier changes, altitude milestones, approach warnings,
    /// hull/component damage, and full status on I.
    ///
    /// Approach:
    ///   - Events (GlobalMessenger): ShipLiftoff, TargetReferenceFrame, UntargetReferenceFrame
    ///   - Events (C#): ShipDamageController.OnShipHullDamaged, OnShipComponentDamaged (lazy hook)
    ///   - Polling (1.5s): speed tier, altitude milestones, approach speed
    ///   - Manual: I reads full flight status on demand
    ///

    /// </summary>
    public class ShipPilotHandler
    {
        #region Constants

        private const float CheckInterval = 1.5f;

        // Speed tier boundaries (m/s)
        private const float SpeedSlow     = 5f;
        private const float SpeedModerate = 50f;
        private const float SpeedFast     = 200f;
        private const float SpeedVeryFast = 1000f;

        // Altitude milestones (metres, descending)
        private static readonly int[] AltitudeMilestones = { 500, 200, 100, 50, 20, 10 };

        // Approach speed thresholds (m/s)
        private const float ApproachWarning = 50f;
        private const float ApproachDanger  = 150f;
        private const float ApproachCooldown = 5f;

        #endregion

        #region State

        private float _nextCheck;

        // Speed tracking
        private int _lastSpeedTier = -1; // -1 = uninitialized (no announce on first poll)
        private bool _firstSpeedPoll = true;

        // Altitude tracking
        private int _lastAltitudeMilestoneIndex = -1; // index into AltitudeMilestones
        private bool _firstAltitudePoll = true;

        // Approach tracking
        private float _lastApproachAlertTime;

        // Reference frame tracking
        private string _lastRefFrameName;

        // Altimeter state tracking
        private bool _lastAltimeterActive;
        private bool _firstAltimeterPoll = true;

        // Damage event hook
        private ShipDamageController _damageCtrl;
        private bool _eventsHooked;

        #endregion

        #region Lifecycle

        /// <summary>Subscribes to GlobalMessenger events. Call from Main.Start().</summary>
        public void Initialize()
        {
            GlobalMessenger.AddListener("ShipLiftoff", OnShipLiftoff);
            GlobalMessenger<ReferenceFrame>.AddListener("TargetReferenceFrame", OnTargetReferenceFrame);
            GlobalMessenger.AddListener("UntargetReferenceFrame", OnUntargetReferenceFrame);
            DebugLogger.Log(LogCategory.State, "ShipPilotHandler", "Initialized");
        }

        /// <summary>Unsubscribes from all events. Call from Main.OnDestroy().</summary>
        public void Cleanup()
        {
            GlobalMessenger.RemoveListener("ShipLiftoff", OnShipLiftoff);
            GlobalMessenger<ReferenceFrame>.RemoveListener("TargetReferenceFrame", OnTargetReferenceFrame);
            GlobalMessenger.RemoveListener("UntargetReferenceFrame", OnUntargetReferenceFrame);
            UnhookDamageEvents();
        }

        /// <summary>Resets all state on scene load. Call from Main.OnSceneLoaded().</summary>
        public void OnSceneLoaded()
        {
            _lastSpeedTier = -1;
            _firstSpeedPoll = true;
            _lastAltitudeMilestoneIndex = -1;
            _firstAltitudePoll = true;
            _lastAltimeterActive = false;
            _firstAltimeterPoll = true;
            _lastApproachAlertTime = 0f;
            _lastRefFrameName = null;
            _nextCheck = 0f;
            UnhookDamageEvents();
            _damageCtrl = null;
        }

        /// <summary>Polls speed, altitude, and approach. Call from Main.Update().</summary>
        public void Update()
        {

            if (!PlayerState.AtFlightConsole()) return;
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + CheckInterval;

            EnsureDamageEventsHooked();
            PollSpeed();
            PollAltitude();
            PollApproachSpeed();
        }

        #endregion

        #region Event Handlers

        private void OnShipLiftoff()
        {

            if (!PlayerState.IsInsideShip()) return;

            ScreenReader.Say(Loc.Get("pilot_liftoff"));
            // Next altitude poll sets baseline silently (avoid "10m" while ascending)
            _firstAltitudePoll = true;
            DebugLogger.Log(LogCategory.State, "ShipPilotHandler", "Liftoff");
        }

        private void OnTargetReferenceFrame(ReferenceFrame refFrame)
        {

            if (!PlayerState.AtFlightConsole()) return;
            if (refFrame == null) return;

            string name = refFrame.GetHUDDisplayName();
            _lastRefFrameName = name;

            ScreenReader.Say(Loc.Get("pilot_approach_body", name));
            // Reset approach alert on new target
            _lastApproachAlertTime = 0f;
            DebugLogger.Log(LogCategory.State, "ShipPilotHandler", "Target: " + name);
        }

        private void OnUntargetReferenceFrame()
        {

            if (!PlayerState.AtFlightConsole()) return;

            _lastRefFrameName = null;
            ScreenReader.Say(Loc.Get("pilot_lost_target"));
            DebugLogger.Log(LogCategory.State, "ShipPilotHandler", "Target lost");
        }

        private void OnHullDamaged(ShipHull hull)
        {

            if (!PlayerState.IsInsideShip()) return;
            if (hull == null) return;

            string partName = TranslateUITextType(hull.hullName);
            ScreenReader.Say(Loc.Get("pilot_hull_damaged", partName), SpeechPriority.Now);
            DebugLogger.Log(LogCategory.State, "ShipPilotHandler",
                "Hull damaged: " + hull.hullName);
        }

        private void OnComponentDamaged(ShipComponent component)
        {

            if (!PlayerState.IsInsideShip()) return;
            if (component == null) return;

            string partName = TranslateUITextType(component.componentName);
            ScreenReader.Say(Loc.Get("pilot_component_damaged", partName), SpeechPriority.Now);
            DebugLogger.Log(LogCategory.State, "ShipPilotHandler",
                "Component damaged: " + component.componentName);
        }

        #endregion

        #region Polling

        private void PollSpeed()
        {
            OWRigidbody shipBody = null;
            try { shipBody = Locator.GetShipBody(); }
            catch { return; }

            float speed = GetRelativeSpeed(shipBody);
            if (speed < 0f) return;

            int tier = GetSpeedTier(speed);

            // Skip first poll to avoid "Stationary" on sit down
            if (_firstSpeedPoll)
            {
                _firstSpeedPoll = false;
                _lastSpeedTier = tier;
                return;
            }

            if (tier != _lastSpeedTier)
            {
                _lastSpeedTier = tier;
                string tierDesc = Loc.Get(GetSpeedTierKey(tier));
                ScreenReader.SayQueued(Loc.Get("pilot_speed", tierDesc, Mathf.RoundToInt(speed)));
                DebugLogger.Log(LogCategory.State, "ShipPilotHandler",
                    $"Speed tier changed: {tier}, speed={speed:F0}");
            }
        }

        private void PollAltitude()
        {
            float altitude = -1f;
            bool isActive = false;

            try
            {
                OWRigidbody shipBody = Locator.GetShipBody();
                if (shipBody == null) return;

                var altimeter = shipBody.GetComponentInChildren<ShipAltimeter>();
                if (altimeter == null) return;

                isActive = altimeter.AltimeterIsActive();

                // Announce altimeter state transitions
                if (_firstAltimeterPoll)
                {
                    _firstAltimeterPoll = false;
                    _lastAltimeterActive = isActive;
                }
                else if (isActive != _lastAltimeterActive)
                {
                    _lastAltimeterActive = isActive;
                    if (isActive)
                    {
                        ScreenReader.Say(Loc.Get("pilot_altimeter_on"));
                        // Reset altitude tracking on re-activation
                        _firstAltitudePoll = true;
                    }
                    else
                    {
                        ScreenReader.Say(Loc.Get("pilot_altimeter_off"));
                    }
                    DebugLogger.Log(LogCategory.State, "ShipPilotHandler",
                        "Altimeter " + (isActive ? "activated" : "deactivated"));
                }

                if (!isActive) return;

                altitude = altimeter.GetShipAltitudeAboveTerrain();
            }
            catch { return; }

            if (altitude < 0f) return;

            // Find which milestone we're at (descending: 500, 200, 100, 50, 20, 10)
            int currentIndex = -1;
            for (int i = 0; i < AltitudeMilestones.Length; i++)
            {
                if (altitude <= AltitudeMilestones[i])
                {
                    currentIndex = i;
                }
            }

            // First poll after liftoff/scene load — set baseline silently
            if (_firstAltitudePoll)
            {
                _firstAltitudePoll = false;
                _lastAltitudeMilestoneIndex = currentIndex;
                return;
            }

            // Only announce when crossing a new milestone going DOWN
            // currentIndex increases as altitude decreases
            if (currentIndex > _lastAltitudeMilestoneIndex)
            {
                _lastAltitudeMilestoneIndex = currentIndex;
                int milestone = AltitudeMilestones[currentIndex];
                ScreenReader.SayQueued(Loc.Get("pilot_altitude", milestone));
                DebugLogger.Log(LogCategory.State, "ShipPilotHandler",
                    $"Altitude milestone: {milestone}m (actual={altitude:F0}m)");
            }
            else if (currentIndex < _lastAltitudeMilestoneIndex)
            {
                // Going up — silently reset
                _lastAltitudeMilestoneIndex = currentIndex;
            }
        }

        private void PollApproachSpeed()
        {
            OWRigidbody shipBody = null;
            try { shipBody = Locator.GetShipBody(); }
            catch { return; }
            if (shipBody == null) return;

            ReferenceFrame rf = null;
            try { rf = Locator.GetReferenceFrame(); }
            catch { return; }
            if (rf == null || rf.GetOWRigidBody() == null) return;

            // Vector from ship to body
            Vector3 toBody = rf.GetOWRigidBody().GetPosition() - shipBody.GetPosition();
            float distance = toBody.magnitude;
            if (distance < 1f) return; // avoid division by zero

            Vector3 direction = toBody / distance;

            // Relative velocity
            Vector3 relVel = shipBody.GetVelocity() - rf.GetOWRigidBody().GetVelocity();

            // Approach speed = projection of relative velocity onto direction to body
            // Positive = approaching, negative = receding
            float approachSpeed = Vector3.Dot(relVel, direction);

            if (approachSpeed < ApproachWarning) return;
            if (Time.time - _lastApproachAlertTime < ApproachCooldown) return;

            _lastApproachAlertTime = Time.time;

            if (approachSpeed >= ApproachDanger)
            {
                ScreenReader.Say(Loc.Get("pilot_approach_danger", Mathf.RoundToInt(approachSpeed)));
                DebugLogger.Log(LogCategory.State, "ShipPilotHandler",
                    $"Approach DANGER: {approachSpeed:F0} m/s");
            }
            else
            {
                ScreenReader.Say(Loc.Get("pilot_approach_warning", Mathf.RoundToInt(approachSpeed)));
                DebugLogger.Log(LogCategory.State, "ShipPilotHandler",
                    $"Approach warning: {approachSpeed:F0} m/s");
            }
        }

        #endregion

        #region I — Full Flight Status

        /// <summary>
        /// Reads full flight status on demand.
        /// Called from Main.ProcessHotkeys() when I is pressed.
        /// </summary>
        public void ReadFlightStatus()
        {


            if (!PlayerState.AtFlightConsole())
            {
                ScreenReader.Say(Loc.Get("pilot_not_at_console"));
                return;
            }

            OWRigidbody shipBody = null;
            try { shipBody = Locator.GetShipBody(); }
            catch { }

            var sb = new System.Text.StringBuilder();

            // Speed
            if (shipBody != null)
            {
                float speed = GetRelativeSpeed(shipBody);
                if (speed >= 0f)
                {
                    string tierDesc = Loc.Get(GetSpeedTierKey(GetSpeedTier(speed)));
                    sb.Append(Loc.Get("pilot_status_speed", Mathf.RoundToInt(speed), tierDesc));
                    sb.Append(" ");
                }

                // Near body
                try
                {
                    ReferenceFrame rf = Locator.GetReferenceFrame();
                    if (rf != null)
                    {
                        string bodyName = rf.GetHUDDisplayName();
                        if (!string.IsNullOrEmpty(bodyName))
                        {
                            sb.Append(Loc.Get("pilot_status_near", bodyName));
                            sb.Append(" ");
                        }
                    }
                }
                catch { }
            }

            // Altitude
            if (shipBody != null)
            {
                try
                {
                    var altimeter = shipBody.GetComponentInChildren<ShipAltimeter>();
                    if (altimeter != null && altimeter.AltimeterIsActive())
                    {
                        float alt = altimeter.GetShipAltitudeAboveTerrain();
                        sb.Append(Loc.Get("pilot_status_altitude", Mathf.RoundToInt(alt)));
                        sb.Append(" ");
                    }
                }
                catch { }
            }

            // Damage
            try
            {
                ShipDamageController dmg = GetDamageController();
                if (dmg != null)
                {
                    if (dmg.IsHullBreached())
                    {
                        sb.Append(Loc.Get("pilot_status_hull_breach"));
                        sb.Append(" ");
                    }

                    if (dmg.IsReactorCritical())
                    {
                        sb.Append(Loc.Get("pilot_status_reactor_critical"));
                        sb.Append(" ");
                    }

                    if (dmg.IsElectricalFailed())
                    {
                        sb.Append(Loc.Get("pilot_status_electrical_fail"));
                        sb.Append(" ");
                    }

                    if (dmg.IsDamaged())
                    {
                        float integrity = dmg.GetLowestHullIntegrity();
                        sb.Append(Loc.Get("pilot_status_damaged", Mathf.RoundToInt(integrity * 100f)));
                        sb.Append(" ");
                    }
                    else
                    {
                        sb.Append(Loc.Get("pilot_status_no_damage"));
                        sb.Append(" ");
                    }
                }
            }
            catch { }

            // Landed
            if (shipBody != null)
            {
                try
                {
                    var landingMgr = shipBody.GetComponentInChildren<LandingPadManager>();
                    if (landingMgr != null && landingMgr.IsLanded())
                    {
                        sb.Append(Loc.Get("pilot_status_landed"));
                    }
                }
                catch { }
            }

            string result = sb.ToString().Trim();
            if (string.IsNullOrEmpty(result))
            {
                ScreenReader.Say(Loc.Get("pilot_unavailable"));
            }
            else
            {
                ScreenReader.Say(result);
            }

            DebugLogger.Log(LogCategory.State, "ShipPilotHandler", "I status: " + result);
        }

        #endregion

        #region Damage Event Hooking

        private void EnsureDamageEventsHooked()
        {
            if (_eventsHooked) return;

            ShipDamageController dmg = GetDamageController();
            if (dmg == null) return;

            _damageCtrl = dmg;
            dmg.OnShipHullDamaged += OnHullDamaged;
            dmg.OnShipComponentDamaged += OnComponentDamaged;
            _eventsHooked = true;

            DebugLogger.Log(LogCategory.State, "ShipPilotHandler", "Damage events hooked");
        }

        private void UnhookDamageEvents()
        {
            if (!_eventsHooked || _damageCtrl == null) return;

            try
            {
                _damageCtrl.OnShipHullDamaged -= OnHullDamaged;
                _damageCtrl.OnShipComponentDamaged -= OnComponentDamaged;
            }
            catch { }

            _eventsHooked = false;
        }

        private ShipDamageController GetDamageController()
        {
            try
            {
                OWRigidbody shipBody = Locator.GetShipBody();
                if (shipBody == null) return null;
                return shipBody.GetComponentInChildren<ShipDamageController>();
            }
            catch { return null; }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Returns the ship speed relative to the current reference frame,
        /// or absolute speed if no reference frame is available.
        /// Returns -1 if the ship body cannot be resolved.
        /// </summary>
        private static float GetRelativeSpeed(OWRigidbody shipBody)
        {
            if (shipBody == null) return -1f;

            try
            {
                ReferenceFrame rf = Locator.GetReferenceFrame();
                if (rf != null && rf.GetOWRigidBody() != null)
                {
                    Vector3 relVel = shipBody.GetVelocity() - rf.GetOWRigidBody().GetVelocity();
                    return relVel.magnitude;
                }
            }
            catch { }

            return shipBody.GetVelocity().magnitude;
        }

        private static int GetSpeedTier(float speed)
        {
            if (speed >= SpeedVeryFast) return 4;
            if (speed >= SpeedFast)     return 3;
            if (speed >= SpeedModerate) return 2;
            if (speed >= SpeedSlow)     return 1;
            return 0;
        }

        private static string GetSpeedTierKey(int tier)
        {
            switch (tier)
            {
                case 0: return "pilot_speed_stationary";
                case 1: return "pilot_speed_slow";
                case 2: return "pilot_speed_moderate";
                case 3: return "pilot_speed_fast";
                case 4: return "pilot_speed_very_fast";
                default: return "pilot_speed_stationary";
            }
        }

        private static string TranslateUITextType(UITextType type)
        {
            switch (type)
            {
                // Hull parts
                case UITextType.ShipPartTop:        return Loc.Get("pilot_part_top");
                case UITextType.ShipPartForward:    return Loc.Get("pilot_part_forward");
                case UITextType.ShipPartPort:       return Loc.Get("pilot_part_port");
                case UITextType.ShipPartLanding:    return Loc.Get("pilot_part_landing");
                case UITextType.ShipPartStarboard:  return Loc.Get("pilot_part_starboard");
                case UITextType.ShipPartAft:        return Loc.Get("pilot_part_aft");

                // Components
                case UITextType.ShipPartAutopilot:   return Loc.Get("pilot_part_autopilot");
                case UITextType.ShipPartFuel:        return Loc.Get("pilot_part_fuel");
                case UITextType.ShipPartGravity:     return Loc.Get("pilot_part_gravity");
                case UITextType.ShipPartLights:      return Loc.Get("pilot_part_lights");
                case UITextType.ShipPartCamera:      return Loc.Get("pilot_part_camera");
                case UITextType.ShipPartLeftThrust:  return Loc.Get("pilot_part_left_thrust");
                case UITextType.ShipPartElectric:    return Loc.Get("pilot_part_electric");
                case UITextType.ShipPartO2:          return Loc.Get("pilot_part_o2");
                case UITextType.ShipPartReactor:     return Loc.Get("pilot_part_reactor");
                case UITextType.ShipPartRightThrust: return Loc.Get("pilot_part_right_thrust");

                default: return type.ToString();
            }
        }

        #endregion
    }
}
