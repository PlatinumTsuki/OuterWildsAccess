using UnityEngine;

namespace OuterWildsAccess
{
    /// <summary>Reason for an auto-jump recommendation.</summary>
    public enum JumpReason
    {
        None,
        StepUp,
        Gap
    }

    /// <summary>Result of a single steering evaluation.</summary>
    public struct SteerResult
    {
        /// <summary>Recommended world-space movement direction (normalized, ground plane).</summary>
        public Vector3 SteerDirection;

        /// <summary>True if the system recommends jumping this frame.</summary>
        public bool ShouldJump;

        /// <summary>Reason for the jump recommendation.</summary>
        public JumpReason JumpReason;

        /// <summary>True if all evaluated directions are blocked.</summary>
        public bool IsBlocked;

        /// <summary>True if a cliff was detected ahead in the chosen direction.</summary>
        public bool CliffAhead;

        /// <summary>True if a hazard volume influenced steering.</summary>
        public bool DangerDetected;
    }

    /// <summary>
    /// Computes smart navigation directions using fan raycasting.
    /// Evaluates obstacle clearance, ground safety, slope, and danger
    /// to recommend the best movement direction toward a target.
    /// Pure computation — no MonoBehaviour, no Harmony, no announcements.
    /// </summary>
    public class SteeringController
    {
        #region Constants

        private const int   RayCount         = 15;
        private const float FanHalfAngle     = 90f;     // degrees each side — full 180° coverage
        private const float RayRange         = 5f;      // metres — obstacle/danger scan
        private const float ChestHeight      = 0.9f;    // metres above player origin
        private const float KneeHeight       = 0.3f;    // metres above player origin
        private const float GroundProbeUp    = 0.5f;    // raise origin above bumps
        private const float GroundProbeDown  = 4f;      // downward ray length
        private const float GroundProbeAhead = 2f;      // how far ahead to probe ground
        private const float CloseObstacle    = 0.8f;    // penalty only when very close
        private const float BlockedThreshold = 0f;      // more tolerant — only block if truly nothing
        private const float MaxWalkableSlope = 45f;     // degrees — game limit

        // Scoring weights — target alignment reduced so steering prefers safe paths
        private const float WTarget = 2f;
        private const float WClear  = 3f;
        private const float WGround = 5f;
        private const float WSlope  = 1.5f;
        private const float WDanger = -8f;

        // Jump detection
        private const float JumpLowHeight   = 0.3f;    // low raycast height
        private const float JumpHighHeight   = 1.0f;    // high raycast height
        private const float JumpCheckDist    = 1.8f;    // forward range for obstacle check
        private const float MaxJumpableGap   = 3.5f;    // max gap the player can jump

        // Cliff probe distances
        private const float CliffProbe1 = 1f;
        private const float CliffProbe2 = 2f;
        private const float CliffProbe3 = 3f;

        #endregion

        #region State

        private readonly float[] _scores = new float[RayCount];

        #endregion

        #region Public API

        /// <summary>Resets internal state. Call when auto-walk starts.</summary>
        public void Reset()
        {
        }

        /// <summary>
        /// Evaluates the best walking direction from playerPos toward targetPos.
        /// All vectors in world space; gravity is inferred from playerUp.
        /// </summary>
        /// <param name="playerPos">Player world position (feet).</param>
        /// <param name="playerUp">Player local up (opposite gravity).</param>
        /// <param name="targetPos">Target world position.</param>
        /// <param name="jumpOnCooldown">True if jump is on cooldown — skip jump detection.</param>
        public SteerResult Evaluate(Vector3 playerPos, Vector3 playerUp,
                                    Vector3 targetPos, bool jumpOnCooldown)
        {
            var result = new SteerResult();

            // Flat direction toward target (projected onto ground plane)
            Vector3 toTarget = targetPos - playerPos;
            Vector3 toTargetFlat = toTarget - Vector3.Project(toTarget, playerUp);

            if (toTargetFlat.sqrMagnitude < 0.001f)
            {
                // Directly above/below — can't steer meaningfully
                result.SteerDirection = Vector3.zero;
                result.IsBlocked = true;
                return result;
            }

            toTargetFlat.Normalize();

            // Angle step between rays
            float angleStep = (FanHalfAngle * 2f) / (RayCount - 1);

            int   bestIndex = -1;
            float bestScore = float.NegativeInfinity;
            bool  anyDanger = false;

            for (int i = 0; i < RayCount; i++)
            {
                float angle = -FanHalfAngle + i * angleStep;
                Vector3 dir = Quaternion.AngleAxis(angle, playerUp) * toTargetFlat;

                _scores[i] = ScoreDirection(playerPos, playerUp, dir, toTargetFlat,
                                            out bool danger);
                if (danger) anyDanger = true;

                if (_scores[i] > bestScore)
                {
                    bestScore = _scores[i];
                    bestIndex = i;
                }
            }

            result.DangerDetected = anyDanger;

            if (bestScore < BlockedThreshold || bestIndex < 0)
            {
                result.IsBlocked = true;
                result.SteerDirection = Vector3.zero;
                return result;
            }

            // Reconstruct best direction — no smoothing, instant response
            float bestAngle = -FanHalfAngle + bestIndex * angleStep;
            Vector3 bestDir = Quaternion.AngleAxis(bestAngle, playerUp) * toTargetFlat;

            result.SteerDirection = bestDir.normalized;

            // Cliff detection in chosen direction
            result.CliffAhead = CheckCliffAhead(playerPos, playerUp, result.SteerDirection);

            // Jump detection in chosen direction
            if (!jumpOnCooldown && _jumpEnabled)
            {
                result.ShouldJump = CheckJump(playerPos, playerUp, result.SteerDirection,
                                              out JumpReason reason);
                result.JumpReason = reason;
            }

            return result;
        }

        #endregion

        #region Jump enable/disable

        private bool _jumpEnabled = true;

        /// <summary>Enables or disables jump detection within steering.</summary>
        public void SetJumpEnabled(bool enabled) { _jumpEnabled = enabled; }

        #endregion

        #region Scoring

        /// <summary>
        /// Scores a single direction on obstacle clearance, ground, slope, danger, and target alignment.
        /// </summary>
        private float ScoreDirection(Vector3 origin, Vector3 up, Vector3 dir,
                                     Vector3 toTargetFlat, out bool dangerDetected)
        {
            dangerDetected = false;

            // --- Target alignment (0..1) ---
            float dot = Vector3.Dot(dir, toTargetFlat);
            float targetScore = (dot + 1f) * 0.5f;

            // --- Obstacle clearance (0..1) ---
            Vector3 chestOrigin = origin + up * ChestHeight;
            Vector3 kneeOrigin  = origin + up * KneeHeight;

            bool chestHit = Physics.Raycast(chestOrigin, dir, out RaycastHit hitChest,
                RayRange, OWLayerMask.physicalMask, QueryTriggerInteraction.Ignore);
            bool kneeHit  = Physics.Raycast(kneeOrigin, dir, out RaycastHit hitKnee,
                RayRange, OWLayerMask.physicalMask, QueryTriggerInteraction.Ignore);

            float minDist = RayRange;
            if (chestHit) minDist = Mathf.Min(minDist, hitChest.distance);
            if (kneeHit)  minDist = Mathf.Min(minDist, hitKnee.distance);

            float clearScore = minDist / RayRange;
            // Only penalize when very close — keeps tight corridors navigable
            if (minDist < CloseObstacle) clearScore *= 0.3f;

            // --- Ground safety (0 or 1) ---
            Vector3 probeOrigin = origin + dir * GroundProbeAhead + up * GroundProbeUp;
            bool hasGround = Physics.Raycast(probeOrigin, -up, out RaycastHit groundHit,
                GroundProbeDown, OWLayerMask.physicalMask, QueryTriggerInteraction.Ignore);
            float groundScore = hasGround ? 1f : 0f;

            // --- Slope safety (0..1) ---
            float slopeScore = 0f;
            if (hasGround)
            {
                float slopeAngle = Vector3.Angle(up, groundHit.normal);
                if (slopeAngle <= MaxWalkableSlope)
                    slopeScore = 1f;
                else
                    slopeScore = Mathf.Clamp01(1f - (slopeAngle - MaxWalkableSlope) / 30f);
            }

            // --- Danger detection (0 or 1) ---
            float dangerScore = 0f;
            bool dangerHit = Physics.Raycast(chestOrigin, dir, out RaycastHit dangerInfo,
                RayRange, OWLayerMask.effectVolumeMask, QueryTriggerInteraction.Collide);
            if (dangerHit)
            {
                var hazard = dangerInfo.collider.GetComponent<HazardVolume>();
                if (hazard != null)
                {
                    dangerScore    = 1f;
                    dangerDetected = true;
                }
            }

            // --- Composite score ---
            return WTarget * targetScore
                 + WClear  * clearScore
                 + WGround * groundScore
                 + WSlope  * slopeScore
                 + WDanger * dangerScore;
        }

        #endregion

        #region Cliff detection

        /// <summary>
        /// Checks for cliffs/gaps at 1m, 2m, 3m ahead in the given direction.
        /// Returns true if a cliff is detected at 1m (imminent danger).
        /// </summary>
        private bool CheckCliffAhead(Vector3 origin, Vector3 up, Vector3 dir)
        {
            Vector3 probe1 = origin + dir * CliffProbe1 + up * GroundProbeUp;
            bool ground1 = Physics.Raycast(probe1, -up, GroundProbeDown,
                OWLayerMask.physicalMask, QueryTriggerInteraction.Ignore);

            return !ground1; // no ground at 1m = cliff
        }

        #endregion

        #region Jump detection

        /// <summary>
        /// Checks if a jump is appropriate in the given direction.
        /// Detects low obstacles (step-up) and small gaps.
        /// </summary>
        private bool CheckJump(Vector3 origin, Vector3 up, Vector3 dir,
                               out JumpReason reason)
        {
            reason = JumpReason.None;

            // --- Step-up: low obstacle with clear space above ---
            Vector3 lowOrigin  = origin + up * JumpLowHeight;
            Vector3 highOrigin = origin + up * JumpHighHeight;

            bool lowHit = Physics.Raycast(lowOrigin, dir, out RaycastHit lowInfo,
                JumpCheckDist, OWLayerMask.physicalMask, QueryTriggerInteraction.Ignore);

            if (lowHit)
            {
                bool highHit = Physics.Raycast(highOrigin, dir, lowInfo.distance + 0.5f,
                    OWLayerMask.physicalMask, QueryTriggerInteraction.Ignore);

                if (!highHit)
                {
                    // Low obstacle, clear above — check landing zone
                    Vector3 beyondOrigin = origin + dir * (lowInfo.distance + 1f) + up * 1.5f;
                    bool landing = Physics.Raycast(beyondOrigin, -up, 3f,
                        OWLayerMask.physicalMask, QueryTriggerInteraction.Ignore);

                    if (landing)
                    {
                        reason = JumpReason.StepUp;
                        return true;
                    }
                }
            }

            // --- Gap: ground at 1m, missing at 2m, present at 3m ---
            Vector3 probe1 = origin + dir * CliffProbe1 + up * GroundProbeUp;
            Vector3 probe2 = origin + dir * CliffProbe2 + up * GroundProbeUp;
            Vector3 probe3 = origin + dir * CliffProbe3 + up * GroundProbeUp;

            bool ground1 = Physics.Raycast(probe1, -up, GroundProbeDown,
                OWLayerMask.physicalMask, QueryTriggerInteraction.Ignore);
            bool ground2 = Physics.Raycast(probe2, -up, GroundProbeDown,
                OWLayerMask.physicalMask, QueryTriggerInteraction.Ignore);
            bool ground3 = Physics.Raycast(probe3, -up, GroundProbeDown,
                OWLayerMask.physicalMask, QueryTriggerInteraction.Ignore);

            if (ground1 && !ground2 && ground3)
            {
                reason = JumpReason.Gap;
                return true;
            }

            return false;
        }

        #endregion
    }
}
