using System.Reflection;
using UnityEngine;

namespace OuterWildsAccess
{
    /// <summary>
    /// Provides screen reader feedback for ship/satellite repair interactions.
    ///
    /// The vanilla repair flow is fully visual: when the player aims a damaged
    /// part within 3 m, FirstPersonManipulator stores it in _focusedRepairReceiver
    /// and shows two on-screen prompts ("Hold interact" + "PartName: 45%").
    /// Pressing/holding interact toggles _isRepairing and ticks RepairFraction
    /// each frame until IsDamaged() turns false.
    ///
    /// We don't have C# events for any of this, so we poll the manipulator's
    /// private state via reflection (cached lazily) and announce:
    ///   - Focus gained on a repairable  → name + integrity %
    ///   - Repair started                → "Réparation en cours…"
    ///   - Periodic progress (~1 s)      → "X %"
    ///   - Repair finished (IsDamaged false) → "Réparation terminée"
    ///   - Repair released before done   → "Réparation interrompue à X %"
    /// </summary>
    public class RepairHandler
    {
        #region Constants

        private const float ProgressInterval = 1.0f; // seconds between % announces
        private const int   ProgressDeltaMin = 5;    // only re-announce when % moved by ≥ this

        #endregion

        #region Reflection cache

        private FirstPersonManipulator _manipulator;
        private FieldInfo              _focusedField;
        private FieldInfo              _isRepairingField;
        private bool                   _reflectionReady;

        #endregion

        #region State

        private RepairReceiver _lastFocused;
        private bool           _wasRepairing;
        private float          _nextProgressTime;
        private int            _lastProgressPct;

        #endregion

        public void Initialize()
        {
            DebugLogger.LogState("[RepairHandler] Initialized.");
        }

        public void Cleanup()
        {
            _manipulator      = null;
            _focusedField     = null;
            _isRepairingField = null;
            _reflectionReady  = false;
            _lastFocused      = null;
            _wasRepairing     = false;
        }

        public void Update()
        {
            if (!EnsureReflection()) return;

            RepairReceiver focused;
            bool           isRepairing;
            try
            {
                focused     = _focusedField.GetValue(_manipulator)     as RepairReceiver;
                isRepairing = (bool)_isRepairingField.GetValue(_manipulator);
            }
            catch
            {
                return;
            }

            HandleFocusChange(focused);
            HandleRepairState(focused, isRepairing);

            _lastFocused  = focused;
            _wasRepairing = isRepairing;
        }

        #region Reflection

        private bool EnsureReflection()
        {
            if (_reflectionReady) return _manipulator != null;

            _manipulator = Object.FindObjectOfType<FirstPersonManipulator>();
            if (_manipulator == null) return false;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            _focusedField     = typeof(FirstPersonManipulator).GetField("_focusedRepairReceiver", flags);
            _isRepairingField = typeof(FirstPersonManipulator).GetField("_isRepairing",           flags);

            if (_focusedField == null || _isRepairingField == null)
            {
                DebugLogger.LogState("[RepairHandler] Reflection failed: fields not found.");
                _manipulator     = null;
                _reflectionReady = true; // don't retry every frame
                return false;
            }

            _reflectionReady = true;
            DebugLogger.LogState("[RepairHandler] Reflection wired.");
            return true;
        }

        #endregion

        #region Event logic

        private void HandleFocusChange(RepairReceiver focused)
        {
            if (focused == _lastFocused) return;

            // Only announce when we acquire focus on a new repairable target.
            // Losing focus is silent (would be too noisy on every micro-aim).
            if (focused != null)
            {
                string partName = LocalizeRepairPart(focused.GetRepairableName());
                int    pct      = ToPct(focused.GetRepairFraction());
                ScreenReader.Say(Loc.Get("repair_focus", partName, pct));
            }
        }

        private void HandleRepairState(RepairReceiver focused, bool isRepairing)
        {
            // Transition: idle → repairing
            if (isRepairing && !_wasRepairing)
            {
                _lastProgressPct  = focused != null ? ToPct(focused.GetRepairFraction()) : 0;
                _nextProgressTime = Time.unscaledTime + ProgressInterval;
                ScreenReader.Say(Loc.Get("repair_started"));
                return;
            }

            // Transition: repairing → idle
            if (!isRepairing && _wasRepairing)
            {
                // _lastFocused holds the receiver we were working on.
                // If it's no longer damaged → completed. Otherwise → interrupted.
                if (_lastFocused != null && !_lastFocused.IsDamaged())
                {
                    ScreenReader.Say(Loc.Get("repair_finished"));
                }
                else
                {
                    int pct = _lastFocused != null ? ToPct(_lastFocused.GetRepairFraction()) : 0;
                    ScreenReader.Say(Loc.Get("repair_interrupted", pct));
                }
                return;
            }

            // Ongoing repair → periodic progress announces
            if (isRepairing && focused != null && Time.unscaledTime >= _nextProgressTime)
            {
                int pct = ToPct(focused.GetRepairFraction());
                if (Mathf.Abs(pct - _lastProgressPct) >= ProgressDeltaMin && pct < 100)
                {
                    ScreenReader.Say(Loc.Get("repair_progress", pct));
                    _lastProgressPct = pct;
                }
                _nextProgressTime = Time.unscaledTime + ProgressInterval;
            }
        }

        #endregion

        #region Helpers

        private static int ToPct(float fraction)
        {
            return Mathf.Clamp(Mathf.RoundToInt(fraction * 100f), 0, 100);
        }

        private static string LocalizeRepairPart(UITextType type)
        {
            return Loc.Get("repair_part_" + type.ToString());
        }

        #endregion
    }
}
