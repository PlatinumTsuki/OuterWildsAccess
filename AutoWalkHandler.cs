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
        private const float PostJumpRescanDelay = 1f;   // rescan path this long after a jump
        private const float JumpHorizBoost     = 4f;   // horizontal velocity added at jump (m/s)

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
        private const float SweepStart      = 30f;   // start looking slightly up
        private const float SweepEnd        = -60f;   // sweep down to -60°
        private const float SweepSpeed      = 60f;    // degrees per second

        // Periodic rescan
        private float _rescanTimer = 0f;

        // Movement tracking — stuck detection for rescan/stop
        private Vector3 _moveCheckPos        = Vector3.zero;
        private float   _moveCheckTime       = 0f;
        private bool    _isStuck             = false;
        private int     _stuckRescanCount    = 0;
        private int     _lastCheckWaypointIndex = 0;

        // Ground state — faster stuck/fall detection via game events
        private PlayerCharacterController _playerController;
        private bool  _wasGrounded      = true;
        private float _airborneTime     = 0f;
        private const float MaxAirborneBeforeStop = 3f;  // seconds airborne before auto-stop

        // Post-jump rescan
        private float _lastJumpTime = 0f;

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

            // Reset camera pitch
            var camCtrl = Locator.GetPlayerCameraController();
            if (camCtrl != null) camCtrl.SetDegreesY(0f);

            // Initial pitch toward target collider
            Transform playerTr = Locator.GetPlayerTransform();
            if (playerTr != null) AlignCameraPitchToTarget(playerTr, target);

            // Start sweep for interactable targets
            if (isInteractable)
            {
                _sweepingPitch = true;
                _sweepPitch    = SweepStart;
                if (camCtrl != null) camCtrl.SetDegreesY(_sweepPitch);
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
            _moveCheckPos        = Vector3.zero;
            _moveCheckTime       = Time.time;
            _isStuck             = false;
            _stuckRescanCount    = 0;
            _lastCheckWaypointIndex = 0;
            _lastJumpTime        = 0f;
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

            // Subscribe to ground events for faster stuck/fall detection
            _playerController = Locator.GetPlayerController();
            _wasGrounded  = _playerController != null && _playerController.IsGrounded();
            _airborneTime = 0f;

            // Reset camera pitch to neutral at start (prevents drift accumulation)
            var camStart = Locator.GetPlayerCameraController();
            if (camStart != null) camStart.SetDegreesY(0f);

            ScreenReader.Say(Loc.Get("auto_walk_on", _targetName));
            DebugLogger.LogInput("F9", "AutoWalk start → " + _targetName);
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

            // ── 3b. Water depth check — stop if submerged or in undertow ────
            if (_inWater && (PlayerState.IsCameraUnderwater() || PlayerState.InUndertowVolume()))
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

            if (horizDist <= PathConstants.ArrivalDist && vertDist > PathConstants.ArrivalVertMax && !_postArrivalAligning)
            {
                ScreenReader.Say(Loc.Get("auto_walk_out_of_reach", _targetName));
                StopWalk(announce: false);
                return;
            }

            if (horizDist <= PathConstants.ArrivalDist && !_postArrivalAligning)
            {
                _onArrival?.Invoke();

                // Initial pitch: aim at collider center
                AlignCameraPitchToTarget(playerTr, _target);


                _injectedAxis        = Vector2.zero;
                _postArrivalAligning = true;
                _alignEndTime        = Time.time + AlignTimeout;
                _pendingArrivalMsg   = Loc.Get("auto_walk_arrived", _targetName);

                // Start pitch sweep for interactable targets
                if (_targetIsInteractable)
                {
                    _sweepingPitch = true;
                    _sweepPitch    = SweepStart;
                    var camSweep = Locator.GetPlayerCameraController();
                    if (camSweep != null) camSweep.SetDegreesY(_sweepPitch);
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

                // Pitch sweep: scan from +30° to -60° looking for interactable focus
                if (_sweepingPitch)
                {
                    var camCtrl = Locator.GetPlayerCameraController();
                    if (camCtrl != null)
                    {
                        // Check if the game detected an interactable via raycast
                        var fpm = Locator.GetPlayerCamera()?.GetComponent<FirstPersonManipulator>();
                        if (fpm != null && fpm.HasFocusedInteractible())
                        {
                            // Found it! Stop sweep and announce
                            _sweepingPitch = false;
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
                if (Time.time >= _rescanTimer)
                {
                    _path = null;
                    _rescanTimer = Time.time + RescanInterval;
                }

                // ── 7. Post-jump rescan — after landing, recompute path ──────
                if (_lastJumpTime > 0f && Time.time - _lastJumpTime > PostJumpRescanDelay)
                {
                    _path = null;
                    _lastJumpTime = 0f;
                }

                // ── 8. Compute path if needed (with segmentation for long distances)
                if (_path == null)
                {
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
                        // Segment: compute A* toward intermediate goal at SegmentDistance
                        _segmentGoal = playerTr.position + horizVec.normalized * SegmentDistance;
                        _path = _pathScanner.FindPath(playerTr.position, up, _segmentGoal);
                        _segmented = true;
                    }
                    else
                    {
                        _path = _pathScanner.FindPath(playerTr.position, up, _target.position);
                        _segmented = false;
                    }
                    _waypointIndex = 0;

                    if (_path == null || _path.Count == 0)
                    {
                        // No path found — walk straight toward target as fallback
                        _path = new List<PathWaypoint>();
                        _path.Add(new PathWaypoint
                        {
                            Position  = _target.position,
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
                            _path = _pathScanner.FindPath(playerTr.position, up, _segmentGoal);
                            _segmented = true;
                        }
                        else
                        {
                            _path = _pathScanner.FindPath(playerTr.position, up, _target.position);
                            _segmented = false;
                        }

                        if (_path == null || _path.Count == 0)
                        {
                            _path = new List<PathWaypoint>();
                            _path.Add(new PathWaypoint
                            {
                                Position  = _target.position,
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
                            _path = _pathScanner.FindPath(playerTr.position, up, _target.position);
                        }

                        if (_path == null || _path.Count == 0)
                        {
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
                bool grounded = _playerController != null && _playerController.IsGrounded();

                // 10a. Airborne detection — stop if falling too long (not a planned jump)
                if (!grounded)
                {
                    _airborneTime += Time.deltaTime;
                    if (_airborneTime >= MaxAirborneBeforeStop && _lastJumpTime == 0f)
                    {
                        ScreenReader.Say(Loc.Get("auto_walk_stuck"), SpeechPriority.Now);
                        StopWalk(announce: false);
                        return;
                    }
                }
                else
                {
                    // Just landed — reset airborne timer and stuck counter
                    if (!_wasGrounded)
                    {
                        _stuckRescanCount = 0;
                        _path = null;  // rescan path after landing
                        DebugLogger.Log(LogCategory.State, "AutoWalk", "Landed — rescan path");
                    }
                    _airborneTime = 0f;

                    // 10b. Slope check via raycast — if standing on slope > 45°, rescan
                    if (Physics.Raycast(playerTr.position + up * 0.5f, -up, out RaycastHit slopeHit, 2f,
                        OWLayerMask.physicalMask, QueryTriggerInteraction.Ignore))
                    {
                        float slopeAngle = Vector3.Angle(up, slopeHit.normal);
                        if (slopeAngle > 45f)
                        {
                            _path = null;
                            DebugLogger.Log(LogCategory.State, "AutoWalk",
                                "Steep slope " + slopeAngle.ToString("F0") + "° — rescan");
                        }
                    }
                }
                _wasGrounded = grounded;

                // 10c. Movement distance check (every 0.5s) — stuck detection
                if (_moveCheckPos == Vector3.zero)
                    _moveCheckPos = playerTr.position;

                if (Time.time - _moveCheckTime >= MoveCheckInterval)
                {
                    float moved = Vector3.Distance(playerTr.position, _moveCheckPos);
                    // Dual criterion: barely moved AND still on same waypoint
                    _isStuck       = moved < MoveThreshold && _waypointIndex == _lastCheckWaypointIndex;
                    _moveCheckPos  = playerTr.position;
                    _moveCheckTime = Time.time;
                    _lastCheckWaypointIndex = _waypointIndex;

                    if (_isStuck)
                    {
                        _stuckRescanCount++;
                        DebugLogger.Log(LogCategory.State, "AutoWalk",
                            "Stuck — rescan " + _stuckRescanCount + "/" + MaxStuckRescans);

                        if (_stuckRescanCount >= MaxStuckRescans)
                        {
                            ScreenReader.Say(Loc.Get("auto_walk_stuck"), SpeechPriority.Now);
                            StopWalk(announce: false);
                            return;
                        }

                        // Force path rescan on next frame
                        _path = null;
                    }
                    else if (grounded)
                    {
                        // Moving freely on ground — reset stuck counter
                        _stuckRescanCount = 0;
                    }
                }

                // ── 11. A* jump waypoints (path-planned, grounded only) ──────
                if (wp.NeedsJump && _jumpCooldown <= 0f && _jumpEnabled)
                {
                    Vector3 toJumpWp    = wp.Position - playerTr.position;
                    Vector3 toJumpHoriz = toJumpWp - Vector3.Project(toJumpWp, up);
                    if (toJumpHoriz.magnitude <= JumpTriggerDist)
                    {
                        FireJumpWithBoost(playerTr, up, toJumpHoriz);
                    }
                }

                // ── 12. Game jump prompt — jump when the game says to ──────
                if (_jumpCooldown <= 0f && _jumpEnabled && CheckJumpPromptVisible())
                {
                    Vector3 toWpDir = wp.Position - playerTr.position;
                    Vector3 toWpH   = toWpDir - Vector3.Project(toWpDir, up);
                    FireJumpWithBoost(playerTr, up, toWpH);
                    DebugLogger.Log(LogCategory.State, "AutoWalk",
                        "Jump triggered by game prompt");
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

                if (facingDot < 0.5f)
                {
                    // Facing away — rotate only, don't walk into walls
                    _injectedAxis = Vector2.zero;
                }
                else
                {
                    float   axisX = Vector3.Dot(worldDir, playerTr.right);
                    float   axisY = Vector3.Dot(worldDir, playerTr.forward);
                    Vector2 axis  = new Vector2(axisX, axisY);
                    if (axis.magnitude > 1f) axis = axis.normalized;
                    _injectedAxis = axis;
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

        /// <summary>Fires a jump and adds horizontal boost toward the waypoint.</summary>
        private void FireJumpWithBoost(Transform playerTr, Vector3 up, Vector3 horizDir)
        {
            RequestJump();
            _jumpCooldown = 1.5f;
            _lastJumpTime = Time.time;

            var owBody = Locator.GetPlayerBody();
            if (owBody != null && horizDir.sqrMagnitude > 0.01f)
            {
                owBody.AddVelocityChange(horizDir.normalized * JumpHorizBoost);
                DebugLogger.Log(LogCategory.State, "AutoWalk",
                    "Jump horizontal boost applied");
            }
        }

        private void StopWalk(bool announce)
        {
            _isActive            = false;
            _injecting           = false;
            _injectedAxis        = Vector2.zero;
            _injectedLook        = Vector2.zero;
            _jumpCooldown        = 0f;
            _wantJumpFrames      = 0;
            _postArrivalAligning = false;
            _pendingArrivalMsg   = null;
            _sweepingPitch       = false;
            _path                = null;
            _waypointIndex       = 0;
            _lastJumpTime        = 0f;
            _stuckRescanCount    = 0;
            _lastCheckWaypointIndex = 0;
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
        /// Computes the best aim point for the target: collider center if available,
        /// otherwise 1m above root. Returns the pitch angle to aim the camera there.
        /// </summary>
        private static float ComputePitchToTarget(Transform playerTr, Transform target)
        {
            var camCtrl = Locator.GetPlayerCameraController();
            if (camCtrl == null) return 0f;

            var playerCam = Locator.GetPlayerCamera();
            Vector3 origin = playerCam != null
                ? playerCam.transform.position
                : playerTr.position;

            Vector3 up = playerTr.up;

            // Find the actual collider center for precise aiming
            Vector3 aimPoint;
            var col = target.GetComponent<Collider>();
            if (col != null)
            {
                aimPoint = col.bounds.center;
            }
            else
            {
                // Fallback: aim ~1m above target root
                aimPoint = target.position + up * 1.0f;
            }

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
                    // Smart water: check if dangerous (camera submerged or undertow)
                    if (PlayerState.IsCameraUnderwater() || PlayerState.InUndertowVolume())
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
            if (_hazardDetector.GetNetDamagePerSecond() > 0f)
            {
                ScreenReader.Say(Loc.Get("auto_walk_hazard", GetHazardName(_hazardDetector)), SpeechPriority.Now);
                StopWalk(announce: false);
            }
        }

        #endregion
    }
}
