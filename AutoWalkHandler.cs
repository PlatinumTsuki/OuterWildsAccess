using System.Collections.Generic;
using HarmonyLib;
using OWML.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OuterWildsAccess
{
    /// <summary>
    /// Walks the player automatically toward a navigation target along an A* path.
    ///
    /// Design principles:
    ///   - A* pathfinding with periodic rescan for obstacle avoidance.
    ///   - If stuck, rescan path up to 3 times then stop with announcement.
    ///   - A* planned jumps (NeedsJump waypoints) fire once, grounded only.
    ///   - No repeated jumping — safe and predictable.
    ///   - Hard stops: arrival, hazard/fluid, target lost, manual input, stuck.
    /// </summary>
    public class AutoWalkHandler
    {
        #region Constants

        private const float InputThreshold    = 0.25f;

        // Periodic path rescan
        private const float RescanInterval    = 2f;    // seconds between automatic rescans

        // Movement tracking — detects stuck state
        private const float MoveCheckInterval = 0.5f;  // check movement every 0.5 s
        private const float MoveThreshold     = 0.05f; // less than this in 0.5 s = stuck
        private const int   MaxStuckRescans   = 4;     // rescan path up to 4 times, then stop

        // Path segmentation for long distances
        private const float SegmentDistance   = 50f;   // max A* segment length

        // Jump timing (A* planned jumps only)
        private const float JumpTriggerDist    = 3f;   // jump when within this distance of A* jump waypoint
        private const float PostJumpRescanDelay = 0.3f; // rescan path this long after a jump (session 29: was 1f, bottlenecked MaxAirborneBeforeStop)
        private const float JumpHorizBoost      = 4f;   // base horizontal boost for A*-planned jumps (m/s)
        private const float PromptJumpBoost     = 4f;   // prompt jump boost (reduced from 8: violent bouncing, player launched too far)

        // Post-arrival alignment
        private const float AlignTimeout   = 2f;
        private const float FaceAlignFinal = 0.995f;

        #endregion

        #region State

        private Transform       _target;
        private string          _targetName;
        private bool            _targetIsInteractable;
        private bool            _isActive;
        private bool            _patchApplied = false;
        private System.Action   _onArrival;

        // Hazard and fluid detection
        private HazardDetector _hazardDetector;
        private FluidDetector  _fluidDetector;
        private bool           _inWater;

        // Path segmentation
        private bool    _segmented;
        private Vector3 _segmentGoal;

        // Post-arrival alignment + pitch sweep
        private bool   _postArrivalAligning = false;
        private float  _alignEndTime        = 0f;
        private string _pendingArrivalMsg   = null;
        private bool   _sweepingPitch       = false;
        private float  _sweepPitch          = 0f;
        private bool   _directAimPending    = false;  // wait 1 frame for direct aim check
        private const float SweepStart      = 30f;    // start looking slightly up
        private const float SweepEnd        = -60f;   // sweep down to -60°
        private const float SweepSpeed      = 60f;    // degrees per second

        // Periodic rescan.
        //
        // Session 29+ flag-based rescan: previously sections 6 (periodic timer) and 7
        // (post-jump one-shot) cleared `_path = null` to force a fresh FindPath. That
        // erased the C1 hysteresis fallback — when AcceptOrRejectNewPath ran, _path was
        // null so the function had no anchor path to keep on a flip-flop reject, forcing
        // it to accept whatever direction A* offered (FLIPFLOP_FORCED). Observed on the
        // 2026-04-11 Mica walks: C1 detected a 152° flip but had to accept it, which
        // walked the player backwards into a dead spot where A* couldn't expand at all.
        // Fix: keep _path alive across rescan triggers, signal "needs recompute" via a
        // flag, and let AcceptOrRejectNewPath compare candidate-vs-existing properly.
        private float _rescanTimer = 0f;
        private bool  _needsRescan = false;

        // Movement tracking — stuck detection for rescan/stop.
        //
        // Session 29+ ground-local frame fix: Outer Wilds uses a floating-origin
        // "Center of the Universe" system where Unity world coordinates shift with
        // the player's current sector. A player running at 6 m/s on Timber Hearth
        // surface shows ~0.08 m/s of transform.position delta in world frame — a
        // factor-40 underreport that caused false STUCK positives and cascading
        // rescans on every Mica walk (2026-04-11 log analysis).
        //
        // Fix: store the last check position in ground-local coordinates via
        // `groundBody.transform.InverseTransformPoint(playerTr.position)`. This
        // frame rotates with the ground body, so a walking player shows their
        // actual physical displacement along the planet surface.
        private Vector3 _moveCheckPos        = Vector3.zero; // ground-local when _moveCheckInit==true
        private bool    _moveCheckInit       = false;
        private float   _moveCheckTime       = 0f;
        private bool    _isStuck             = false;
        private int     _stuckRescanCount    = 0;
        private int     _lastCheckWaypointIndex = 0;
        // Session 31: track horizDist at first stuck event. Only reset counter when
        // horizDist has improved by ≥1m, not just because the player wiggled 5cm.
        private float   _stuckStartHorizDist = float.MaxValue;

        // Ground state — faster stuck/fall detection via game events
        private PlayerCharacterController _playerController;
        private bool  _wasGrounded      = true;
        private float _airborneTime     = 0f;
        // Session 29+: raised from 0.5s to 1.2s. On hilly terrain (Mica path) the
        // player regularly walks off 1-2m ledges which produce ~0.5-0.7s of airborne
        // time. The previous 0.5s threshold killed the walk on normal terrain features.
        // 1.2s corresponds to a ~7m free-fall which is a genuine dangerous drop.
        private const float MaxAirborneBeforeStop = 1.2f;
        private const float AbsoluteAirborneCeiling = 2.5f; // safety net for jump arcs gone wrong

        // Post-jump rescan.
        //
        // Session 29+ airborne guard fix: previously, at `Time.time - _lastJumpTime
        // > PostJumpRescanDelay` the code cleared `_lastJumpTime = 0f` to schedule a
        // fresh FindPath. But that clearing was the ONLY thing protecting the jump
        // arc from the `_airborneTime >= MaxAirborneBeforeStop && _lastJumpTime == 0f`
        // stop at line ~893. Consequence: any prompt jump got cut off at 0.5s airborne
        // even while the player was mid-parabola with correct forward velocity (logged
        // 2026-04-11 Mica walks, both sessions ended AIRBORNE_STOP at ~0.51s).
        //
        // Fix: split the concerns. `_postJumpRescanDone` is the one-shot flag for the
        // rescan trigger; `_lastJumpTime` stays non-zero until the player actually
        // lands (cleared in the grounded-again branch of section 10b). This gives the
        // jump arc the full ~1.5s `AbsoluteAirborneCeiling` budget instead of 0.5s.
        private float _lastJumpTime = 0f;
        private bool  _postJumpRescanDone = false;
        private bool  _jumpGraceExpiredLogged = false;

        // Session telemetry (reliability campaign — session 29)
        private float _progressLogTime;
        private float _sessionStartTime;
        private int   _sessionFallbackCount;
        private const float ProgressLogInterval = 2f;

        // Walking telemetry — WALKING line every 0.25s during active walk.
        private float _walkingLogTime;
        private const float WalkingLogInterval = 0.25f;

        // Jump prompt grace — suppress prompt jumps for 1s at walk start to avoid
        // firing immediately when the player happens to stand at a ledge edge.
        private float _jumpPromptGraceEndTime;
        private bool  _jumpPromptGraceLoggedOnce;
        private const float JumpPromptGraceDuration = 1f;

        // Post-jump prompt cooldown — after a prompt JUMPBOOST, ignore further
        // JumpPromptTriggers for 3s. Without this, the player re-enters the same
        // trigger zone on landing and immediately jumps again, creating a 13-17
        // jump loop that wastes 30s bouncing in place (observed Mica run 2 log).
        private float _promptJumpCooldownEnd;
        private bool  _promptCooldownLoggedOnce;
        private const float PromptJumpCooldown = 3f;

        // Session 31: cap on consecutive prompt jumps without progress. After
        // MaxPromptJumpsWithoutProgress prompt jumps without horizDist improving
        // by PromptJumpProgressThreshold, disable prompt jumps entirely for this
        // walk session. The player will walk around the zone instead of bouncing.
        private int   _promptJumpCount;
        private float _promptJumpRefHorizDist;
        private bool  _promptJumpsDisabled;
        private const int   MaxPromptJumpsWithoutProgress = 4;
        private const float PromptJumpProgressThreshold   = 3f;

        // Path acceptance — progress-monotonic check: accept new path only if its
        // endpoint is closer to target by at least the improvement threshold.
        private const float ProgressImprovementThreshold = 1.0f;
        private const float ProgressRegressionThreshold  = 1.0f;
        private const int   MaxStuckRescansBeforeFlipAllowed = 2;

        // Static fields accessed by Harmony prefix
        private static bool    _injecting    = false;
        private static Vector2 _injectedAxis = Vector2.zero;
        private static Vector2 _injectedLook = Vector2.zero;

        // Jump — Reflection fields + Harmony postfix
        private static System.Reflection.FieldInfo _fieldJumpNext   = null;
        private static System.Reflection.FieldInfo _fieldJumpCharge = null;
        private static bool  _jumpEnabled     = false;
        private static int   _wantJumpFrames  = 0;
        private static float _wantJumpCharge  = 1.0f;
        private const int    JumpPersistFrames = 4;
        private float _jumpCooldown = 0f;
        private Harmony _harmony;

        // Path following (PathScanner A* grid — shared instance, set via Initialize)
        private PathScanner _pathScanner;
        private List<PathWaypoint> _path = null;
        private int _waypointIndex = 0;

        // Jump prompt detection — Reflection
        private static System.Reflection.FieldInfo _fieldListPrompts = null;
        private static bool _promptReflectionReady = false;

        // Focus detection — Reflection on FirstPersonManipulator private fields
        // HasFocusedInteractible() only checks _interactReceiver and _interactZone,
        // but the game also tracks _focusedRepairReceiver, _focusedNomaiText,
        // _focusedItemSocket, and _focusedItem separately.
        private static System.Reflection.FieldInfo _fieldFocusedRepair   = null;
        private static System.Reflection.FieldInfo _fieldFocusedNomaiText = null;
        private static System.Reflection.FieldInfo _fieldFocusedItemSocket = null;
        private static System.Reflection.FieldInfo _fieldFocusedItem     = null;
        private static bool _focusReflectionReady = false;

        // (PCC diagnostic reflection removed — served its purpose for Mica investigation)

        /// <summary>True while auto-walk is running.</summary>
        public bool IsActive => _isActive;

        #endregion

        #region Lifecycle

        /// <summary>
        /// Registers Harmony patches for input injection, jump, and turning.
        /// Call from Main.Start() with the mod's IModHelper.
        /// </summary>
        public void Initialize(IModHelper modHelper, PathScanner sharedScanner, System.Action onArrival = null)
        {
            _pathScanner = sharedScanner;
            _onArrival = onArrival;
            try
            {
                _harmony = new Harmony("com.outerwildsaccess.autowalk");
                var harmony = _harmony;
                var types   = new System.Type[] { typeof(IInputCommands), typeof(InputMode) };
                var flags   = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;

                System.Reflection.MethodInfo original =
                    typeof(OWInput).GetMethod("GetAxisValue", flags, null, types, null);

                if (original == null)
                {
                    flags    = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
                    original = typeof(InputManager).GetMethod("GetAxisValue", flags, null, types, null);
                    DebugLogger.LogState("[AutoWalkHandler] Patching InputManager.GetAxisValue (fallback).");
                }

                if (original == null)
                {
                    DebugLogger.LogState("[AutoWalkHandler] GetAxisValue not found.");
                    return;
                }

                harmony.Patch(original,
                    prefix: new HarmonyMethod(typeof(AutoWalkHandler), nameof(PrefixGetAxisValue)));
                _patchApplied = true;
                DebugLogger.LogState("[AutoWalkHandler] Harmony prefix on "
                    + original.DeclaringType.Name + "." + original.Name);

                // Jump postfix
                var rf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                _fieldJumpNext   = typeof(PlayerCharacterController).GetField("_jumpNextFixedUpdate", rf);
                _fieldJumpCharge = typeof(PlayerCharacterController).GetField("_jumpChargeTime", rf);

                var updateJumpMethod = typeof(PlayerCharacterController).GetMethod("UpdateJumpInput", rf);
                if (_fieldJumpNext != null && _fieldJumpCharge != null && updateJumpMethod != null)
                {
                    harmony.Patch(updateJumpMethod,
                        postfix: new HarmonyMethod(typeof(AutoWalkHandler), nameof(PostfixUpdateJumpInput)));
                    _jumpEnabled = true;
                }
                DebugLogger.LogState("[AutoWalkHandler] Jump postfix — enabled:" + _jumpEnabled);

                // UpdateTurning postfix (registered but body is empty — kept for future use)
                var updateTurningMethod = typeof(PlayerCharacterController).GetMethod("UpdateTurning", rf);
                if (updateTurningMethod != null)
                {
                    harmony.Patch(updateTurningMethod,
                        postfix: new HarmonyMethod(typeof(AutoWalkHandler), nameof(PostfixUpdateTurning)));
                }

                // Jump prompt detection via Reflection
                _fieldListPrompts = typeof(ScreenPromptList).GetField("_listPrompts", rf);
                _promptReflectionReady = (_fieldListPrompts != null);
                DebugLogger.LogState("[AutoWalkHandler] Prompt reflection — ready:" + _promptReflectionReady);
            }
            catch (System.Exception ex)
            {
                ScreenReader.Say(Loc.Get("autowalk_patch_failed"));
                DebugLogger.LogState("[AutoWalkHandler] Harmony patch failed: " + ex.Message);
            }
        }

        /// <summary>Stops auto-walk and clears injection. Call from Main.OnDestroy().</summary>
        public void Cleanup()
        {
            _injecting    = false;
            _injectedAxis = Vector2.zero;
            StopWalk(announce: false);
        }

        #endregion

        #region Public API

        /// <summary>Sets the navigation target for auto-walk.</summary>
        public void SetTarget(Transform target, string name, bool isInteractable = false)
        {
            _target               = target;
            _targetName           = name ?? string.Empty;
            _targetIsInteractable = isInteractable;
            DebugLogger.LogState("[AutoWalkHandler] Target set: " + _targetName);
        }

        /// <summary>
        /// Starts alignment-only mode: rotates the player body toward the target
        /// and sweeps the camera pitch to find the interaction zone.
        /// Used after teleportation or other instant-travel methods.
        /// </summary>
        public void StartAlignment(Transform target, string targetName, bool isInteractable)
        {
            if (target == null) return;

            // Stop any current auto-walk
            if (_isActive) StopWalk(announce: false);

            _target              = target;
            _targetName          = targetName ?? string.Empty;
            _targetIsInteractable = isInteractable;
            _isActive            = true;
            _injecting           = true;
            _injectedAxis        = Vector2.zero;
            _injectedLook        = Vector2.zero;
            _postArrivalAligning = true;
            _alignEndTime        = Time.time + AlignTimeout;
            _pendingArrivalMsg   = null;  // caller already announced
            _path                = null;
            _waypointIndex       = 0;

            // Instant body rotation (yaw) toward the target so the
            // first frame of the sweep already has correct horizontal aim.
            Transform playerTr = Locator.GetPlayerTransform();
            if (playerTr != null && target != null)
            {
                AlignBodyYawToTarget(playerTr, target);
            }

            // Initial pitch toward target collider (direct aim attempt)
            var camCtrl = Locator.GetPlayerCameraController();
            if (playerTr != null) AlignCameraPitchToTarget(playerTr, target);

            // For interactable targets: wait 1 frame for the game's raycast
            // to process our direct aim before falling back to sweep.
            if (isInteractable)
            {
                _directAimPending = true;
                _sweepingPitch    = false;
            }

            DebugLogger.LogState("[AutoWalkHandler] Alignment started for: " + _targetName);
        }

        /// <summary>Silently stops auto-walk and clears the target.</summary>
        public void ClearTarget()
        {
            StopWalk(announce: false);
            _target     = null;
            _targetName = string.Empty;
            DebugLogger.LogState("[AutoWalkHandler] Target cleared.");
        }

        /// <summary>Toggles auto-walk on/off.</summary>
        public void Toggle()
        {
            if (!ModSettings.AutoWalkEnabled) return;

            if (_isActive)
            {
                StopWalk(announce: true);
                return;
            }

            if (_target == null)
            {
                ScreenReader.Say(Loc.Get("nav_no_target"));
                return;
            }

            if (!_patchApplied)
            {
                ScreenReader.Say(Loc.Get("autowalk_patch_failed"));
                return;
            }

            _isActive            = true;
            _injecting           = true;
            _postArrivalAligning = false;
            _path                = null;
            _waypointIndex       = 0;
            _rescanTimer         = Time.time + RescanInterval;
            _needsRescan         = false;
            _moveCheckPos        = Vector3.zero;
            _moveCheckInit       = false;
            _moveCheckTime       = Time.time;
            _isStuck             = false;
            _stuckRescanCount    = 0;
            _stuckStartHorizDist = float.MaxValue;
            _lastCheckWaypointIndex = 0;
            _lastJumpTime        = 0f;
            _postJumpRescanDone  = false;
            _inWater             = false;
            _segmented           = false;

            // Subscribe to hazard/fluid events
            var playerDetector = Locator.GetPlayerDetector();
            _hazardDetector = playerDetector?.GetComponent<HazardDetector>();
            if (_hazardDetector != null)
                _hazardDetector.OnHazardsUpdated += OnHazardUpdated;

            _fluidDetector = playerDetector?.GetComponent<FluidDetector>();
            if (_fluidDetector != null)
            {
                _fluidDetector.OnEnterFluidType += OnFluidEntered;
                _fluidDetector.OnExitFluidType  += OnFluidExited;
            }

            // Subscribe to input mode changes (Phase 1.6 session 29).
            // If the player opens a menu, map, ship computer, telescope, signalscope, etc.
            // during an auto-walk, the InputMode leaves Character and our injected inputs
            // are silently ignored. Stopping cleanly prevents a desynced resume.
            if (OWInput.SharedInputManager != null)
                OWInput.SharedInputManager.OnUpdateInputMode += OnInputModeChanged;

            // Subscribe to ground events for faster stuck/fall detection
            _playerController = Locator.GetPlayerController();
            _wasGrounded  = _playerController != null && _playerController.IsGrounded();
            _airborneTime = 0f;

            // Reset camera pitch to neutral at start (prevents drift accumulation)
            var camStart = Locator.GetPlayerCameraController();
            if (camStart != null) camStart.SetDegreesY(0f);

            ScreenReader.Say(Loc.Get("auto_walk_on", _targetName));
            DebugLogger.LogInput("F9", "AutoWalk start → " + _targetName);

            // Session telemetry (Phase reliability campaign — session 29)
            Transform playerTrLog = Locator.GetPlayerTransform();
            if (playerTrLog != null && _target != null)
            {
                Vector3 upL    = playerTrLog.up;
                Vector3 toT    = _target.position - playerTrLog.position;
                Vector3 flatL  = toT - Vector3.Project(toT, upL);
                float   hd     = flatL.magnitude;
                float   vd     = Vector3.Dot(toT, upL);
                DebugLogger.LogAutoWalk("SESSION_START target=\"" + _targetName +
                    "\" horizDist=" + hd.ToString("F1") + "m vertDist=" + vd.ToString("F1") +
                    "m playerPos=" + playerTrLog.position.ToString("F2") +
                    " targetPos=" + _target.position.ToString("F2"));
            }
            _progressLogTime    = Time.time + ProgressLogInterval;
            _walkingLogTime     = Time.time + WalkingLogInterval;
            _sessionStartTime   = Time.time;
            _sessionFallbackCount = 0;
            _jumpPromptGraceEndTime    = Time.time + JumpPromptGraceDuration;
            _jumpPromptGraceLoggedOnce = false;
            _promptJumpCooldownEnd     = 0f;
            _promptCooldownLoggedOnce  = false;
            _promptJumpCount           = 0;
            _promptJumpRefHorizDist    = float.MaxValue;
            _promptJumpsDisabled       = false;
        }

        /// <summary>Call every frame from Main.Update().</summary>
        public void Update()
        {
            if (!_isActive) return;
            if (!ModSettings.AutoWalkEnabled) { StopWalk(announce: false); return; }

            Transform playerTr = Locator.GetPlayerTransform();
            if (playerTr == null) return;

            // ── 1. Target destroyed ──────────────────────────────────────────
            if (_target == null)
            {
                ScreenReader.Say(Loc.Get("nav_target_lost"));
                StopWalk(announce: false);
                return;
            }

            // ── 2. Manual input cancels ──────────────────────────────────────
            bool keyboardMove = Keyboard.current != null && (
                Keyboard.current.wKey.isPressed ||
                Keyboard.current.sKey.isPressed ||
                Keyboard.current.aKey.isPressed ||
                Keyboard.current.dKey.isPressed);

            var gamepad = Gamepad.current;
            bool gamepadMove = gamepad != null &&
                               gamepad.leftStick.ReadValue().magnitude > InputThreshold;

            if (keyboardMove || gamepadMove)
            {
                StopWalk(announce: true);
                return;
            }

            // ── 3. Retry hazard/fluid subscriptions if they failed ───────────
            if (_hazardDetector == null || _fluidDetector == null)
            {
                var pd = Locator.GetPlayerDetector();
                if (_hazardDetector == null)
                {
                    _hazardDetector = pd?.GetComponent<HazardDetector>();
                    if (_hazardDetector != null)
                        _hazardDetector.OnHazardsUpdated += OnHazardUpdated;
                }
                if (_fluidDetector == null)
                {
                    _fluidDetector = pd?.GetComponent<FluidDetector>();
                    if (_fluidDetector != null)
                    {
                        _fluidDetector.OnEnterFluidType += OnFluidEntered;
                        _fluidDetector.OnExitFluidType  += OnFluidExited;
                    }
                }
            }

            // ── 3b. Water depth check — stop if submerged or in undertow (without suit) ──
            if (_inWater && !PlayerState.IsWearingSuit()
                && (PlayerState.IsCameraUnderwater() || PlayerState.InUndertowVolume()))
            {
                ScreenReader.Say(Loc.Get("auto_walk_hazard", Loc.Get("fluid_deep_water")), SpeechPriority.Now);
                StopWalk(announce: false);
                return;
            }

            // ── 4. Distance check — arrived? ─────────────────────────────────
            Vector3 up         = playerTr.up;
            Vector3 toTarget3D = _target.position - playerTr.position;
            Vector3 vertVec    = Vector3.Project(toTarget3D, up);
            Vector3 horizVec   = toTarget3D - vertVec;
            float   horizDist  = horizVec.magnitude;
            float   vertDist   = vertVec.magnitude;

            // Progress telemetry every ~2s (session 29 reliability campaign)
            if (Time.time >= _progressLogTime)
            {
                _progressLogTime = Time.time + ProgressLogInterval;
                int pathCount = _path != null ? _path.Count : 0;
                bool grnd     = _playerController != null && _playerController.IsGrounded();
                DebugLogger.LogAutoWalk("PROGRESS t=" +
                    (Time.time - _sessionStartTime).ToString("F1") + "s horizDist=" +
                    horizDist.ToString("F1") + "m wp=" + _waypointIndex + "/" + pathCount +
                    " stuck=" + _stuckRescanCount + " air=" + _airborneTime.ToString("F2") +
                    " grounded=" + grnd + " segmented=" + _segmented +
                    " fallbacks=" + _sessionFallbackCount);
            }

            if (horizDist <= PathConstants.ArrivalDist && vertDist > PathConstants.ArrivalVertMax && !_postArrivalAligning)
            {
                ScreenReader.Say(Loc.Get("auto_walk_out_of_reach", _targetName));
                StopWalk(announce: false);
                return;
            }

            if (horizDist <= PathConstants.ArrivalDist && !_postArrivalAligning)
            {
                _onArrival?.Invoke();

                // Instant body yaw + camera pitch toward collider
                AlignBodyYawToTarget(playerTr, _target);
                AlignCameraPitchToTarget(playerTr, _target);

                _injectedAxis        = Vector2.zero;
                _postArrivalAligning = true;
                _alignEndTime        = Time.time + AlignTimeout;
                _pendingArrivalMsg   = Loc.Get("auto_walk_arrived", _targetName);

                // Wait 1 frame for direct aim check before sweeping
                if (_targetIsInteractable)
                {
                    _directAimPending = true;
                    _sweepingPitch    = false;
                }
            }

            // Post-arrival alignment
            if (_postArrivalAligning)
            {
                if (Time.time >= _alignEndTime)
                {
                    _sweepingPitch = false;
                    ScreenReader.Say(_pendingArrivalMsg ?? "");
                    _pendingArrivalMsg = null;
                    StopWalk(announce: false);
                    return;
                }

                // Direct aim check: after 1 frame, test if our calculated aim
                // already hit the target before resorting to the sweep.
                if (_directAimPending)
                {
                    _directAimPending = false;
                    var fpmDirect = Locator.GetPlayerCamera()?.GetComponent<FirstPersonManipulator>();
                    if (fpmDirect != null && HasAnyFocus(fpmDirect))
                    {
                        DebugLogger.Log(LogCategory.State, "AutoWalk",
                            "Direct aim hit — no sweep needed");
                        ScreenReader.Say(_pendingArrivalMsg ?? "");
                        _pendingArrivalMsg = null;
                        StopWalk(announce: false);
                        return;
                    }

                    // Direct aim missed — start sweep fallback
                    _sweepingPitch = true;
                    _sweepPitch    = SweepStart;
                    var camFallback = Locator.GetPlayerCameraController();
                    if (camFallback != null) camFallback.SetDegreesY(_sweepPitch);
                    DebugLogger.Log(LogCategory.State, "AutoWalk",
                        "Direct aim missed — starting sweep");
                }

                // Pitch sweep: scan from +30° to -60° looking for interactable focus
                if (_sweepingPitch)
                {
                    var camCtrl = Locator.GetPlayerCameraController();
                    if (camCtrl != null)
                    {
                        // Check if the game detected ANY focus (interact, repair, nomai, item)
                        var fpm = Locator.GetPlayerCamera()?.GetComponent<FirstPersonManipulator>();
                        if (fpm != null && HasAnyFocus(fpm))
                        {
                            // Found it! Stop sweep and announce
                            _sweepingPitch = false;
                            DebugLogger.Log(LogCategory.State, "AutoWalk",
                                $"Sweep found focus at pitch {_sweepPitch:F1}°");
                            ScreenReader.Say(_pendingArrivalMsg ?? "");
                            _pendingArrivalMsg = null;
                            StopWalk(announce: false);
                            return;
                        }

                        // Continue sweeping down
                        _sweepPitch -= SweepSpeed * Time.deltaTime;
                        if (_sweepPitch < SweepEnd)
                        {
                            _sweepingPitch = false; // sweep exhausted, let timeout handle it
                        }
                        else
                        {
                            camCtrl.SetDegreesY(_sweepPitch);
                        }
                    }
                }

                _injectedAxis = Vector2.zero;
            }

            // ── 5. Tick A* jump cooldown ──────────────────────────────────────
            if (_jumpCooldown > 0f)
                _jumpCooldown -= Time.deltaTime;

            Vector3 worldDir;

            if (!_postArrivalAligning)
            {
                // ── 6. Periodic rescan ───────────────────────────────────────
                // Sets the flag instead of nulling _path so AcceptOrRejectNewPath has a
                // C1 fallback to keep on flip-flop rejection (see _needsRescan comment).
                // Session 31: gate on grounded + not mid-jump. Rescanning while airborne
                // produces unstable paths (mid-air position yields different A* each frame).
                // Timer still advances so we don't accumulate rescan debt.
                bool recentJump = _lastJumpTime > 0f || _airborneTime > 0.1f;
                if (Time.time >= _rescanTimer)
                {
                    _rescanTimer = Time.time + RescanInterval;
                    if (!recentJump)
                        _needsRescan = true;
                }

                // ── 7. Post-jump rescan — recompute path once mid-arc ──────
                // One-shot: fires PostJumpRescanDelay seconds after jump fire, signals a
                // recompute on the next frame. Does NOT clear _lastJumpTime (cleared in
                // section 10b on landing) and does NOT null _path (kept alive as C1 anchor).
                // Session 31: removed mid-arc rescan (_needsRescan no longer set here).
                // Rescanning 0.3s after jump while airborne produced wildly different paths
                // each bounce, sending the player zigzagging (17-jump Mica run 2). Now we
                // just mark the one-shot done; section 10b triggers rescan on landing.
                if (_lastJumpTime > 0f
                    && Time.time - _lastJumpTime > PostJumpRescanDelay
                    && !_postJumpRescanDone)
                {
                    _postJumpRescanDone = true;
                }

                // ── 8. Compute path if needed (with segmentation for long distances)
                // Triggers: no current path, OR a rescan has been requested via the
                // _needsRescan flag (periodic timer / post-jump one-shot). The flag-based
                // form keeps the existing _path alive across the candidate computation,
                // so AcceptOrRejectNewPath can compare candidate-vs-current and reject
                // a flip-flop without losing the fallback (cf C1 hysteresis).
                if (_path == null || _needsRescan)
                {
                    var oldPath = _path;
                    _needsRescan = false;

                    if (horizDist < PathConstants.DirectPathDist)
                    {
                        // Direct path is unconditional — no C1, no flip-flop concern.
                        _path = new List<PathWaypoint>();
                        _path.Add(new PathWaypoint
                        {
                            Position  = _target.position,
                            NeedsJump = false
                        });
                        _segmented = false;
                    }
                    else if (horizDist > SegmentDistance)
                    {
                        // Segment: compute A* toward intermediate goal at SegmentDistance
                        _segmentGoal = playerTr.position + horizVec.normalized * SegmentDistance;
                        _path = AcceptOrRejectNewPath(
                            _pathScanner.FindPath(playerTr.position, up, _segmentGoal),
                            playerTr.position, up, _segmentGoal);
                        _segmented = true;
                    }
                    else
                    {
                        _path = AcceptOrRejectNewPath(
                            _pathScanner.FindPath(playerTr.position, up, _target.position),
                            playerTr.position, up, _target.position);
                        _segmented = false;
                    }

                    // If progress-monotonic C1 kept the previous path (reference equality),
                    // preserve the existing waypoint index so the player keeps walking from
                    // where they were. Only reset the index when the path object actually changed.
                    if (!ReferenceEquals(_path, oldPath))
                    {
                        _waypointIndex = 0;
                    }

                    if (_path == null || _path.Count == 0)
                    {
                        // No A* path — would normally walk straight to target as fallback.
                        // Phase 1.1 stage 5b: verify ground continuity first. If the straight
                        // line crosses a void, refuse to start instead of walking into it.
                        Vector3 fallbackTarget = _segmented ? _segmentGoal : _target.position;
                        _sessionFallbackCount++;
                        DebugLogger.LogAutoWalk("FALLBACK #1 (first compute) horizDist=" +
                            horizDist.ToString("F1") + "m segmented=" + _segmented);
                        if (!IsStraightLinePathSafe(playerTr.position, fallbackTarget, up))
                        {
                            DebugLogger.LogAutoWalk("FALLBACK #1 UNSAFE → StopWalk");
                            ScreenReader.Say(Loc.Get("auto_walk_unsafe_path"), SpeechPriority.Now);
                            StopWalk(announce: false);
                            return;
                        }
                        DebugLogger.LogAutoWalk("FALLBACK #1 SAFE → straight-line waypoint");
                        _path = new List<PathWaypoint>();
                        _path.Add(new PathWaypoint
                        {
                            Position  = fallbackTarget,
                            NeedsJump = false
                        });
                        _segmented = false;
                    }

                    DebugLogger.Log(LogCategory.State, "AutoWalk",
                        "Path: " + _path.Count + " waypoints, dist=" + horizDist.ToString("F1") + "m"
                        + (_segmented ? " (segment)" : ""));
                }

                // ── 9. Advance past reached waypoints ────────────────────────
                while (_waypointIndex < _path.Count)
                {
                    Vector3 toWp     = _path[_waypointIndex].Position - playerTr.position;
                    Vector3 toWpFlat = toWp - Vector3.Project(toWp, up);
                    if (toWpFlat.magnitude > PathConstants.WaypointReachDist) break;
                    _waypointIndex++;
                }

                // All waypoints consumed → compute next segment or rescan
                if (_waypointIndex >= _path.Count)
                {
                    if (_segmented)
                    {
                        // Segment consumed — immediately compute next segment
                        _path = null;  // triggers section 8 recompute this frame
                        _waypointIndex = 0;
                        _rescanTimer = Time.time + RescanInterval;

                        // Re-enter section 8 logic inline for immediate response
                        if (horizDist < PathConstants.DirectPathDist)
                        {
                            _path = new List<PathWaypoint>();
                            _path.Add(new PathWaypoint
                            {
                                Position  = _target.position,
                                NeedsJump = false
                            });
                            _segmented = false;
                        }
                        else if (horizDist > SegmentDistance)
                        {
                            _segmentGoal = playerTr.position + horizVec.normalized * SegmentDistance;
                            _path = AcceptOrRejectNewPath(
                                _pathScanner.FindPath(playerTr.position, up, _segmentGoal),
                                playerTr.position, up, _segmentGoal);
                            _segmented = true;
                        }
                        else
                        {
                            _path = AcceptOrRejectNewPath(
                                _pathScanner.FindPath(playerTr.position, up, _target.position),
                                playerTr.position, up, _target.position);
                            _segmented = false;
                        }

                        if (_path == null || _path.Count == 0)
                        {
                            // Stage 5b safety raycast (Phase 1.1 session 29) — see first fallback for rationale.
                            Vector3 fbTarget = _segmented ? _segmentGoal : _target.position;
                            _sessionFallbackCount++;
                            DebugLogger.LogAutoWalk("FALLBACK #2 (segment-consumed/segmented) horizDist=" +
                                horizDist.ToString("F1") + "m");
                            if (!IsStraightLinePathSafe(playerTr.position, fbTarget, up))
                            {
                                DebugLogger.LogAutoWalk("FALLBACK #2 UNSAFE → StopWalk");
                                ScreenReader.Say(Loc.Get("auto_walk_unsafe_path"), SpeechPriority.Now);
                                StopWalk(announce: false);
                                return;
                            }
                            DebugLogger.LogAutoWalk("FALLBACK #2 SAFE → straight-line waypoint");
                            _path = new List<PathWaypoint>();
                            _path.Add(new PathWaypoint
                            {
                                Position  = fbTarget,
                                NeedsJump = false
                            });
                            _segmented = false;
                        }
                    }
                    else
                    {
                        if (horizDist < PathConstants.DirectPathDist)
                        {
                            _path = new List<PathWaypoint>();
                            _path.Add(new PathWaypoint
                            {
                                Position  = _target.position,
                                NeedsJump = false
                            });
                        }
                        else
                        {
                            _path = AcceptOrRejectNewPath(
                                _pathScanner.FindPath(playerTr.position, up, _target.position),
                                playerTr.position, up, _target.position);
                        }

                        if (_path == null || _path.Count == 0)
                        {
                            // Stage 5b safety raycast (Phase 1.1 session 29) — see first fallback for rationale.
                            _sessionFallbackCount++;
                            DebugLogger.LogAutoWalk("FALLBACK #3 (segment-consumed/non-seg) horizDist=" +
                                horizDist.ToString("F1") + "m");
                            if (!IsStraightLinePathSafe(playerTr.position, _target.position, up))
                            {
                                DebugLogger.LogAutoWalk("FALLBACK #3 UNSAFE → StopWalk");
                                ScreenReader.Say(Loc.Get("auto_walk_unsafe_path"), SpeechPriority.Now);
                                StopWalk(announce: false);
                                return;
                            }
                            DebugLogger.LogAutoWalk("FALLBACK #3 SAFE → straight-line waypoint");
                            _path = new List<PathWaypoint>();
                            _path.Add(new PathWaypoint
                            {
                                Position  = _target.position,
                                NeedsJump = false
                            });
                        }
                    }
                    _waypointIndex = 0;
                    _rescanTimer = Time.time + RescanInterval;
                    // Fall through — rotation will use the new path this frame
                }

                PathWaypoint wp = _path[_waypointIndex];

                // ── 10. Ground state + movement tracking ──────────────────────
                bool suitUnderwater = PlayerState.IsWearingSuit() && PlayerState.IsCameraUnderwater();
                bool grounded = (_playerController != null && _playerController.IsGrounded())
                    || suitUnderwater;

                // 10a. Airborne detection — stop if falling too long (not a planned jump)
                if (!grounded)
                {
                    _airborneTime += Time.deltaTime;

                    // Diagnostic: log when post-jump grace expires while still airborne (overshoot signal)
                    if (_lastJumpTime > 0f && Time.time - _lastJumpTime > PostJumpRescanDelay
                        && !_jumpGraceExpiredLogged)
                    {
                        DebugLogger.Log(LogCategory.State, "AutoWalkJump",
                            "GRACE_EXPIRED still airborne after " +
                            (Time.time - _lastJumpTime).ToString("F2") + "s" +
                            " airborneTime=" + _airborneTime.ToString("F2") +
                            " pos=" + playerTr.position.ToString("F2"));
                        _jumpGraceExpiredLogged = true;
                    }

                    if (_airborneTime >= MaxAirborneBeforeStop && _lastJumpTime == 0f)
                    {
                        DebugLogger.LogAutoWalk("AIRBORNE_STOP time=" + _airborneTime.ToString("F2") +
                            "s (non-jump fall)");
                        ScreenReader.Say(Loc.Get("auto_walk_stuck"), SpeechPriority.Now);
                        StopWalk(announce: false);
                        return;
                    }

                    // Absolute airborne ceiling — fires regardless of _lastJumpTime guard.
                    // Phase 1.2 session 29: catches the edge case where a jump passes safety
                    // raycasts but the landing still fails (e.g. surface collapses, collider phasing,
                    // boosted past the predicted landing zone). Without this, PostJumpRescanDelay
                    // gated the airborne stop and the player fell unchecked.
                    if (_airborneTime >= AbsoluteAirborneCeiling)
                    {
                        DebugLogger.Log(LogCategory.State, "AutoWalkJump",
                            "CEILING_ABORT airborneTime=" + _airborneTime.ToString("F2") +
                            "s lastJumpTime=" + _lastJumpTime.ToString("F2"));
                        DebugLogger.LogAutoWalk("AIRBORNE_CEILING time=" +
                            _airborneTime.ToString("F2") + "s lastJump=" +
                            _lastJumpTime.ToString("F2"));
                        ScreenReader.Say(Loc.Get("auto_walk_stuck"), SpeechPriority.Now);
                        StopWalk(announce: false);
                        return;
                    }
                }
                else
                {
                    // Just landed — reset airborne timer, stuck counter, and jump guards.
                    // Session 31: rescan is now triggered HERE on landing (via _needsRescan)
                    // instead of mid-arc in section 7. Grounded position gives A* stable input.
                    if (!_wasGrounded)
                    {
                        _stuckRescanCount    = 0;
                        _stuckStartHorizDist = float.MaxValue;
                        // Session 31: trigger rescan on landing instead of mid-arc.
                        // The player's grounded position is stable and gives A* a
                        // reliable starting point (unlike mid-air rescans).
                        _needsRescan = true;
                        _lastJumpTime = 0f;
                        _postJumpRescanDone = false;
                        DebugLogger.Log(LogCategory.State, "AutoWalk",
                            "Landed after airborne=" + _airborneTime.ToString("F2") + "s" +
                            " pos=" + playerTr.position.ToString("F2"));
                    }
                    _airborneTime = 0f;
                    _jumpGraceExpiredLogged = false;

                    // 10b. Slope check via raycast — if standing on slope > 45°, rescan.
                    // BUG #1 fix (session 29): the raycast starts 0.5m above the player's
                    // transform, which is inside the player's own capsule collider. Without
                    // filtering, the cast hit Player_Body first and treated it as ground.
                    // We now use RaycastAll + filter to skip the player's own rigidbody.
                    if (TryRaycastExcludingPlayer(playerTr.position + up * 0.5f, -up, 2f,
                        out RaycastHit slopeHit))
                    {
                        float slopeAngle = Vector3.Angle(up, slopeHit.normal);
                        if (slopeAngle > 45f)
                        {
                            _needsRescan = true;
                            DebugLogger.Log(LogCategory.State, "AutoWalk",
                                "Steep slope " + slopeAngle.ToString("F0") + "° — rescan");
                        }
                    }
                }
                _wasGrounded = grounded;

                // 10c. Movement distance check (every 0.5s) — stuck detection.
                //
                // Session 29+ fix: measure the displacement in the groundBody's local
                // frame rather than Unity world, because the game's floating-origin
                // "Center of the Universe" system makes transform.position track the
                // sector rather than physical walking motion. See the block comment on
                // _moveCheckPos for the full rationale. Falls back to world frame only
                // if the groundBody isn't available (airborne or scene transition).
                var groundBodyStk = _playerController != null
                    ? _playerController.GetGroundBody() : null;
                Vector3 playerCheckPos = groundBodyStk != null
                    ? groundBodyStk.transform.InverseTransformPoint(playerTr.position)
                    : playerTr.position;

                if (!_moveCheckInit)
                {
                    _moveCheckPos  = playerCheckPos;
                    _moveCheckInit = true;
                }

                if (Time.time - _moveCheckTime >= MoveCheckInterval)
                {
                    float moved = Vector3.Distance(playerCheckPos, _moveCheckPos);
                    // Dual criterion: barely moved AND still on same waypoint
                    _isStuck       = moved < MoveThreshold && _waypointIndex == _lastCheckWaypointIndex;
                    _moveCheckPos  = playerCheckPos;
                    _moveCheckTime = Time.time;
                    _lastCheckWaypointIndex = _waypointIndex;

                    if (_isStuck)
                    {
                        if (_stuckRescanCount == 0)
                            _stuckStartHorizDist = horizDist;
                        _stuckRescanCount++;
                        DebugLogger.Log(LogCategory.State, "AutoWalk",
                            "Stuck — rescan " + _stuckRescanCount + "/" + MaxStuckRescans);
                        DebugLogger.LogAutoWalk("STUCK wp=" + _waypointIndex + "/" +
                            (_path != null ? _path.Count : 0) + " moved=" + moved.ToString("F3") +
                            "m (local) rescan=" + _stuckRescanCount + "/" + MaxStuckRescans);

                        if (_stuckRescanCount >= MaxStuckRescans)
                        {
                            DebugLogger.LogAutoWalk("STUCK_ABORT max rescans reached → StopWalk");
                            ScreenReader.Say(Loc.Get("auto_walk_stuck"), SpeechPriority.Now);
                            StopWalk(announce: false);
                            return;
                        }

                        // Force path rescan on next frame
                        _path = null;
                    }
                    // Session 31: only reset stuck counter when the player has made real
                    // progress (horizDist decreased by ≥1m from when stuck began). Previously
                    // reset whenever moved > 0.05m in 0.5s, which caused the counter to
                    // oscillate 0↔1 forever — player wiggles 5cm trying a new direction,
                    // counter resets, then gets stuck again next check. Observed: 28s stuck
                    // at 9.1m from Mica, counter never reached 4/4 STUCK_ABORT.
                    else if ((grounded || suitUnderwater)
                        && horizDist < _stuckStartHorizDist - 1f)
                    {
                        _stuckRescanCount    = 0;
                        _stuckStartHorizDist = float.MaxValue;
                    }
                }

                // ── 11. A* jump waypoints (path-planned, grounded only) ──────
                if (wp.NeedsJump && _jumpCooldown <= 0f && _jumpEnabled)
                {
                    Vector3 toJumpWp    = wp.Position - playerTr.position;
                    Vector3 toJumpHoriz = toJumpWp - Vector3.Project(toJumpWp, up);
                    if (toJumpHoriz.magnitude <= JumpTriggerDist)
                    {
                        FireJumpWithBoost(playerTr, up, toJumpHoriz, "astar");
                    }
                }

                // ── 12. Game jump prompt — jump when the game says to ──────
                // Session 29+ gate: require a valid current waypoint before firing the
                // prompt jump. Without this gate, a STUCK rescan (which sets `_path = null`
                // one frame before the new FindPath runs) can fall through into this block
                // and call FireJumpWithBoost with a stale/empty path, producing a
                // JUMPBOOST log with wpPos=(0,0,0) and a meaningless horizDir fallback.
                // Observed in the 2026-04-11 Mica session 2 log.
                if (_jumpCooldown <= 0f && _jumpEnabled
                    && _path != null && _waypointIndex < _path.Count
                    && CheckJumpPromptVisible())
                {
                    // Session 29 step B: suppress prompt jumps during the first second of
                    // the walk to avoid firing a jump immediately when the player happens
                    // to start at a ledge edge (see Ardoise-return scenario).
                    if (Time.time < _jumpPromptGraceEndTime)
                    {
                        if (!_jumpPromptGraceLoggedOnce)
                        {
                            DebugLogger.LogAutoWalk("PROMPT_SUPPRESSED grace_remaining=" +
                                (_jumpPromptGraceEndTime - Time.time).ToString("F2") + "s");
                            _jumpPromptGraceLoggedOnce = true;
                        }
                    }
                    // Session 31: post-jump cooldown — after a prompt jump, let the player
                    // WALK for 3s before allowing another prompt jump. Prevents the 13-17
                    // jump loop observed on Mica (player re-enters JumpPromptTrigger on
                    // landing and immediately re-jumps, bouncing in place for 30s).
                    else if (Time.time < _promptJumpCooldownEnd)
                    {
                        if (!_promptCooldownLoggedOnce)
                        {
                            DebugLogger.LogAutoWalk("PROMPT_COOLDOWN duration=" +
                                PromptJumpCooldown.ToString("F0") + "s");
                            _promptCooldownLoggedOnce = true;
                        }
                    }
                    // Session 31: cap on prompt jumps without progress. After 4 jumps
                    // without horizDist improving by 3m, disable prompt jumps entirely.
                    // Data shows: fewer jumps = higher success rate (2 jumps → 20s arrival,
                    // 12 jumps → 57s failure). Walking around is better than bouncing.
                    else if (_promptJumpsDisabled)
                    {
                        // Already disabled — do nothing, let the player walk.
                    }
                    else
                    {
                        // Track progress across prompt jumps.
                        if (_promptJumpCount == 0)
                            _promptJumpRefHorizDist = horizDist;

                        _promptJumpCount++;

                        // Check if we've made progress since the reference.
                        float improvement = _promptJumpRefHorizDist - horizDist;
                        if (improvement >= PromptJumpProgressThreshold)
                        {
                            // Real progress — reset the counter.
                            _promptJumpCount        = 1;
                            _promptJumpRefHorizDist = horizDist;
                        }

                        if (_promptJumpCount > MaxPromptJumpsWithoutProgress)
                        {
                            _promptJumpsDisabled = true;
                            DebugLogger.LogAutoWalk("PROMPT_JUMPS_DISABLED count=" +
                                _promptJumpCount + " refDist=" +
                                _promptJumpRefHorizDist.ToString("F1") +
                                "m currentDist=" + horizDist.ToString("F1") + "m");
                        }
                        else
                        {
                            // Session 31 fix: boost STRAIGHT FORWARD for prompt jumps.
                            // JumpPromptTrigger volumes are designer-placed at spots where
                            // jumping FORWARD is the correct action. Previously we boosted
                            // toward the A* waypoint, which could point sideways (observed:
                            // localBoost=(3.42, 0, 2.07) = 59° right → player launched off
                            // course, run failed). The waypoint direction is irrelevant here
                            // — the game knows where to jump, we just need to go forward.
                            Vector3 playerFwd = playerTr.forward;
                            Vector3 fwdH = playerFwd - Vector3.Project(playerFwd, up);
                            FireJumpWithBoost(playerTr, up, fwdH, "prompt");
                            _promptJumpCooldownEnd    = Time.time + PromptJumpCooldown;
                            _promptCooldownLoggedOnce = false;
                        }
                    }
                }

                // ── 13. Walk toward current waypoint ─────────────────────────
                Vector3 toWaypoint = wp.Position - playerTr.position;
                Vector3 toWpHoriz  = toWaypoint - Vector3.Project(toWaypoint, up);

                if (toWpHoriz.sqrMagnitude < 0.001f)
                {
                    _injectedAxis = Vector2.zero;
                    return;
                }

                worldDir = toWpHoriz.normalized;

                // Face direction check — don't walk backward
                Vector3 playerFwdFlat = playerTr.forward
                    - Vector3.Project(playerTr.forward, up);
                float facingDot = (playerFwdFlat.sqrMagnitude > 0.001f)
                    ? Vector3.Dot(playerFwdFlat.normalized, worldDir)
                    : 0f;

                Vector2 decidedAxis;
                string  axisMode;
                if (facingDot < 0.5f)
                {
                    // Facing away — rotate only, don't walk into walls
                    _injectedAxis = Vector2.zero;
                    decidedAxis = Vector2.zero;
                    axisMode = "ROTATE_ONLY";
                }
                else
                {
                    float   axisX = Vector3.Dot(worldDir, playerTr.right);
                    float   axisY = Vector3.Dot(worldDir, playerTr.forward);
                    Vector2 axis  = new Vector2(axisX, axisY);
                    if (axis.magnitude > 1f) axis = axis.normalized;
                    _injectedAxis = axis;
                    decidedAxis = axis;
                    axisMode = "WALK";
                }

                // Walking telemetry (Phase B session 29) — fires every ~0.25s
                // to trace why the player isn't progressing. Captures waypoint target,
                // direction math, facing dot, injected axis, player velocity.
                if (Time.time >= _walkingLogTime)
                {
                    _walkingLogTime = Time.time + WalkingLogInterval;
                    float wpHorizDist = toWpHoriz.magnitude;
                    float rotAngleNeeded = (playerFwdFlat.sqrMagnitude > 0.001f)
                        ? Vector3.SignedAngle(playerFwdFlat.normalized, worldDir, up)
                        : 0f;
                    var owBodyW = Locator.GetPlayerBody();
                    Vector3 velNowW = owBodyW != null ? owBodyW.GetVelocity() : Vector3.zero;
                    DebugLogger.LogAutoWalk("WALKING wp=" + _waypointIndex + "/" +
                        (_path != null ? _path.Count : 0) +
                        " wpPos=" + wp.Position.ToString("F2") +
                        " wpHorizDist=" + wpHorizDist.ToString("F2") +
                        "m worldDir=" + worldDir.ToString("F2") +
                        " pFwd=" + (playerFwdFlat.sqrMagnitude > 0.001f ? playerFwdFlat.normalized.ToString("F2") : "ZERO") +
                        " facingDot=" + facingDot.ToString("F2") +
                        " rotNeeded=" + rotAngleNeeded.ToString("F0") +
                        "° mode=" + axisMode +
                        " axis=" + decidedAxis.ToString("F2") +
                        " vel=|" + velNowW.magnitude.ToString("F1") + "|");

                    // (PCC diagnostic removed — Mica investigation complete)
                }
            }
            else
            {
                // Post-arrival: compute worldDir for alignment rotation
                Vector3 toTarget = _target.position - playerTr.position;
                Vector3 horizontal = toTarget - Vector3.Project(toTarget, up);
                if (horizontal.sqrMagnitude < 0.001f)
                {
                    ScreenReader.Say(_pendingArrivalMsg ?? "");
                    _pendingArrivalMsg = null;
                    StopWalk(announce: false);
                    return;
                }
                worldDir = horizontal.normalized;
            }

            _injecting    = true;
            _injectedLook = Vector2.zero;

            // Post-arrival alignment check (skip if sweep is still scanning)
            if (_postArrivalAligning && !_sweepingPitch)
            {
                Vector3 pFwdCheck = playerTr.forward
                    - Vector3.Project(playerTr.forward, up);
                if (pFwdCheck.sqrMagnitude > 0.001f
                    && Vector3.Dot(pFwdCheck.normalized, worldDir) >= FaceAlignFinal)
                {
                    ScreenReader.Say(_pendingArrivalMsg ?? "");
                    _pendingArrivalMsg = null;
                    StopWalk(announce: false);
                    return;
                }
            }

            // ── 14. Direct body rotation ─────────────────────────────────────
            Vector3 pFwd = playerTr.forward - Vector3.Project(playerTr.forward, up);
            if (pFwd.sqrMagnitude > 0.001f)
            {
                pFwd.Normalize();
                float rotAngle  = Vector3.SignedAngle(pFwd, worldDir, up);
                float rotSpeed  = Mathf.Clamp(Mathf.Abs(rotAngle) * 5f, 30f, 720f);
                float rotMax    = rotSpeed * Time.deltaTime;
                float rotActual = Mathf.Clamp(rotAngle, -rotMax, rotMax);
                playerTr.rotation = Quaternion.AngleAxis(rotActual, up) * playerTr.rotation;
            }
        }

        #endregion

        #region Harmony patches

        /// <summary>
        /// Harmony prefix for OWInput.GetAxisValue.
        /// Injects movement and look input when auto-walk is active.
        /// </summary>
        public static bool PrefixGetAxisValue(IInputCommands command, ref Vector2 __result)
        {
            if (_injecting)
            {
                if (command == InputLibrary.moveXZ)
                {
                    __result = _injectedAxis;
                    return false;
                }
                if (command == InputLibrary.look)
                {
                    __result = _injectedLook;  // Vector2.zero — blocks stick drift
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Harmony postfix on UpdateJumpInput — injects jump after game's own logic.
        /// Only fires when the player is grounded to prevent mid-air stacking.
        /// </summary>
        private static void PostfixUpdateJumpInput(PlayerCharacterController __instance)
        {
            if (_wantJumpFrames <= 0 || !_jumpEnabled) return;
            // Never jump while airborne — prevents velocity stacking
            if (!__instance.IsGrounded())
            {
                _wantJumpFrames = 0;
                return;
            }
            try
            {
                _fieldJumpCharge.SetValue(__instance, _wantJumpCharge);
                _fieldJumpNext.SetValue(__instance, true);
                _wantJumpFrames--;
            }
            catch (System.Exception ex)
            {
                _wantJumpFrames = 0;
                DebugLogger.LogState("[AutoWalkHandler] PostfixUpdateJumpInput error: " + ex.Message);
            }
        }

        /// <summary>
        /// Harmony postfix on UpdateTurning — registered but disabled.
        /// Direct rotation in Update() is more reliable.
        /// </summary>
        private static void PostfixUpdateTurning(PlayerCharacterController __instance)
        {
        }

        #endregion

        #region Private helpers

        private void RequestJump(float charge = 1.0f)
        {
            _wantJumpFrames = JumpPersistFrames;
            _wantJumpCharge = charge;
            DebugLogger.Log(LogCategory.State, "AutoWalk",
                "Jump requested (charge=" + charge.ToString("F2") + ")");
        }

        // Stage 5b safety raycast constants (Phase 1.1 session 29).
        // When A* fails and the handler falls back to a direct line-to-target waypoint,
        // we sample ~10 points along the horizontal segment and raycast downward to confirm
        // there's stable ground all the way. If too many samples miss the ground, we refuse
        // to start rather than walking the player into the void.
        private const int   UnsafePathSamples          = 10;
        private const float UnsafePathProbeUpOffset    = 2f;   // start raycast this far above sample point
        private const float UnsafePathProbeDownLength  = 6f;   // max raycast length downward
        private const float UnsafePathMaxMissRatio     = 0.2f; // > 20% missing → refuse

        /// <summary>
        /// Stage 5b safety check — samples the horizontal segment from `from` to `to`
        /// and verifies each sample has ground within UnsafePathProbeDownLength metres.
        /// Returns true if the straight-line walk is safe enough (≤ 20% missing samples).
        /// Used only on A* fallback paths; when A* succeeds, per-cell probe validation
        /// already guarantees ground continuity.
        /// </summary>
        // Reusable hit buffer for filtered raycasts (session 29 — BUG #1 fix).
        // Size 16 is more than enough for any cast we do: at most a handful of colliders
        // between the probe origin and the max distance.
        private readonly RaycastHit[] _rayHitBuffer = new RaycastHit[16];

        /// <summary>
        /// Raycast variant that skips any hit belonging to the player's own rigidbody.
        /// Mirrors the PathScanner.SphereCastExcludingPlayer pattern.
        /// Returns the nearest non-player hit, or false if none.
        /// </summary>
        private bool TryRaycastExcludingPlayer(Vector3 origin, Vector3 direction, float maxDistance,
            out RaycastHit hit)
        {
            var playerBody = Locator.GetPlayerBody();
            Rigidbody playerRb = playerBody != null ? playerBody.GetRigidbody() : null;

            int count = Physics.RaycastNonAlloc(origin, direction, _rayHitBuffer, maxDistance,
                OWLayerMask.physicalMask, QueryTriggerInteraction.Ignore);

            hit = default;
            bool found = false;
            float bestDist = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = _rayHitBuffer[i];
                if (h.collider == null) continue;
                if (playerRb != null && h.rigidbody == playerRb) continue;
                if (h.collider.GetComponentInParent<PlayerCharacterController>() != null) continue;
                if (h.distance < bestDist)
                {
                    bestDist = h.distance;
                    hit = h;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>
        /// Phase C (session 29+): progress-monotonic candidate filter.
        ///
        /// Replaces the earlier angle-based C1 hysteresis (which compared wp1 direction
        /// vectors and rejected flips &gt; 120°). The angle approach failed for two
        /// reasons:
        ///   1. After a jump, the wp1 direction often legitimately flips by &gt; 90°
        ///      because the player landed somewhere new — the angle check rejected the
        ///      correction or forced a bad acceptance.
        ///   2. The "anchor direction" was stale and didn't reflect actual progress
        ///      toward the target.
        ///
        /// New logic: compare the candidate path's ENDPOINT distance to the target
        /// against the current path's ENDPOINT distance. Accept if the candidate
        /// gets us meaningfully closer; reject if it would take us meaningfully
        /// further. Hysteresis on the "similar progress" range prevents thrashing
        /// between equivalent options. Stuck bypass still applies.
        /// </summary>
        private List<PathWaypoint> AcceptOrRejectNewPath(
            List<PathWaypoint> candidate, Vector3 playerPos, Vector3 up, Vector3 target)
        {
            bool hasCurrent = _path != null && _path.Count > 0 && _waypointIndex < _path.Count;

            // Null/empty candidate handling: keep the existing path if available
            // (a transient FindPath failure shouldn't kill an otherwise-good walk).
            // If we have nothing to fall back to, pass the null through so the caller's
            // fallback logic runs.
            if (candidate == null || candidate.Count == 0)
            {
                if (hasCurrent)
                {
                    DebugLogger.LogAutoWalk("RESCAN_FAIL_KEEP_PATH candidate=null waypoints_left=" +
                        (_path.Count - _waypointIndex));
                    return _path;
                }
                return candidate;
            }

            // No current path → unconditionally accept the candidate. This is the
            // very first path of the session, or we've consumed the previous one.
            if (!hasCurrent)
            {
                return candidate;
            }

            // Stuck bypass — after enough stuck rescans, accept any direction the
            // planner offers because the current path clearly isn't working.
            if (_stuckRescanCount >= MaxStuckRescansBeforeFlipAllowed)
            {
                DebugLogger.LogAutoWalk("PROGRESS_BYPASSED stuck=" + _stuckRescanCount +
                    " → accepting new path");
                return candidate;
            }

            // Compute horizontal distance from each path's endpoint to the target.
            // The endpoint is the path's "best closest" cell (PARTIAL paths) or the
            // actual target cell (EXACT paths). Both representations work because we
            // only care about which path's endpoint sits closer to the target.
            Vector3 currentEnd   = _path[_path.Count - 1].Position;
            Vector3 candidateEnd = candidate[candidate.Count - 1].Position;

            Vector3 toCurrent      = currentEnd   - target;
            Vector3 toCandidate    = candidateEnd - target;
            Vector3 toCurrentFlat  = toCurrent   - Vector3.Project(toCurrent,   up);
            Vector3 toCandidateFlat = toCandidate - Vector3.Project(toCandidate, up);

            float currentEndDist   = toCurrentFlat.magnitude;
            float candidateEndDist = toCandidateFlat.magnitude;
            float improvement = currentEndDist - candidateEndDist; // positive = candidate closer

            if (improvement > ProgressImprovementThreshold)
            {
                // Candidate brings us meaningfully closer — accept.
                DebugLogger.LogAutoWalk("PROGRESS_ACCEPT improvement=" + improvement.ToString("F1") +
                    "m currentEnd=" + currentEndDist.ToString("F1") +
                    "m candidateEnd=" + candidateEndDist.ToString("F1") + "m");
                return candidate;
            }
            if (improvement < -ProgressRegressionThreshold)
            {
                // Candidate would take us meaningfully further from target — reject.
                DebugLogger.LogAutoWalk("PROGRESS_REJECT regression=" + (-improvement).ToString("F1") +
                    "m currentEnd=" + currentEndDist.ToString("F1") +
                    "m candidateEnd=" + candidateEndDist.ToString("F1") + "m");
                return _path;
            }

            // Similar progress (within hysteresis band ±threshold): keep the
            // current path to avoid churning between equivalent options.
            return _path;
        }

        private bool IsStraightLinePathSafe(Vector3 from, Vector3 to, Vector3 up)
        {
            Vector3 delta = to - from;
            Vector3 flat  = delta - Vector3.Project(delta, up);
            float   horizDist = flat.magnitude;
            if (horizDist < PathConstants.DirectPathDist) return true; // too short to sample

            Vector3 horizDir = flat / horizDist;
            int misses = 0;
            for (int i = 1; i <= UnsafePathSamples; i++)
            {
                float t = (float)i / UnsafePathSamples;
                // Sample point on the player's horizontal plane (no vertical component).
                Vector3 samplePt = from + horizDir * (horizDist * t);
                Vector3 castFrom = samplePt + up * UnsafePathProbeUpOffset;

                if (!Physics.Raycast(castFrom, -up, out RaycastHit _,
                    UnsafePathProbeUpOffset + UnsafePathProbeDownLength,
                    OWLayerMask.physicalMask, QueryTriggerInteraction.Ignore))
                {
                    misses++;
                }
            }

            float missRatio = (float)misses / UnsafePathSamples;
            bool safe = missRatio <= UnsafePathMaxMissRatio;
            if (!safe)
            {
                DebugLogger.Log(LogCategory.State, "AutoWalk",
                    "UNSAFE_FALLBACK misses=" + misses + "/" + UnsafePathSamples +
                    " horizDist=" + horizDist.ToString("F1") + "m");
            }
            return safe;
        }

        /// <summary>
        /// Fires a jump and adds horizontal boost toward the waypoint, but only after
        /// validating that the predicted landing zone has stable ground. If any sample
        /// point along the trajectory has no ground or steep slope, the jump is cancelled
        /// and the path is invalidated to force a rescan.
        /// </summary>
        private void FireJumpWithBoost(Transform playerTr, Vector3 up, Vector3 horizDir, string source)
        {
            Vector3 wpPos = (_path != null && _waypointIndex < _path.Count)
                ? _path[_waypointIndex].Position : Vector3.zero;
            float   wpDist    = horizDir.magnitude;
            Vector3 horizDirN = wpDist > 0.01f ? horizDir / wpDist : Vector3.zero;

            // Safety check: predict landing zone and verify ground exists.
            // The boost (4 m/s) + walk speed pushes the player past the waypoint, so we
            // sample at the waypoint AND 2m / 4m beyond it in the same direction. All
            // three points must have stable ground (slope ≤ 60°) within 5m below.
            if (wpDist > 0.01f && _path != null && _waypointIndex < _path.Count)
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector3 samplePoint = wpPos + horizDirN * (i * 2f);
                    Vector3 castFrom    = samplePoint + up * 2f;

                    if (!Physics.Raycast(castFrom, -up, out RaycastHit groundHit, 6f,
                        OWLayerMask.physicalMask, QueryTriggerInteraction.Ignore))
                    {
                        DebugLogger.Log(LogCategory.State, "AutoWalkJump",
                            "CANCEL_NO_GROUND source=" + source + " sample=" + i +
                            " samplePos=" + samplePoint.ToString("F2"));
                        _path = null;          // force rescan
                        _jumpCooldown = 0.3f;  // brief cooldown to avoid immediate re-trigger
                        return;
                    }

                    float slope = Vector3.Angle(up, groundHit.normal);
                    if (slope > 60f)
                    {
                        DebugLogger.Log(LogCategory.State, "AutoWalkJump",
                            "CANCEL_STEEP source=" + source + " sample=" + i +
                            " slope=" + slope.ToString("F0"));
                        _path = null;
                        _jumpCooldown = 0.3f;
                        return;
                    }
                }
            }

            // Safe — fire the jump.
            RequestJump();
            _jumpCooldown = 1.5f;
            _lastJumpTime = Time.time;

            DebugLogger.Log(LogCategory.State, "AutoWalkJump",
                "FIRE source=" + source +
                " playerPos=" + playerTr.position.ToString("F2") +
                " wpIdx=" + _waypointIndex +
                " wpPos=" + wpPos.ToString("F2") +
                " horizDist=" + wpDist.ToString("F2") +
                " boostDir=" + (wpDist > 0.01f ? horizDirN.ToString("F2") : "ZERO") +
                " boostMag=" + JumpHorizBoost);

            var owBody = Locator.GetPlayerBody();
            if (owBody != null && horizDir.sqrMagnitude > 0.01f)
            {
                // Prompt-triggered jumps (designer-placed JumpPromptTrigger zones) get a
                // stronger boost. These zones exist on terrain with steps/ledges that block
                // walking. A standard 4 m/s boost barely clears the obstacle because the
                // player slides back on the slope after landing. 8 m/s covers more ground
                // per jump, reducing 10+ bounce-loops to 2-3 efficient jumps.
                float boostMag = (source == "prompt") ? PromptJumpBoost : JumpHorizBoost;

                Vector3 localBoostDir = playerTr.InverseTransformDirection(horizDir.normalized);
                localBoostDir.y = 0f;
                if (localBoostDir.sqrMagnitude > 0.0001f)
                    localBoostDir.Normalize();

                // Session 31: block backwards jumps. If the waypoint is behind the player,
                // localBoostDir.z is negative and the boost would push them backwards —
                // observed on Mica runs: localBoost=(-3.77, 0, -1.34) sent player away.
                if (localBoostDir.z < 0f)
                {
                    DebugLogger.LogAutoWalk("JUMPBOOST_BLOCKED_BACKWARD source=" + source +
                        " localDir=" + localBoostDir.ToString("F2") +
                        " wpPos=" + wpPos.ToString("F2"));
                    return;
                }

                Vector3 localBoost = localBoostDir * boostMag;

                owBody.AddLocalVelocityChange(localBoost);

                DebugLogger.LogAutoWalk("JUMPBOOST source=" + source +
                    " playerPos=" + playerTr.position.ToString("F2") +
                    " wpPos=" + wpPos.ToString("F2") +
                    " boost=" + boostMag.ToString("F0") + "m/s" +
                    " localBoost=" + localBoost.ToString("F2"));
            }
        }

        private void StopWalk(bool announce)
        {
            // Session end telemetry (session 29 reliability campaign)
            if (_isActive)
            {
                float duration = Time.time - _sessionStartTime;
                Transform pt   = Locator.GetPlayerTransform();
                string pos     = pt != null ? pt.position.ToString("F2") : "null";
                DebugLogger.LogAutoWalk("SESSION_END duration=" + duration.ToString("F1") +
                    "s fallbacks=" + _sessionFallbackCount + " pos=" + pos +
                    " wp=" + _waypointIndex + "/" + (_path != null ? _path.Count : 0) +
                    " stuck=" + _stuckRescanCount);
            }

            _isActive            = false;
            _injecting           = false;
            _injectedAxis        = Vector2.zero;
            _injectedLook        = Vector2.zero;
            _jumpCooldown        = 0f;
            _wantJumpFrames      = 0;
            _jumpGraceExpiredLogged = false;
            _postArrivalAligning = false;
            _pendingArrivalMsg   = null;
            _directAimPending    = false;
            _sweepingPitch       = false;
            _path                = null;
            _waypointIndex       = 0;
            _lastJumpTime        = 0f;
            _postJumpRescanDone  = false;
            _needsRescan         = false;
            _stuckRescanCount    = 0;
            _lastCheckWaypointIndex = 0;
            _moveCheckInit       = false;
            _moveCheckPos        = Vector3.zero;
            _airborneTime        = 0f;
            _wasGrounded         = true;
            _playerController    = null;
            _inWater             = false;
            _segmented           = false;

            if (_hazardDetector != null)
            {
                _hazardDetector.OnHazardsUpdated -= OnHazardUpdated;
                _hazardDetector = null;
            }
            if (_fluidDetector != null)
            {
                _fluidDetector.OnEnterFluidType -= OnFluidEntered;
                _fluidDetector.OnExitFluidType  -= OnFluidExited;
                _fluidDetector = null;
            }

            // Unsubscribe InputMode event (Phase 1.6 session 29)
            if (OWInput.SharedInputManager != null)
                OWInput.SharedInputManager.OnUpdateInputMode -= OnInputModeChanged;

            if (announce)
                ScreenReader.Say(Loc.Get("auto_walk_off"));
            DebugLogger.LogState("[AutoWalkHandler] Stopped.");
        }

        /// <summary>Prompt positions to scan for jump prompts.</summary>
        private static readonly PromptPosition[] _promptPositions = new[]
        {
            PromptPosition.Center,
            PromptPosition.UpperRight,
            PromptPosition.LowerLeft,
            PromptPosition.UpperLeft,
            PromptPosition.BottomCenter
        };

        private bool CheckJumpPromptVisible()
        {
            if (!_promptReflectionReady) return false;
            try
            {
                var pm = Locator.GetPromptManager();
                if (pm == null) return false;

                for (int p = 0; p < _promptPositions.Length; p++)
                {
                    var list = pm.GetScreenPromptList(_promptPositions[p]);
                    if (list == null) continue;

                    var prompts = _fieldListPrompts.GetValue(list)
                                  as System.Collections.Generic.List<ScreenPrompt>;
                    if (prompts == null) continue;

                    for (int i = 0; i < prompts.Count; i++)
                    {
                        if (!prompts[i].IsVisible()) continue;
                        var cmds = prompts[i].GetInputCommandList();
                        for (int j = 0; j < cmds.Count; j++)
                        {
                            if (cmds[j] == InputLibrary.jump)
                                return true;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.LogState("[AutoWalkHandler] CheckJumpPrompt error: " + ex.Message);
            }
            return false;
        }

        /// <summary>
        /// Checks whether FirstPersonManipulator has ANY focused target,
        /// including RepairReceiver, NomaiText, OWItemSocket, OWItem —
        /// not just the InteractReceiver/InteractZone covered by
        /// HasFocusedInteractible().
        /// </summary>
        private static bool HasAnyFocus(FirstPersonManipulator fpm)
        {
            if (fpm == null) return false;

            // Public check covers _interactReceiver and _interactZone
            if (fpm.HasFocusedInteractible()) return true;

            // Lazy init reflection fields
            if (!_focusReflectionReady)
            {
                var rf = System.Reflection.BindingFlags.NonPublic
                       | System.Reflection.BindingFlags.Instance;
                _fieldFocusedRepair     = typeof(FirstPersonManipulator).GetField("_focusedRepairReceiver", rf);
                _fieldFocusedNomaiText  = typeof(FirstPersonManipulator).GetField("_focusedNomaiText", rf);
                _fieldFocusedItemSocket = typeof(FirstPersonManipulator).GetField("_focusedItemSocket", rf);
                _fieldFocusedItem       = typeof(FirstPersonManipulator).GetField("_focusedItem", rf);
                _focusReflectionReady = true;
                DebugLogger.LogState("[AutoWalkHandler] Focus reflection init — repair:"
                    + (_fieldFocusedRepair != null) + " nomai:" + (_fieldFocusedNomaiText != null)
                    + " socket:" + (_fieldFocusedItemSocket != null) + " item:" + (_fieldFocusedItem != null));
            }

            if (_fieldFocusedRepair != null && _fieldFocusedRepair.GetValue(fpm) != null) return true;
            if (_fieldFocusedNomaiText != null && _fieldFocusedNomaiText.GetValue(fpm) != null) return true;
            if (_fieldFocusedItemSocket != null && _fieldFocusedItemSocket.GetValue(fpm) != null) return true;
            if (_fieldFocusedItem != null && _fieldFocusedItem.GetValue(fpm) != null) return true;

            return false;
        }

        /// <summary>
        /// Computes the best aim point for the target: collider center if available,
        /// otherwise 1m above root. Returns the pitch angle to aim the camera there.
        /// </summary>
        /// <summary>
        /// Instantly rotates the player body so it faces the target horizontally.
        /// Only modifies yaw (rotation around up), never pitch.
        /// </summary>
        private static void AlignBodyYawToTarget(Transform playerTr, Transform target)
        {
            Vector3 up = playerTr.up;
            Vector3 aimPoint = GetBestAimPoint(playerTr, target);
            Vector3 toTarget = aimPoint - playerTr.position;
            // Remove vertical component to get horizontal direction only
            Vector3 horizDir = toTarget - Vector3.Project(toTarget, up);
            if (horizDir.sqrMagnitude < 0.01f) return;

            Quaternion targetRot = Quaternion.LookRotation(horizDir.normalized, up);
            playerTr.rotation = targetRot;

            DebugLogger.Log(LogCategory.State, "AutoWalk",
                "Body yaw aligned toward " + target.name);
        }

        /// <summary>
        /// Returns the best world-space point to aim at for a target.
        /// Prefers the closest point on the nearest collider, then
        /// collider bounds center, then 1m above root.
        /// </summary>
        private static Vector3 GetBestAimPoint(Transform playerTr, Transform target)
        {
            Vector3 up = playerTr.up;
            var playerCam = Locator.GetPlayerCamera();
            Vector3 origin = playerCam != null
                ? playerCam.transform.position
                : playerTr.position;

            // Try to find the closest collider on the target or its children
            var col = target.GetComponent<Collider>();
            if (col == null)
                col = target.GetComponentInChildren<Collider>();

            if (col != null)
            {
                // ClosestPoint gives the nearest surface point to the camera —
                // this is where the raycast needs to hit.
                Vector3 closest = col.ClosestPoint(origin);
                // Sanity: ClosestPoint returns the input point if inside
                // the collider. Fall back to bounds.center in that case.
                if (Vector3.Distance(closest, origin) < 0.1f)
                    return col.bounds.center;
                return closest;
            }

            // Fallback: aim ~1m above target root
            return target.position + up * 1.0f;
        }

        private static float ComputePitchToTarget(Transform playerTr, Transform target)
        {
            var camCtrl = Locator.GetPlayerCameraController();
            if (camCtrl == null) return 0f;

            var playerCam = Locator.GetPlayerCamera();
            Vector3 origin = playerCam != null
                ? playerCam.transform.position
                : playerTr.position;

            Vector3 up = playerTr.up;
            Vector3 aimPoint = GetBestAimPoint(playerTr, target);

            Vector3 toAim = aimPoint - origin;
            Vector3 horizDir = toAim - Vector3.Project(toAim, up);
            if (horizDir.sqrMagnitude < 0.01f) return 0f;

            // Negate: SignedAngle positive = below horizon, game positive = look UP
            float pitch = -Vector3.SignedAngle(horizDir.normalized, toAim.normalized,
                Vector3.Cross(up, horizDir.normalized));

            return Mathf.Clamp(pitch, -80f, 80f);
        }

        /// <summary>
        /// Aligns the camera pitch toward the target's collider center.
        /// Called once at arrival as initial aim before the sweep takes over.
        /// </summary>
        private static void AlignCameraPitchToTarget(Transform playerTr, Transform target)
        {
            try
            {
                var camCtrl = Locator.GetPlayerCameraController();
                if (camCtrl == null) return;

                float pitch = ComputePitchToTarget(playerTr, target);
                camCtrl.SetDegreesY(pitch);
                DebugLogger.Log(LogCategory.State, "AutoWalk",
                    $"Camera pitch aligned: {pitch:F1}°");
            }
            catch (System.Exception ex)
            {
                DebugLogger.LogState("[AutoWalk] AlignCameraPitch error: " + ex.Message);
            }
        }

        private static string GetHazardName(HazardDetector detector)
        {
            if (detector.InHazardType(HazardVolume.HazardType.DARKMATTER))
                return Loc.Get("hazard_darkmatter");
            if (detector.InHazardType(HazardVolume.HazardType.FIRE))
                return Loc.Get("hazard_fire");
            if (detector.InHazardType(HazardVolume.HazardType.HEAT))
                return Loc.Get("hazard_heat");
            if (detector.InHazardType(HazardVolume.HazardType.ELECTRICITY))
                return Loc.Get("hazard_electricity");
            if (detector.InHazardType(HazardVolume.HazardType.SANDFALL))
                return Loc.Get("hazard_sandfall");
            return Loc.Get("hazard_generic");
        }

        private void OnFluidEntered(FluidVolume.Type fluidType)
        {
            if (!_isActive) return;

            switch (fluidType)
            {
                case FluidVolume.Type.WATER:
                    // Smart water: dangerous without suit (camera submerged or undertow)
                    if (!PlayerState.IsWearingSuit()
                        && (PlayerState.IsCameraUnderwater() || PlayerState.InUndertowVolume()))
                    {
                        ScreenReader.Say(Loc.Get("auto_walk_hazard", Loc.Get("fluid_deep_water")), SpeechPriority.Now);
                        StopWalk(announce: false);
                    }
                    else
                    {
                        // Shallow water — keep walking
                        _inWater = true;
                        ScreenReader.Say(Loc.Get("auto_walk_wading"));
                    }
                    return;

                case FluidVolume.Type.SAND:
                case FluidVolume.Type.PLASMA:
                case FluidVolume.Type.GEYSER:
                    ScreenReader.Say(Loc.Get("auto_walk_hazard", GetFluidName(fluidType)), SpeechPriority.Now);
                    StopWalk(announce: false);
                    return;

                case FluidVolume.Type.TRACTOR_BEAM:
                    ScreenReader.Say(Loc.Get("fluid_tractor"));
                    return; // don't stop

                default:
                    return; // AIR, CLOUD, FOG — ignore
            }
        }

        private void OnFluidExited(FluidVolume.Type fluidType)
        {
            if (fluidType == FluidVolume.Type.WATER)
                _inWater = false;
        }

        private static string GetFluidName(FluidVolume.Type type)
        {
            switch (type)
            {
                case FluidVolume.Type.WATER:        return Loc.Get("fluid_water");
                case FluidVolume.Type.SAND:         return Loc.Get("fluid_sand");
                case FluidVolume.Type.PLASMA:       return Loc.Get("fluid_plasma");
                case FluidVolume.Type.GEYSER:       return Loc.Get("fluid_geyser");
                case FluidVolume.Type.TRACTOR_BEAM: return Loc.Get("fluid_tractor");
                default:                            return null;
            }
        }

        private void OnHazardUpdated()
        {
            if (!_isActive || _hazardDetector == null) return;
            // With suit underwater, ignore water-related damage
            if (PlayerState.IsWearingSuit() && PlayerState.IsCameraUnderwater()) return;
            if (_hazardDetector.GetNetDamagePerSecond() > 0f)
            {
                ScreenReader.Say(Loc.Get("auto_walk_hazard", GetHazardName(_hazardDetector)), SpeechPriority.Now);
                StopWalk(announce: false);
            }
        }

        /// <summary>
        /// Fired by InputManager when the active InputMode changes (menu open, ship computer, etc.).
        /// Phase 1.6 session 29: stop the auto-walk cleanly so the player doesn't resume into a
        /// desynced state when they return to Character mode.
        /// </summary>
        private void OnInputModeChanged()
        {
            if (!_isActive) return;
            if (!OWInput.IsInputMode(InputMode.Character))
            {
                DebugLogger.Log(LogCategory.State, "AutoWalk",
                    "InputMode left Character → StopWalk (now " + OWInput.GetInputMode() + ")");
                StopWalk(announce: true);
            }
        }

        #endregion
    }
}
