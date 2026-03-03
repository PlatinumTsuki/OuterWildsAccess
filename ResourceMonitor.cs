using UnityEngine;

namespace OuterWildsAccess
{
    /// <summary>
    /// Monitors player health, oxygen, jetpack fuel, and ship fuel.
    /// Announces a warning via the screen reader each time a resource crosses
    /// a downward threshold (75%, 50%, 25%, 10% for health/oxygen; 50%, 25%, 10% for fuels).
    /// Silently resets the tracked index when a resource is replenished above a threshold.
    ///
    /// Guard: ModSettings.GaugeEnabled
    /// </summary>
    public class ResourceMonitor
    {
        #region Constants

        // Thresholds in descending order — checked top to bottom
        private static readonly float[] HealthThresholds   = { 0.75f, 0.50f, 0.25f, 0.10f };
        private static readonly float[] OxygenThresholds   = { 0.75f, 0.50f, 0.25f, 0.10f };
        private static readonly float[] JetpackThresholds  = { 0.50f, 0.25f, 0.10f };
        private static readonly float[] ShipFuelThresholds = { 0.50f, 0.25f, 0.10f };

        // Check resources twice per second (no need per-frame)
        private const float CheckInterval = 0.5f;

        #endregion

        #region State

        private PlayerResources _playerResources;
        private ShipResources   _shipResources;

        // -1 = above the first threshold (no warning given yet)
        private int _healthIdx   = -1;
        private int _oxygenIdx   = -1;
        private int _jetpackIdx  = -1;
        private int _shipFuelIdx = -1;

        private float _nextCheck;

        #endregion

        #region Lifecycle

        /// <summary>
        /// Resets cached component references and threshold state.
        /// Call from Main.OnSceneLoaded() whenever a new scene is loaded.
        /// </summary>
        public void OnSceneLoaded()
        {
            _playerResources = null;
            _shipResources   = null;
            _healthIdx       = -1;
            _oxygenIdx       = -1;
            _jetpackIdx      = -1;
            _shipFuelIdx     = -1;
            _nextCheck       = 0f;
        }

        #endregion

        #region Public API

        /// <summary>Call every frame from Main.Update().</summary>
        public void Update()
        {
            if (!ModSettings.GaugeEnabled) return;
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + CheckInterval;

            CheckHealth();
            CheckOxygen();
            CheckJetpackFuel();
            CheckShipFuel();
        }

        #endregion

        #region Private checks

        private void CheckHealth()
        {
            if (_playerResources == null)
                _playerResources = Locator.GetPlayerBody()?.GetComponent<PlayerResources>();
            if (_playerResources == null) return;

            Evaluate(HealthThresholds, ref _healthIdx,
                     _playerResources.GetHealthFraction(), "gauge_health");
        }

        private void CheckOxygen()
        {
            if (_playerResources == null)
                _playerResources = Locator.GetPlayerBody()?.GetComponent<PlayerResources>();
            if (_playerResources == null) return;

            // While refilling, reset so the next depletion announces again from the start
            if (_playerResources.IsRefillingOxygen())
            {
                _oxygenIdx = -1;
                return;
            }

            Evaluate(OxygenThresholds, ref _oxygenIdx,
                     _playerResources.GetOxygenFraction(), "gauge_oxygen");
        }

        private void CheckJetpackFuel()
        {
            if (_playerResources == null) return;

            Evaluate(JetpackThresholds, ref _jetpackIdx,
                     _playerResources.GetFuelFraction(), "gauge_jetpack");
        }

        private void CheckShipFuel()
        {
            if (_shipResources == null)
                _shipResources = Locator.GetShipBody()?.GetComponentInChildren<ShipResources>();
            if (_shipResources == null) return;

            Evaluate(ShipFuelThresholds, ref _shipFuelIdx,
                     _shipResources.GetFractionalFuel(), "gauge_ship_fuel");
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Determines the lowest threshold the fraction has crossed (highest array index).
        /// Announces when a new lower threshold is first crossed.
        /// Silently resets the index when resources go back up above a threshold.
        /// </summary>
        private static void Evaluate(float[] thresholds, ref int lastIdx,
                                     float fraction, string locKey)
        {
            // Walk the descending threshold array: track the deepest threshold crossed
            int idx = -1;
            for (int i = 0; i < thresholds.Length; i++)
            {
                if (fraction <= thresholds[i])
                    idx = i;
                else
                    break; // thresholds are descending; above this one means above all following
            }

            if (idx > lastIdx)
            {
                // Player crossed a new lower threshold — announce
                int pct = Mathf.RoundToInt(thresholds[idx] * 100f);
                ScreenReader.Say(Loc.Get(locKey, pct), SpeechPriority.Now);
                DebugLogger.Log(LogCategory.State, "ResourceMonitor",
                    $"{locKey} {pct}% (fraction={fraction:F2})");
            }
            // Always update — handles silent reset when resource is replenished
            lastIdx = idx;
        }

        #endregion
    }
}
