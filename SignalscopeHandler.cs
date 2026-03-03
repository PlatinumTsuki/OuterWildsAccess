using UnityEngine;

namespace OuterWildsAccess
{
    /// <summary>
    /// Announces signalscope information via screen reader:
    /// frequency name, signal detection, strength changes, identification.
    ///
    /// Approach:
    ///   - Events: EquipSignalscope, UnequipSignalscope, IdentifySignal
    ///   - Polling (0.5s): frequency changes, signal strength tier changes
    ///   - Manual: U reads full signalscope status on demand
    ///

    /// </summary>
    public class SignalscopeHandler
    {
        #region Constants

        private const float CheckInterval = 0.5f;

        // Strength tier boundaries
        private const float StrengthVeryWeak = 0.01f;
        private const float StrengthWeak     = 0.25f;
        private const float StrengthModerate = 0.50f;
        private const float StrengthStrong   = 0.80f;
        private const float StrengthMaximum  = 0.95f;

        // Dark Bramble distance cap (game adds 100000 for fog warp signals)
        private const float MaxReasonableDistance = 10000f;

        #endregion

        #region State

        private bool _isEquipped;
        private float _nextCheck;

        // Frequency tracking
        private SignalFrequency _lastFrequency;

        // Signal tracking
        private SignalName _lastSignalName;
        private int _lastStrengthTier; // -1 = no signal, 0-4 = tiers
        private bool _hadSignal;

        // Cached reference
        private Signalscope _scope;

        #endregion

        #region Lifecycle

        /// <summary>Subscribes to signalscope events. Call from Main.Start().</summary>
        public void Initialize()
        {
            GlobalMessenger<Signalscope>.AddListener("EquipSignalscope", OnEquipSignalscope);
            GlobalMessenger.AddListener("UnequipSignalscope", OnUnequipSignalscope);
            GlobalMessenger.AddListener("IdentifySignal", OnIdentifySignal);
            DebugLogger.Log(LogCategory.State, "SignalscopeHandler", "Initialized");
        }

        /// <summary>Unsubscribes from events. Call from Main.OnDestroy().</summary>
        public void Cleanup()
        {
            GlobalMessenger<Signalscope>.RemoveListener("EquipSignalscope", OnEquipSignalscope);
            GlobalMessenger.RemoveListener("UnequipSignalscope", OnUnequipSignalscope);
            GlobalMessenger.RemoveListener("IdentifySignal", OnIdentifySignal);
            _scope = null;
        }

        /// <summary>Polls frequency and signal changes. Call from Main.Update().</summary>
        public void Update()
        {

            if (!_isEquipped) return;
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + CheckInterval;

            PollFrequencyChange();
            PollSignalChanges();
        }

        #endregion

        #region Event Handlers

        private void OnEquipSignalscope(Signalscope scope)
        {
            _scope = scope;
            _isEquipped = true;
            ResetTracking();



            SignalFrequency freq = scope.GetFrequencyFilter();
            _lastFrequency = freq;
            string freqName = AudioSignal.FrequencyToString(freq, false);
            ScreenReader.Say(Loc.Get("scope_equipped", freqName));
            // Delay polling so the frequency announcement isn't immediately buried
            _nextCheck = Time.time + 2.0f;
            DebugLogger.Log(LogCategory.State, "SignalscopeHandler",
                "Equipped, freq=" + freq);
        }

        private void OnUnequipSignalscope()
        {
            _isEquipped = false;
            _scope = null;
            ResetTracking();
        }

        private void OnIdentifySignal()
        {

            if (_scope == null) return;

            AudioSignal strongest = _scope.GetStrongestSignal();
            if (strongest == null) return;

            string signalName = AudioSignal.SignalNameToString(strongest.GetName());
            ScreenReader.Say(Loc.Get("scope_signal_identified", signalName));
            DebugLogger.Log(LogCategory.State, "SignalscopeHandler",
                "Signal identified: " + signalName);
        }

        #endregion

        #region Polling

        private void PollFrequencyChange()
        {
            if (_scope == null) return;

            SignalFrequency currentFreq = _scope.GetFrequencyFilter();
            if (currentFreq != _lastFrequency)
            {
                _lastFrequency = currentFreq;
                string freqName = AudioSignal.FrequencyToString(currentFreq, false);
                ScreenReader.Say(Loc.Get("scope_frequency", freqName));
                DebugLogger.Log(LogCategory.State, "SignalscopeHandler",
                    "Frequency changed: " + currentFreq);

                // Reset signal tracking when frequency changes
                _lastSignalName = SignalName.Default;
                _lastStrengthTier = -1;
                _hadSignal = false;
            }
        }

        private void PollSignalChanges()
        {
            if (_scope == null) return;

            AudioSignal strongest = _scope.GetStrongestSignal();

            if (strongest == null || strongest.GetSignalStrength() < StrengthVeryWeak)
            {
                if (_hadSignal)
                {
                    ScreenReader.Say(Loc.Get("scope_signal_lost"));
                    DebugLogger.Log(LogCategory.State, "SignalscopeHandler", "Signal lost");
                    _hadSignal = false;
                    _lastSignalName = SignalName.Default;
                    _lastStrengthTier = -1;
                }
                return;
            }

            float strength = strongest.GetSignalStrength();
            float distance = strongest.GetDistanceFromScope();
            SignalName signalName = strongest.GetName();
            int tier = GetStrengthTier(strength);
            bool known = PlayerData.KnowsSignal(signalName);

            // New signal detected (different from last)
            if (signalName != _lastSignalName)
            {
                _lastSignalName = signalName;
                _lastStrengthTier = tier;
                _hadSignal = true;

                string name = known
                    ? AudioSignal.SignalNameToString(signalName)
                    : Loc.Get("scope_unknown_signal");
                string tierDesc = Loc.Get(GetStrengthTierKey(tier));

                if (strength >= StrengthStrong && distance < MaxReasonableDistance)
                {
                    ScreenReader.Say(Loc.Get("scope_signal_detected_dist",
                        name, tierDesc, Mathf.RoundToInt(distance)));
                }
                else
                {
                    ScreenReader.Say(Loc.Get("scope_signal_detected", name, tierDesc));
                }

                DebugLogger.Log(LogCategory.State, "SignalscopeHandler",
                    $"New signal: {signalName}, tier={tier}, dist={distance:F0}");
                return;
            }

            // Strength tier changed
            if (tier != _lastStrengthTier)
            {
                _lastStrengthTier = tier;
                _hadSignal = true;

                string tierDesc = Loc.Get(GetStrengthTierKey(tier));

                if (strength >= StrengthStrong && known && distance < MaxReasonableDistance)
                {
                    ScreenReader.Say(Loc.Get("scope_strength_dist",
                        tierDesc, Mathf.RoundToInt(distance)));
                }
                else
                {
                    ScreenReader.Say(Loc.Get("scope_strength", tierDesc));
                }

                DebugLogger.Log(LogCategory.State, "SignalscopeHandler",
                    $"Strength tier changed: {tier}, dist={distance:F0}");
            }
        }

        #endregion

        #region Manual Read (U)

        /// <summary>
        /// Reads full signalscope status on demand.
        /// Called from Main.ProcessHotkeys() when U is pressed.
        /// </summary>
        public void ReadStatus()
        {


            // Check if scope is active
            bool scopeEquipped = false;
            try
            {
                var swapper = Locator.GetToolModeSwapper();
                scopeEquipped = swapper != null
                    && swapper.IsInToolMode(ToolMode.SignalScope);
            }
            catch { }

            if (!scopeEquipped)
            {
                ScreenReader.Say(Loc.Get("scope_not_equipped"));
                return;
            }

            // Get scope reference if we don't have it
            if (_scope == null)
            {
                try { _scope = Locator.GetToolModeSwapper().GetSignalScope(); }
                catch { }
            }
            if (_scope == null)
            {
                ScreenReader.Say(Loc.Get("scope_not_equipped"));
                return;
            }

            SignalFrequency freq = _scope.GetFrequencyFilter();
            string freqName = AudioSignal.FrequencyToString(freq, false);

            AudioSignal strongest = _scope.GetStrongestSignal();
            if (strongest == null || strongest.GetSignalStrength() < StrengthVeryWeak)
            {
                ScreenReader.Say(Loc.Get("scope_status_no_signal", freqName));
                return;
            }

            float strength = strongest.GetSignalStrength();
            float distance = strongest.GetDistanceFromScope();
            float degrees = strongest.GetDegreesFromScope();
            SignalName signalName = strongest.GetName();
            bool known = PlayerData.KnowsSignal(signalName);

            string name = known
                ? AudioSignal.SignalNameToString(signalName)
                : Loc.Get("scope_unknown_signal");
            string tierDesc = Loc.Get(GetStrengthTierKey(GetStrengthTier(strength)));

            if (strength >= StrengthStrong && distance < MaxReasonableDistance)
            {
                ScreenReader.Say(Loc.Get("scope_status_full",
                    freqName, name, tierDesc,
                    Mathf.RoundToInt(distance),
                    Mathf.RoundToInt(degrees)));
            }
            else
            {
                ScreenReader.Say(Loc.Get("scope_status_partial",
                    freqName, name, tierDesc,
                    Mathf.RoundToInt(degrees)));
            }
        }

        #endregion

        #region Helpers

        private void ResetTracking()
        {
            _lastFrequency = SignalFrequency.Default;
            _lastSignalName = SignalName.Default;
            _lastStrengthTier = -1;
            _hadSignal = false;
            _nextCheck = 0f;
        }

        private static int GetStrengthTier(float strength)
        {
            if (strength >= StrengthMaximum)  return 4;
            if (strength >= StrengthStrong)   return 3;
            if (strength >= StrengthModerate) return 2;
            if (strength >= StrengthWeak)     return 1;
            return 0;
        }

        private static string GetStrengthTierKey(int tier)
        {
            switch (tier)
            {
                case 0: return "scope_str_very_weak";
                case 1: return "scope_str_weak";
                case 2: return "scope_str_moderate";
                case 3: return "scope_str_strong";
                case 4: return "scope_str_maximum";
                default: return "scope_str_very_weak";
            }
        }

        #endregion
    }
}
