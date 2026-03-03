using UnityEngine;

namespace OuterWildsAccess
{
    /// <summary>
    /// Announces scout probe (Guetteur) events and provides on-demand status.
    ///
    /// Event-driven (always active):
    ///   - Launch, anchor, retrieve, destroy, snapshot
    ///
    /// Polling (only while probe is launched):
    ///   - Interference state changes (Quantum Moon, sand, cloak)
    ///
    /// Manual (F8):
    ///   - Distance to player, time anchored, interference status
    ///
    /// Always active — no ModSettings guard.
    /// </summary>
    public class ScoutHandler
    {
        #region Constants

        private const float CheckInterval = 1.0f;

        #endregion

        #region State

        private SurveyorProbe _probe;
        private bool _isLaunched;
        private bool _lastInterference;
        private float _nextCheck;

        #endregion

        #region Lifecycle

        /// <summary>Subscribes to GlobalMessenger probe events. Call from Main.Start().</summary>
        public void Initialize()
        {
            GlobalMessenger<SurveyorProbe>.AddListener("LaunchProbe", OnLaunchProbe);
            GlobalMessenger<SurveyorProbe>.AddListener("RetrieveProbe", OnRetrieveProbe);
            GlobalMessenger<SurveyorProbe>.AddListener("ForceRetrieveProbe", OnForceRetrieve);
            GlobalMessenger<ProbeCamera>.AddListener("ProbeSnapshot", OnSnapshot);
            DebugLogger.Log(LogCategory.State, "ScoutHandler", "Initialized");
        }

        /// <summary>Unsubscribes from all events. Call from Main.OnDestroy().</summary>
        public void Cleanup()
        {
            GlobalMessenger<SurveyorProbe>.RemoveListener("LaunchProbe", OnLaunchProbe);
            GlobalMessenger<SurveyorProbe>.RemoveListener("RetrieveProbe", OnRetrieveProbe);
            GlobalMessenger<SurveyorProbe>.RemoveListener("ForceRetrieveProbe", OnForceRetrieve);
            GlobalMessenger<ProbeCamera>.RemoveListener("ProbeSnapshot", OnSnapshot);
            UnsubscribeFromProbeEvents();
            _probe = null;
        }

        /// <summary>Polls interference changes while probe is launched.</summary>
        public void Update()
        {
            if (!_isLaunched || _probe == null) return;
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + CheckInterval;

            PollInterference();
        }

        #endregion

        #region GlobalMessenger Event Handlers

        private void OnLaunchProbe(SurveyorProbe probe)
        {
            _probe = probe;
            _isLaunched = true;
            _lastInterference = false;
            _nextCheck = Time.time + 2.0f;

            SubscribeToProbeEvents();

            ScreenReader.Say(Loc.Get("scout_launched"));
            DebugLogger.Log(LogCategory.State, "ScoutHandler", "Probe launched");
        }

        private void OnRetrieveProbe(SurveyorProbe probe)
        {
            HandleRetrieval("Probe retrieved");
        }

        private void OnForceRetrieve(SurveyorProbe probe)
        {
            HandleRetrieval("Probe force-retrieved");
        }

        private void OnSnapshot(ProbeCamera camera)
        {
            ScreenReader.Say(Loc.Get("scout_snapshot"));
            DebugLogger.Log(LogCategory.State, "ScoutHandler", "Snapshot taken");
        }

        #endregion

        #region C# Event Handlers (SurveyorProbe instance)

        private void OnAnchorProbe()
        {
            ScreenReader.Say(Loc.Get("scout_anchored"));
            DebugLogger.Log(LogCategory.State, "ScoutHandler", "Probe anchored");
        }

        private void OnProbeDestroyed()
        {
            UnsubscribeFromProbeEvents();
            _isLaunched = false;
            _probe = null;
            ScreenReader.Say(Loc.Get("scout_destroyed"), SpeechPriority.Now);
            DebugLogger.Log(LogCategory.State, "ScoutHandler", "Probe destroyed");
        }

        #endregion

        #region Manual Status (F8)

        /// <summary>
        /// Reads scout probe status on demand. Called from Main.ProcessHotkeys() on F8.
        /// </summary>
        public void ReadStatus()
        {
            SurveyorProbe probe = Locator.GetProbe();

            if (probe == null || !probe.IsLaunched())
            {
                ScreenReader.Say(Loc.Get("scout_available"));
                return;
            }

            var sb = new System.Text.StringBuilder();

            // Distance to player
            OWRigidbody playerBody = Locator.GetPlayerBody();
            if (playerBody != null)
            {
                float dist = Vector3.Distance(playerBody.GetPosition(), probe.transform.position);
                sb.Append(Loc.Get("scout_distance", Mathf.RoundToInt(dist)));
            }

            // State: anchored (with time), in flight, or being retrieved
            if (probe.IsAnchored())
            {
                float seconds = probe.GetAnchor().GetSecondsAnchored();
                int min = Mathf.FloorToInt(seconds / 60f);
                int sec = Mathf.FloorToInt(seconds % 60f);
                if (min > 0)
                    sb.Append(", ").Append(Loc.Get("scout_anchored_time_min", min, sec));
                else
                    sb.Append(", ").Append(Loc.Get("scout_anchored_time_sec", sec));
            }
            else if (probe.IsRetrieving())
            {
                sb.Append(", ").Append(Loc.Get("scout_retrieving"));
            }
            else
            {
                sb.Append(", ").Append(Loc.Get("scout_in_flight"));
            }

            // Interference
            ProbeCamera fwdCam = probe.GetForwardCamera();
            if (fwdCam != null && fwdCam.HasInterference())
            {
                sb.Append(", ").Append(Loc.Get("scout_has_interference"));
            }

            ScreenReader.Say(sb.ToString());
            DebugLogger.Log(LogCategory.State, "ScoutHandler", $"Status: {sb}");
        }

        #endregion

        #region Helpers

        private void HandleRetrieval(string logMsg)
        {
            UnsubscribeFromProbeEvents();
            _isLaunched = false;
            _probe = null;
            ScreenReader.Say(Loc.Get("scout_retrieved"));
            DebugLogger.Log(LogCategory.State, "ScoutHandler", logMsg);
        }

        private void SubscribeToProbeEvents()
        {
            if (_probe == null) return;
            _probe.OnAnchorProbe += OnAnchorProbe;
            _probe.OnProbeDestroyed += OnProbeDestroyed;
        }

        private void UnsubscribeFromProbeEvents()
        {
            if (_probe == null) return;
            _probe.OnAnchorProbe -= OnAnchorProbe;
            _probe.OnProbeDestroyed -= OnProbeDestroyed;
        }

        private void PollInterference()
        {
            ProbeCamera fwdCam = _probe.GetForwardCamera();
            if (fwdCam == null) return;

            bool interference = fwdCam.HasInterference();
            if (interference != _lastInterference)
            {
                _lastInterference = interference;
                if (interference)
                {
                    ScreenReader.Say(Loc.Get("scout_interference_on"));
                    DebugLogger.Log(LogCategory.State, "ScoutHandler", "Interference detected");
                }
                else
                {
                    ScreenReader.Say(Loc.Get("scout_interference_off"));
                    DebugLogger.Log(LogCategory.State, "ScoutHandler", "Interference cleared");
                }
            }
        }

        #endregion
    }
}
