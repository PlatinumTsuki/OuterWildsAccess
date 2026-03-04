using HarmonyLib;
using UnityEngine;

namespace OuterWildsAccess
{
    /// <summary>
    /// Auto-pilots the model rocket to the geyser landing spot.
    /// Patches ModelShipController input when autopilot is active.
    /// End key to start/stop when seated at the console.
    /// </summary>
    public class ModelRocketHandler
    {
        #region Constants

        private const float LaunchTime = 1.5f;   // seconds of pure upward thrust at start
        private const float LaunchUp   = 0.9f;   // upward thrust during launch
        private const float CruiseUp   = 0.35f;  // upward thrust during cruise (fight gravity)
        private const float CruiseHoriz = 0.4f;  // horizontal thrust toward target
        private const float LandDist   = 3f;     // start landing phase (m horizontal)

        #endregion

        #region State

        private bool _atConsole;
        private Harmony _harmony;

        private static bool       _autopilotActive;
        private static OWRigidbody _rocketBody;
        private static Transform  _landingTarget;
        private static Vector3    _thrustInput;
        private static bool       _landed;
        private static float      _announceTimer;
        private static float      _launchTimer;

        /// <summary>True when the player is at the model rocket console.</summary>
        public bool AtConsole => _atConsole;

        /// <summary>True while autopilot is flying the rocket.</summary>
        public bool IsAutopiloting => _autopilotActive;

        #endregion

        #region Lifecycle

        /// <summary>Call from Main.Start().</summary>
        public void Initialize()
        {
            GlobalMessenger<OWRigidbody>.AddListener("EnterRemoteFlightConsole", OnEnterConsole);
            GlobalMessenger.AddListener("ExitRemoteFlightConsole", OnExitConsole);

            _harmony = new Harmony("com.outerwildsaccess.modelrocket");
            var rf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

            try
            {
                var readTrans = typeof(ModelShipController).GetMethod("ReadTranslationalInput", rf);
                if (readTrans != null)
                    _harmony.Patch(readTrans,
                        prefix: new HarmonyMethod(typeof(ModelRocketHandler), nameof(PrefixReadTranslational)));

                var readRot = typeof(ModelShipController).GetMethod("ReadRotationalInput", rf);
                if (readRot != null)
                    _harmony.Patch(readRot,
                        prefix: new HarmonyMethod(typeof(ModelRocketHandler), nameof(PrefixReadRotational)));

                DebugLogger.Log(LogCategory.State, "ModelRocket", "Harmony patches applied");
            }
            catch (System.Exception ex)
            {
                DebugLogger.LogState("[ModelRocket] Patch failed: " + ex.Message);
            }
        }

        /// <summary>Call from Main.OnDestroy().</summary>
        public void Cleanup()
        {
            GlobalMessenger<OWRigidbody>.RemoveListener("EnterRemoteFlightConsole", OnEnterConsole);
            GlobalMessenger.RemoveListener("ExitRemoteFlightConsole", OnExitConsole);
            _autopilotActive = false;
            _atConsole = false;
            _rocketBody = null;
            _landingTarget = null;
        }

        #endregion

        #region Public API

        /// <summary>Toggle autopilot on/off. Call when End key pressed at console.</summary>
        public void ToggleAutopilot()
        {
            if (!_atConsole || _rocketBody == null) return;

            if (_autopilotActive)
            {
                _autopilotActive = false;
                _thrustInput = Vector3.zero;
                ScreenReader.Say(Loc.Get("model_rocket_autopilot_off"));
                return;
            }

            // Find landing target: prefer ModelShipLandingSpot, fallback to nearest geyser
            var spot = Object.FindObjectOfType<ModelShipLandingSpot>();
            if (spot != null)
            {
                _landingTarget = spot.transform;
            }
            else
            {
                var geysers = Object.FindObjectsOfType<GeyserController>();
                if (geysers.Length == 0)
                {
                    ScreenReader.Say(Loc.Get("model_rocket_no_target"));
                    return;
                }

                float minDist = float.MaxValue;
                Transform nearest = null;
                foreach (var g in geysers)
                {
                    float d = Vector3.Distance(_rocketBody.transform.position, g.transform.position);
                    if (d < minDist) { minDist = d; nearest = g.transform; }
                }
                _landingTarget = nearest;
            }

            _autopilotActive = true;
            _landed = false;
            _announceTimer = 0f;
            _launchTimer = LaunchTime;
            ScreenReader.Say(Loc.Get("model_rocket_autopilot_on"));
            DebugLogger.Log(LogCategory.State, "ModelRocket", "Autopilot started");
        }

        /// <summary>Call every frame from Main.Update().</summary>
        public void Update()
        {
            if (!_autopilotActive || _rocketBody == null || _landingTarget == null) return;

            // Check dialogue condition for successful landing
            if (!_landed && DialogueConditionManager.SharedInstance != null
                && DialogueConditionManager.SharedInstance.GetConditionState("LandedModelRocket"))
            {
                _landed = true;
                _autopilotActive = false;
                _thrustInput = Vector3.zero;
                ScreenReader.Say(Loc.Get("model_rocket_landed"), SpeechPriority.Now);
                DebugLogger.Log(LogCategory.State, "ModelRocket", "Landing confirmed!");
                return;
            }

            // Periodic distance announcements (every 3s)
            _announceTimer -= Time.deltaTime;
            if (_announceTimer <= 0f)
            {
                float dist = Vector3.Distance(_rocketBody.transform.position, _landingTarget.position);
                ScreenReader.Say(Loc.Get("model_rocket_distance", Mathf.RoundToInt(dist)),
                    SpeechPriority.Normal);
                _announceTimer = 3f;
            }

            ComputeThrust();
        }

        #endregion

        #region Autopilot control

        private void ComputeThrust()
        {
            Transform rocketTr = _rocketBody.transform;
            Vector3 rocketPos = rocketTr.position;
            Vector3 targetPos = _landingTarget.position;

            // Planet up direction
            Vector3 planetUp = Vector3.up;
            var astro = Locator.GetAstroObject(AstroObject.Name.TimberHearth);
            if (astro != null)
                planetUp = (rocketPos - astro.GetOWRigidbody().GetPosition()).normalized;

            // Horizontal distance to target
            Vector3 toTarget = targetPos - rocketPos;
            Vector3 horizToTarget = toTarget - Vector3.Project(toTarget, planetUp);
            float horizDist = horizToTarget.magnitude;

            Vector3 thrustWorld;

            if (_launchTimer > 0f)
            {
                // Phase 1: Launch — go UP to clear ground
                _launchTimer -= Time.deltaTime;
                thrustWorld = planetUp * LaunchUp;
            }
            else if (horizDist > LandDist)
            {
                // Phase 2: Cruise — fly toward target + maintain altitude
                Vector3 horizDir = horizToTarget.normalized;
                thrustWorld = horizDir * CruiseHoriz + planetUp * CruiseUp;
            }
            else
            {
                // Phase 3: Landing — stop horizontal, let it descend
                thrustWorld = planetUp * 0.1f;
            }

            // Convert to rocket local space and clamp
            _thrustInput = rocketTr.InverseTransformDirection(thrustWorld);
            _thrustInput.x = Mathf.Clamp(_thrustInput.x, -1f, 1f);
            _thrustInput.y = Mathf.Clamp(_thrustInput.y, -1f, 1f);
            _thrustInput.z = Mathf.Clamp(_thrustInput.z, -1f, 1f);
        }

        #endregion

        #region Events

        private void OnEnterConsole(OWRigidbody rocketBody)
        {
            _atConsole = true;
            _rocketBody = rocketBody;
            _landed = false;
            ScreenReader.Say(Loc.Get("model_rocket_console_enter"));
            DebugLogger.Log(LogCategory.State, "ModelRocket", "Entered console");
        }

        private void OnExitConsole()
        {
            _atConsole = false;
            _autopilotActive = false;
            _rocketBody = null;
            _landingTarget = null;
            _thrustInput = Vector3.zero;
            DebugLogger.Log(LogCategory.State, "ModelRocket", "Exited console");
        }

        #endregion

        #region Harmony patches

        private static bool PrefixReadTranslational(ref Vector3 __result)
        {
            if (!_autopilotActive) return true;
            __result = _thrustInput;
            return false;
        }

        private static bool PrefixReadRotational(ref Vector3 __result)
        {
            if (!_autopilotActive) return true;
            __result = Vector3.zero; // no rotation — thrusters handle everything
            return false;
        }

        #endregion
    }
}
