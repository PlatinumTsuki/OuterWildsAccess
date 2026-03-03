using UnityEngine;

namespace OuterWildsAccess
{
    /// <summary>
    /// On-demand status readouts:
    ///   H — personal status (health, oxygen, jetpack fuel, boost, suit)
    ///   J — ship status (fuel, oxygen, hull integrity, damage)
    ///   K — environment (sector, hazards, gravity)
    /// </summary>
    public class StatusHandler
    {
        #region Cached references

        private PlayerResources _playerResources;
        private ShipResources _shipResources;
        private ShipDamageController _shipDamage;
        private HazardDetector _hazardDetector;
        private JetpackThrusterModel _jetpack;

        #endregion

        #region Lifecycle

        /// <summary>Resets cached references on scene load.</summary>
        public void OnSceneLoaded()
        {
            _playerResources = null;
            _shipResources = null;
            _shipDamage = null;
            _hazardDetector = null;
            _jetpack = null;
        }

        #endregion

        #region H — Personal status

        /// <summary>Announces health, oxygen, jetpack fuel, boost charge, suit state.</summary>
        public void ReadPersonalStatus()
        {
            EnsurePlayerRefs();
            if (_playerResources == null)
            {
                ScreenReader.Say(Loc.Get("status_unavailable"));
                return;
            }

            var sb = new System.Text.StringBuilder();

            // Health
            int health = Mathf.RoundToInt(_playerResources.GetHealth());
            sb.Append(Loc.Get("status_health", health));

            // Oxygen
            float oxySeconds = _playerResources.GetOxygenInSeconds();
            int oxyMin = Mathf.FloorToInt(oxySeconds / 60f);
            int oxySec = Mathf.FloorToInt(oxySeconds % 60f);
            if (oxyMin > 0)
                sb.Append(", ").Append(Loc.Get("status_oxygen_min", oxyMin, oxySec));
            else
                sb.Append(", ").Append(Loc.Get("status_oxygen_sec", oxySec));

            // Jetpack fuel
            int fuel = Mathf.RoundToInt(_playerResources.GetFuelFraction() * 100f);
            sb.Append(", ").Append(Loc.Get("status_jetpack", fuel));

            // Boost charge
            if (_jetpack != null)
            {
                int boost = Mathf.RoundToInt(_jetpack.GetBoostChargeFraction() * 100f);
                sb.Append(", ").Append(Loc.Get("status_boost", boost));
            }

            // Suit puncture
            if (PlayerState.IsWearingSuit() && _playerResources.IsSuitPunctured())
                sb.Append(", ").Append(Loc.Get("status_suit_punctured"));

            ScreenReader.Say(sb.ToString());
            DebugLogger.Log(LogCategory.State, "StatusHandler", $"Personal: {sb}");
        }

        #endregion

        #region J — Ship status

        /// <summary>Announces ship fuel, oxygen, hull integrity, damage report.</summary>
        public void ReadShipStatus()
        {
            EnsureShipRefs();
            if (_shipResources == null && _shipDamage == null)
            {
                ScreenReader.Say(Loc.Get("status_ship_unavailable"));
                return;
            }

            var sb = new System.Text.StringBuilder();

            // Ship fuel
            if (_shipResources != null)
            {
                int shipFuel = Mathf.RoundToInt(_shipResources.GetFractionalFuel() * 100f);
                sb.Append(Loc.Get("status_ship_fuel", shipFuel));

                // Ship oxygen
                int shipOxy = Mathf.RoundToInt(_shipResources.GetFractionalOxygen() * 100f);
                sb.Append(", ").Append(Loc.Get("status_ship_oxygen", shipOxy));
            }

            // Damage
            if (_shipDamage != null)
            {
                if (_shipDamage.IsHullBreached())
                {
                    sb.Append(", ").Append(Loc.Get("status_ship_hull_breach"));
                }
                else if (_shipDamage.IsDamaged())
                {
                    int integrity = Mathf.RoundToInt(_shipDamage.GetLowestHullIntegrity() * 100f);
                    sb.Append(", ").Append(Loc.Get("status_ship_integrity", integrity));
                }
                else
                {
                    sb.Append(", ").Append(Loc.Get("status_ship_ok"));
                }

                if (_shipDamage.IsReactorCritical())
                    sb.Append(", ").Append(Loc.Get("status_ship_reactor"));
                if (_shipDamage.IsElectricalFailed())
                    sb.Append(", ").Append(Loc.Get("status_ship_electrical"));
            }

            ScreenReader.Say(sb.ToString());
            DebugLogger.Log(LogCategory.State, "StatusHandler", $"Ship: {sb}");
        }

        #endregion

        #region K — Environment

        /// <summary>Announces active hazards, zero-G state, underwater. Location is handled by L.</summary>
        public void ReadEnvironment()
        {
            EnsurePlayerRefs();
            var sb = new System.Text.StringBuilder();

            // Hazards
            if (_hazardDetector != null)
            {
                float dps = _hazardDetector.GetNetDamagePerSecond();
                if (dps > 0f)
                {
                    string hazardName = GetActiveHazardName();
                    sb.Append(Loc.Get("status_hazard", hazardName, Mathf.RoundToInt(dps)));
                }
                else
                {
                    sb.Append(Loc.Get("status_no_hazard"));
                }
            }

            // Zero-G
            if (PlayerState.InZeroG())
                sb.Append(", ").Append(Loc.Get("status_zero_g"));

            // Underwater
            if (PlayerState.IsCameraUnderwater())
                sb.Append(", ").Append(Loc.Get("status_underwater"));

            if (sb.Length == 0)
            {
                ScreenReader.Say(Loc.Get("status_unavailable"));
                return;
            }

            ScreenReader.Say(sb.ToString());
            DebugLogger.Log(LogCategory.State, "StatusHandler", $"Environment: {sb}");
        }

        #endregion

        #region Helpers

        private void EnsurePlayerRefs()
        {
            if (_playerResources == null)
                _playerResources = Locator.GetPlayerBody()?.GetComponent<PlayerResources>();
            if (_hazardDetector == null)
                _hazardDetector = Locator.GetPlayerDetector()?.GetComponent<HazardDetector>();
            if (_jetpack == null)
                _jetpack = Locator.GetPlayerController()?.GetComponent<JetpackThrusterModel>();
        }

        private void EnsureShipRefs()
        {
            if (_shipResources == null)
                _shipResources = Locator.GetShipBody()?.GetComponentInChildren<ShipResources>();
            if (_shipDamage == null)
                _shipDamage = Locator.GetShipBody()?.GetComponentInChildren<ShipDamageController>();
        }

        private string GetActiveHazardName()
        {
            if (_hazardDetector.InHazardType(HazardVolume.HazardType.DARKMATTER))
                return Loc.Get("hazard_ghost_matter");
            if (_hazardDetector.InHazardType(HazardVolume.HazardType.FIRE))
                return Loc.Get("hazard_fire");
            if (_hazardDetector.InHazardType(HazardVolume.HazardType.HEAT))
                return Loc.Get("hazard_heat");
            if (_hazardDetector.InHazardType(HazardVolume.HazardType.ELECTRICITY))
                return Loc.Get("hazard_electricity");
            if (_hazardDetector.InHazardType(HazardVolume.HazardType.SANDFALL))
                return Loc.Get("hazard_sand");
            return Loc.Get("hazard_unknown");
        }

        #endregion
    }
}
