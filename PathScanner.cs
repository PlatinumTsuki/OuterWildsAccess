using System.Collections.Generic;
using UnityEngine;

namespace OuterWildsAccess
{
    /// <summary>A waypoint along a computed path.</summary>
    public struct PathWaypoint
    {
        /// <summary>World position on the ground surface.</summary>
        public Vector3 Position;

        /// <summary>True if the player needs to jump to reach this waypoint.</summary>
        public bool NeedsJump;
    }

    /// <summary>
    /// Scans the environment in a grid pattern and uses A* pathfinding
    /// to compute a walkable path from the player to a target.
    ///
    /// Height-propagating A*: each cell is probed relative to its parent
    /// cell's ground height, naturally following terrain up slopes and stairs.
    /// The cell cache uses 3D keys (x, z, heightBucket) to support multiple
    /// elevation levels at the same horizontal position.
    /// </summary>
    public class PathScanner
    {
        #region Constants

        private const float DefaultCellSize = 1f;   // metres per grid cell (close range)
        private const float LargeCellSize   = 2f;   // metres per grid cell (long range)
        private const float LargeCellThreshold = 40f; // use large cells above this distance
        // Probe geometry — tight enough to avoid catching ground from far away
        // (which makes edge cells inconsistent at the sub-metre level), but
        // tall enough to detect upward steps up to MaxStepHeight (3m) plus
        // a small margin for safety. Length covers MaxStepHeight in both
        // directions so downward stairs are still detected.
        private const float ProbeHeight    = 4f;    // cast from this far above reference height
        private const float ProbeLength    = 8f;    // max downward ray length
        private const float ProbeRadius    = 0.46f; // SphereCast radius (matches player capsule)
        private const float MaxSlope       = 45f;   // degrees — game's _maxAngleToBeGrounded
        private const float MaxAngleBetweenSlopes = 115f; // game's _maxAngleBetweenSlopes — multi-slope curved terrain acceptance
        private const float WallCheckH     = 0.9f;  // chest-height wall check
        // Session 29+ Phase B: raised from 0.5m to 1.0m to let CheckEdge accept
        // taller drops/steps as jumpable. The 0.5m value was too conservative for
        // terrain like Mica where realistic ledges are around 0.7-1.2m and the game
        // accepts them with a natural jump prompt. Stays well below MaxStepHeight=3m
        // so we don't accidentally allow vertically impossible jumps.
        private const float JumpClearH     = 1.0f;  // ledge clearance threshold
        private const float MaxStepHeight  = 3f;    // max vertical gap between adjacent cells
        private const float HeightBand     = 3f;    // height bucket granularity (metres)
        private const float GoalHeightTol  = 4f;    // accept goal within this vertical tolerance
        private const int   MaxExplored    = 12000; // A* cell budget (tripled for complex terrain)
        private const float DiagCost       = 1.414f; // diagonal movement cost multiplier
        // Session 32: continuous ground sampling step for CheckEdge.
        // The game checks every ~0.1m at 60fps/6m/s. We use 0.2m for a good
        // balance between accuracy and raycast budget (~5 samples per 1m cell).
        private const float EdgeSampleStep  = 0.2f;

        #endregion

        #region Grid coordinate system

        private Vector3 _origin;
        private Vector3 _right;
        private Vector3 _forward;
        private Vector3 _up;
        private float   _targetHeight;  // height of target along _up relative to _origin
        private float   _cellSize = DefaultCellSize;  // current cell size (adaptive)

        #endregion

        #region Cell cache

        private struct CellInfo
        {
            public bool    Walkable;
            public Vector3 GroundPos;
            public float   Height;        // signed distance from _origin along _up
            public float   SlopeAngle;    // degrees from _up — 0° = flat, 45° = max single-slope
            public float   HazardPenalty; // 0 = safe; added to A* move cost. >= BlockingHazardCost → cell forced non-walkable at scan time.
        }

        // Hazard cost taxonomy (Phase 1.4 — session 29).
        // Based on decompiled HazardVolume.HazardType enum and game behaviour:
        //  - DARKMATTER: instant death on entry (session 29 agent 2 research).
        //  - ELECTRICITY: 10 m/s kickback repeating every 1s → chain of knockbacks, practically intraversable.
        //  - HEAT/FIRE: continuous damage, traversable briefly if no alternative.
        //  - SANDFALL: dangerous but can have safe overhangs (SandfallHazardVolume.IsObjectExposed gating).
        //  - RAPIDS: applies torque, no direct damage.
        //  - GENERAL: default baseline (preserves previous +50 behaviour).
        // SAND fluid (FluidVolume.Type.SAND in SandfallFluidVolume) is penalised separately — the sand
        // funnel flow velocity can carry the player away even when the cell is technically walkable.
        private const float BlockingHazardCost = 1000f;
        private const float HazardCostGeneral  = 50f;
        private const float HazardCostRapids   = 30f;
        private const float HazardCostHeat     = 150f;
        private const float HazardCostFire     = 150f;
        private const float HazardCostSandfall = 150f;
        private const float FluidSandPenalty   = 100f;

        // Cell cache keyed by (x, z, heightBucket) — supports multi-layer terrain
        private readonly Dictionary<(int, int, int), CellInfo> _cells =
            new Dictionary<(int, int, int), CellInfo>();

        #endregion

        #region Player exclusion (session 29 — BUG #1 fix)

        // Lazy-initialised cache of the player's Rigidbody, used to filter out the player's
        // own capsule colliders from our SphereCasts. Without this filter, the start cell
        // probe always hits the player's own collider (because the probe column is centred
        // on the player) and records the player's body as "ground", which then cascades
        // into 100% edge rejection in CheckEdge. This reproduces the pattern used by the
        // game itself in PlayerCharacterController.IsValidGroundedHit (decompiled line 950).
        private Rigidbody _playerRigidbody;
        private readonly RaycastHit[] _castHitBuffer = new RaycastHit[16];
        private readonly Vector3[] _projectedNormals = new Vector3[16]; // multi-slope scratch buffer

        private Rigidbody GetPlayerRigidbody()
        {
            if (_playerRigidbody == null)
            {
                var body = Locator.GetPlayerBody();
                if (body != null) _playerRigidbody = body.GetRigidbody();
            }
            return _playerRigidbody;
        }

        /// <summary>
        /// SphereCast variant that ignores any hit belonging to the player's own rigidbody.
        /// Returns the nearest non-player hit in <paramref name="hit"/>, or false if none.
        /// </summary>
        private bool SphereCastExcludingPlayer(Vector3 origin, float radius, Vector3 direction,
            out RaycastHit hit, float maxDistance, int layerMask, QueryTriggerInteraction qti)
        {
            var playerRb = GetPlayerRigidbody();
            int count = Physics.SphereCastNonAlloc(origin, radius, direction,
                _castHitBuffer, maxDistance, layerMask, qti);

            hit = default;
            bool found = false;
            float bestDist = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = _castHitBuffer[i];
                if (h.collider == null) continue;

                // Primary filter: rigidbody identity (matches game pattern in
                // PlayerCharacterController.IsValidGroundedHit).
                if (playerRb != null && h.rigidbody == playerRb) continue;

                // Secondary filter: defensive check for child colliders that might not
                // report the rigidbody correctly in edge cases.
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

        #endregion

        #region Reliability telemetry (session 29 — always-on via DebugLogger.LogAutoWalk)

        // Per-FindPath counters, reset at the start of each call.
        private int _statCellsFreshScanned;
        private int _statCellsWalkable;
        private int _statCellsRejectedNoGround;
        private int _statCellsRejectedOffAxis;
        private int _statCellsRejectedSlope;
        private int _statCellsBlockedByHazard;   // blocking hazard (dark matter, electricity)
        private int _statCellsWithHazardPenalty; // walkable but carries a cost penalty
        private int _statEdgesImpassable;        // CheckEdge returned 1
        private int _statEdgesJumpable;          // CheckEdge returned 2

        // Edge rejection taxonomy (session 29 — deep diagnostic on short paths).
        private int _statEdgeRejectNonWalk;     // either cell not walkable
        private int _statEdgeRejectHeight;      // vert > MaxStepHeight
        private int _statEdgeRejectWallAndJump; // wall at chest AND wall at jump height
        private int _statEdgeRejectCliff;       // game cliff-prevention (forward-down raycast)
        private int _statEdgeClear;             // no wall, directly passable

        // Deferred log buffers — only dumped to the log when _statCellsFreshScanned < 30
        // (i.e. short-range failing calls like the Ardoise spawn case). Long paths would
        // flood the file. Buffers are cleared at ResetStats.
        private readonly List<string> _scanCellLogBuffer = new List<string>(32);
        private readonly List<string> _edgeRejectLogBuffer = new List<string>(32);
        private const int ScanCellLogBudget   = 30;  // max entries buffered per FindPath
        private const int EdgeRejectLogBudget = 40;  // max entries buffered per FindPath
        private const int DetailedDumpMaxScans = 30; // only dump detail on short-range calls

        private void ResetStats()
        {
            _statCellsFreshScanned      = 0;
            _statCellsWalkable          = 0;
            _statCellsRejectedNoGround  = 0;
            _statCellsRejectedOffAxis   = 0;
            _statCellsRejectedSlope     = 0;
            _statCellsBlockedByHazard   = 0;
            _statCellsWithHazardPenalty = 0;
            _statEdgesImpassable        = 0;
            _statEdgesJumpable          = 0;
            _statEdgeRejectNonWalk      = 0;
            _statEdgeRejectHeight       = 0;
            _statEdgeRejectWallAndJump  = 0;
            _statEdgeRejectCliff        = 0;
            _statEdgeClear              = 0;
            _scanCellLogBuffer.Clear();
            _edgeRejectLogBuffer.Clear();
        }

        #endregion

        #region Result cache

        // Avoids recomputing when multiple handlers query the same path within a short window
        private const float CacheMaxAge  = 0.4f;  // seconds — valid for less than one guidance rescan
        private const float CachePosTol  = 0.25f; // metres — recompute if player/target moved more

        private float               _cacheTime;
        private Vector3             _cachePlayerPos;
        private Vector3             _cacheTargetPos;
        private List<PathWaypoint>  _cacheResult;
        private float               _cacheCellSize;

        #endregion

        #region Public API

        /// <summary>
        /// Scans the environment and computes a path from playerPos to targetPos.
        /// Returns a simplified list of waypoints, or null if no path is found.
        /// Results are cached briefly so multiple handlers sharing this instance
        /// avoid redundant A* computations for the same target.
        /// </summary>
        public List<PathWaypoint> FindPath(Vector3 playerPos, Vector3 playerUp, Vector3 targetPos)
        {
            // Adaptive cell size — large cells for long distances, small for close range
            Vector3 toTargetRaw = targetPos - playerPos;
            Vector3 flatRaw = toTargetRaw - Vector3.Project(toTargetRaw, playerUp.normalized);
            float horizDistRaw = flatRaw.magnitude;
            // Hysteresis: switch up at 45m, switch down at 35m
            if (horizDistRaw > 45f)
                _cellSize = LargeCellSize;
            else if (horizDistRaw < 35f)
                _cellSize = DefaultCellSize;
            // else keep current _cellSize

            // Return cached result if still fresh and positions haven't changed much
            float age = Time.time - _cacheTime;
            if (_cacheResult != null && age < CacheMaxAge
                && _cacheCellSize == _cellSize
                && (playerPos - _cachePlayerPos).sqrMagnitude < CachePosTol * CachePosTol
                && (targetPos - _cacheTargetPos).sqrMagnitude < CachePosTol * CachePosTol)
            {
                DebugLogger.LogAutoWalk("FindPath CACHE_HIT dist=" + horizDistRaw.ToString("F1") +
                    "m waypoints=" + (_cacheResult == null ? "null" : _cacheResult.Count.ToString()));
                return _cacheResult;
            }

            DebugLogger.LogAutoWalk("FindPath BEGIN dist=" + horizDistRaw.ToString("F1") +
                "m cellSize=" + _cellSize.ToString("F1"));
            ResetStats();
            _cells.Clear();

            _up = playerUp.normalized;

            // Session 29+ ROOT-CAUSE FIX: snap grid origin to a stable point in the
            // ground body's local frame. The previous behaviour `_origin = playerPos`
            // re-anchored the entire grid to the player's exact world position on every
            // FindPath call, so a 0.27m sub-cell offset between two attempts shifted
            // every cell boundary by 0.27m. Cells classified as walkable in attempt A
            // could be reclassified as wall/slope/noground in attempt B, and A* would
            // pick a completely different route (observed: 152° flips between Mica
            // walks with identical starting conditions on 2026-04-11).
            //
            // The fix: round the player's position into the ground body's rotating
            // local frame (which co-rotates with the planet so it stays stable as the
            // planet moves through space) on a `_cellSize` grain, then convert back
            // to world. Two starting positions within the same physical cell snap to
            // the same `_origin`, so the grid axes, cell centres, and edge classifica-
            // tions are all bit-identical → A* gives the same path → fully reproducible.
            //
            // This affects ALL walks, not just Mica. Any terrain with two competing
            // routes of similar cost (cliffs, ledges, branching corridors) becomes
            // deterministic instead of position-sensitive.
            Vector3 snappedOrigin = playerPos;
            var snapGroundBody = Locator.GetPlayerController() != null
                ? Locator.GetPlayerController().GetGroundBody() : null;
            if (snapGroundBody != null)
            {
                Transform gbT = snapGroundBody.transform;
                Vector3 localPos = gbT.InverseTransformPoint(playerPos);
                float snap = _cellSize;
                Vector3 snappedLocal = new Vector3(
                    Mathf.Round(localPos.x / snap) * snap,
                    Mathf.Round(localPos.y / snap) * snap,
                    Mathf.Round(localPos.z / snap) * snap);
                snappedOrigin = gbT.TransformPoint(snappedLocal);
            }
            _origin = snappedOrigin;

            // Build local coordinate system — forward points toward target.
            // Derived from snappedOrigin (not playerPos) so that two close starting
            // positions produce the same _forward / _right basis. This is the second
            // half of the determinism fix: snapping just the origin without snapping
            // the basis would still cause slight angular variance in the grid axes.
            Vector3 toTarget = targetPos - _origin;
            Vector3 flat = toTarget - Vector3.Project(toTarget, _up);

            if (flat.sqrMagnitude < 0.1f)
            {
                // Target directly above/below — pick an arbitrary horizontal direction
                flat = Vector3.Cross(_up, Vector3.right);
                if (flat.sqrMagnitude < 0.01f)
                    flat = Vector3.Cross(_up, Vector3.forward);
                flat = flat.normalized;
            }
            else
            {
                flat = flat.normalized;
            }

            _forward = flat;
            _right   = Vector3.Cross(_up, _forward).normalized;
            _forward = Vector3.Cross(_right, _up).normalized; // ensure orthogonal

            // Target height and grid coordinates
            _targetHeight = Vector3.Dot(targetPos - _origin, _up);
            WorldToGrid(targetPos, out int tx, out int tz);

            // Scan the start cell to get its ground height
            // startH must match the ScanCell cache key (based on refHeight=0, not actual height)
            CellInfo startCell = ScanCell(0, 0, 0f);
            int startH = 0; // RoundToInt(0 / HeightBand) — matches cache key
            var startKey = (0, 0, startH);

            // Session 29+ trap-cell fix: force the start cell to be walkable.
            // The player IS physically standing at this position (the game's
            // PlayerCharacterController has accepted it as grounded), so it must be
            // reachable by definition. Without this override, a player who lands on a
            // slope slightly above MaxSlope (45°) after a jump arc gets a non-walkable
            // start cell → CheckEdge rejects ALL edges from it as "nonwalk" → A*
            // returns FAIL_NO_PATH → FALLBACK UNSAFE → auto-walk stops, even though
            // the player is surrounded by perfectly walkable terrain.
            // Observed on Mica: 3+ sessions ended in FAIL_NO_PATH explored=1 nonwalk=3
            // from the exact same pattern.
            if (!startCell.Walkable)
            {
                startCell.Walkable  = true;
                // Always use the player's actual position — ScanCell's probe may have
                // found a ground point on the steep slope surface which is geometrically
                // offset from where the player physically stands. Using that offset
                // GroundPos causes CheckEdge's chest-height SphereCast to start inside
                // the slope → "wall" hit → all edges rejected. playerPos is the
                // authoritative ground position (the game's PlayerCharacterController
                // has accepted it).
                startCell.GroundPos = playerPos;
                startCell.Height    = Vector3.Dot(playerPos - _origin, _up);
                _cells[startKey]    = startCell;
            }

            // A* with lazy cell evaluation — 3D state (x, z, heightBucket)
            var open       = new List<(float f, int x, int z, int h)>(256);
            var gScore     = new Dictionary<(int, int, int), float>();
            var parent     = new Dictionary<(int, int, int), (int, int, int)>();
            var parentJump = new Dictionary<(int, int, int), bool>();
            var closed     = new HashSet<(int, int, int)>();

            float startHeuristic = Heuristic(0, 0, startCell.Height, tx, tz, _targetHeight);
            gScore[startKey] = 0f;
            open.Add((startHeuristic, 0, 0, startH));

            // Track closest cell to goal (for partial paths when goal unreachable)
            (int, int, int) bestClosest     = startKey;
            float           bestClosestDist = startHeuristic;
            int             explored        = 0;

            // 8-connected neighbors: 0-3 cardinal, 4-7 diagonal
            int[] dx = { 1, -1, 0, 0, 1, -1, 1, -1 };
            int[] dz = { 0, 0, 1, -1, 1, 1, -1, -1 };

            while (open.Count > 0 && explored < MaxExplored)
            {
                // Pop node with lowest f-score
                int bi = 0;
                for (int i = 1; i < open.Count; i++)
                    if (open[i].f < open[bi].f) bi = i;
                var cur = open[bi];
                open.RemoveAt(bi);

                var ck = (cur.x, cur.z, cur.h);
                if (closed.Contains(ck)) continue;
                closed.Add(ck);
                explored++;

                // Retrieve current cell info (guaranteed in cache)
                CellInfo curCell = _cells[ck];

                // Track closest to goal (3D heuristic)
                float distG = Heuristic(cur.x, cur.z, curCell.Height, tx, tz, _targetHeight);
                if (distG < bestClosestDist)
                {
                    bestClosestDist = distG;
                    bestClosest     = ck;
                }

                // Goal reached? Check (x,z) match AND height within tolerance
                if (cur.x == tx && cur.z == tz &&
                    Mathf.Abs(curCell.Height - _targetHeight) < GoalHeightTol)
                {
                    var built = BuildPath(parent, parentJump, ck);
                    LogFindPathStats("EXACT", explored, built != null ? built.Count : 0);
                    return CacheAndReturn(playerPos, targetPos, built);
                }

                // Expand 8-connected neighbors
                for (int i = 0; i < 8; i++)
                {
                    int nx = cur.x + dx[i];
                    int nz = cur.z + dz[i];
                    bool isDiag = i >= 4;

                    // Diagonal wall-clip prevention: both adjacent cardinal cells must be walkable
                    if (isDiag)
                    {
                        CellInfo adj1 = ScanCell(nx, cur.z, curCell.Height);
                        CellInfo adj2 = ScanCell(cur.x, nz, curCell.Height);
                        if (!adj1.Walkable || !adj2.Walkable) continue;
                    }

                    // Scan neighbor using current cell's ground height as reference
                    CellInfo ni = ScanCell(nx, nz, curCell.Height);
                    if (!ni.Walkable) continue;

                    // nh must match ScanCell cache key (based on refHeight=curCell.Height)
                    int nh = Mathf.RoundToInt(curCell.Height / HeightBand);
                    var nk = (nx, nz, nh);
                    if (closed.Contains(nk)) continue;

                    // Inline edge check
                    byte edge = CheckEdge(curCell, ni);
                    if (edge == 1) continue; // impassable wall

                    bool  jump = (edge == 2);
                    float cost = isDiag ? _cellSize * DiagCost : _cellSize;
                    if (jump) cost += 1.5f;      // prefer non-jump paths
                    if (ni.HazardPenalty > 0f) cost += ni.HazardPenalty; // typed hazard penalty (session 29)

                    float g = gScore[ck] + cost;
                    if (!gScore.ContainsKey(nk) || g < gScore[nk])
                    {
                        gScore[nk]     = g;
                        parent[nk]     = ck;
                        parentJump[nk] = jump;
                        float h = Heuristic(nx, nz, ni.Height, tx, tz, _targetHeight);
                        open.Add((g + h, nx, nz, nh));
                    }
                }
            }

            // No exact path — use closest reachable cell
            bool budgetExhausted = explored >= MaxExplored;
            if (bestClosestDist < startHeuristic - 1f)
            {
                var built = BuildPath(parent, parentJump, bestClosest);
                LogFindPathStats(budgetExhausted ? "PARTIAL_BUDGET" : "PARTIAL",
                    explored, built != null ? built.Count : 0);
                return CacheAndReturn(playerPos, targetPos, built);
            }

            LogFindPathStats(budgetExhausted ? "FAIL_BUDGET" : "FAIL_NO_PATH", explored, 0);
            return CacheAndReturn(playerPos, targetPos, null);
        }

        /// <summary>
        /// Writes a one-line summary of the A* call to the auto-walk telemetry log.
        /// Also dumps the detailed scan/edge reject buffers when the call scanned few cells
        /// (short-range failing cases like the Ardoise spawn), so we can see exactly why
        /// A* rejected every edge.
        /// </summary>
        private void LogFindPathStats(string verdict, int explored, int waypoints)
        {
            DebugLogger.LogAutoWalk(
                "FindPath END verdict=" + verdict +
                " explored=" + explored +
                " waypoints=" + waypoints +
                " cells:scan=" + _statCellsFreshScanned +
                " walk=" + _statCellsWalkable +
                " noground=" + _statCellsRejectedNoGround +
                " offaxis=" + _statCellsRejectedOffAxis +
                " slope=" + _statCellsRejectedSlope +
                " hazblock=" + _statCellsBlockedByHazard +
                " hazpen=" + _statCellsWithHazardPenalty +
                " edges:impass=" + _statEdgesImpassable +
                " (nonwalk=" + _statEdgeRejectNonWalk +
                " height=" + _statEdgeRejectHeight +
                " wall+jump=" + _statEdgeRejectWallAndJump +
                " cliff=" + _statEdgeRejectCliff + ")" +
                " clear=" + _statEdgeClear +
                " jump=" + _statEdgesJumpable);

            // Dump detailed buffers only on short-range failing calls — the long-path
            // case would flood the log with thousands of lines.
            if (_statCellsFreshScanned <= DetailedDumpMaxScans)
            {
                if (_scanCellLogBuffer.Count > 0)
                {
                    DebugLogger.LogAutoWalk("DETAIL cells (" + _scanCellLogBuffer.Count + "):");
                    for (int i = 0; i < _scanCellLogBuffer.Count; i++)
                        DebugLogger.LogAutoWalk(_scanCellLogBuffer[i]);
                }
                if (_edgeRejectLogBuffer.Count > 0)
                {
                    DebugLogger.LogAutoWalk("DETAIL edges (" + _edgeRejectLogBuffer.Count + "):");
                    for (int i = 0; i < _edgeRejectLogBuffer.Count; i++)
                        DebugLogger.LogAutoWalk(_edgeRejectLogBuffer[i]);
                }
            }
        }

        #endregion

        #region Cache helpers

        /// <summary>Stores result in cache and returns it.</summary>
        private List<PathWaypoint> CacheAndReturn(Vector3 playerPos, Vector3 targetPos, List<PathWaypoint> result)
        {
            _cacheTime      = Time.time;
            _cachePlayerPos = playerPos;
            _cacheTargetPos = targetPos;
            _cacheResult    = result;
            _cacheCellSize  = _cellSize;
            return result;
        }

        #endregion

        #region Grid helpers

        /// <summary>3D Euclidean heuristic including vertical distance.</summary>
        private float Heuristic(int x1, int z1, float h1, int x2, int z2, float h2)
        {
            float ddx = (x2 - x1) * _cellSize;
            float ddz = (z2 - z1) * _cellSize;
            float ddy = h2 - h1;
            return Mathf.Sqrt(ddx * ddx + ddz * ddz + ddy * ddy);
        }

        private void WorldToGrid(Vector3 pos, out int x, out int z)
        {
            Vector3 d = pos - _origin;
            x = Mathf.RoundToInt(Vector3.Dot(d, _right) / _cellSize);
            z = Mathf.RoundToInt(Vector3.Dot(d, _forward) / _cellSize);
        }

        /// <summary>Returns the horizontal world position of a grid cell (on the grid plane).</summary>
        private Vector3 GridToWorld(int x, int z)
        {
            return _origin + _right * (x * _cellSize) + _forward * (z * _cellSize);
        }

        #endregion

        #region Cell scanning

        /// <summary>
        /// Lazily scans a grid cell using height-relative probing with multi-slope
        /// acceptance matching the game's CastForGrounded logic.
        ///
        /// The game (PlayerCharacterController.cs lines 847-898) accepts ground in
        /// three steps:
        ///   1. Single normal ≤ 45° → grounded.
        ///   2. If no single normal passes, check ALL SphereCast contacts: if any
        ///      individual contact has normal ≤ 45° → grounded (best of multiple hits).
        ///   3. If still not grounded, project all contact normals onto the horizontal
        ///      plane and compare PAIRS: if any pair's angle > 115° → grounded.
        ///      This is the "multi-slope" rule that lets the player walk on curved
        ///      terrain (hills, pipes) where individual surface normals exceed 45°.
        ///
        /// Previously our ScanCell used a single SphereCast hit and a single slope
        /// check. This caused ~800-1400 false "slope" rejections per FindPath on
        /// Mica's hill terrain, making A* unable to find direct routes. This was the
        /// ROOT CAUSE of virtually all the Mica walk failures investigated over
        /// sessions 29+.
        /// </summary>
        private CellInfo ScanCell(int x, int z, float refHeight)
        {
            int hBucket = Mathf.RoundToInt(refHeight / HeightBand);
            var key = (x, z, hBucket);
            if (_cells.TryGetValue(key, out CellInfo c)) return c;

            _statCellsFreshScanned++;
            c = new CellInfo();

            Vector3 horizPos = GridToWorld(x, z);
            Vector3 probe    = horizPos + _up * (refHeight + ProbeHeight);

            // Cast for ALL contacts (not just closest) — needed for multi-slope.
            var playerRb = GetPlayerRigidbody();
            int hitCount = Physics.SphereCastNonAlloc(probe, ProbeRadius, -_up,
                _castHitBuffer, ProbeLength - ProbeRadius,
                OWLayerMask.physicalMask, QueryTriggerInteraction.Ignore);

            // ── Filter: find closest valid on-axis non-player hit ─────────────
            RaycastHit bestHit = default;
            bool   foundAny   = false;
            float  bestDist   = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit h = _castHitBuffer[i];
                if (h.collider == null) continue;
                if (playerRb != null && h.rigidbody == playerRb) continue;
                if (h.collider.GetComponentInParent<PlayerCharacterController>() != null) continue;

                // Off-axis filter
                Vector3 hitOff   = h.point - horizPos;
                float   horizOff = (hitOff - _up * Vector3.Dot(hitOff, _up)).magnitude;
                if (horizOff > ProbeRadius + 0.1f) continue;

                if (h.distance < bestDist)
                {
                    bestDist = h.distance;
                    bestHit  = h;
                    foundAny = true;
                }
            }

            if (!foundAny)
            {
                _statCellsRejectedNoGround++;
                BufferScanCellLog(x, z, horizPos, "NOGROUND probeStart=" + probe.ToString("F2") +
                    " probeLen=" + (ProbeLength - ProbeRadius).ToString("F1") + "m");
                _cells[key] = c;
                return c;
            }

            // ── Store ground position from closest hit regardless of slope ────
            c.GroundPos = bestHit.point;
            c.Height    = Vector3.Dot(bestHit.point - _origin, _up);

            // ── Step 1: single-normal check on closest hit ────────────────────
            float slopeAngle = Vector3.Angle(_up, bestHit.normal);
            if (slopeAngle <= MaxSlope)
            {
                c.Walkable    = true;
                c.SlopeAngle  = slopeAngle;
                BufferScanCellLog(x, z, horizPos, "WALK slope=" + slopeAngle.ToString("F0") +
                    "° hit=" + bestHit.point.ToString("F2") +
                    " collider=" + (bestHit.collider != null ? bestHit.collider.name : "null"));
            }
            else
            {
                // ── Step 2: check ALL valid hits for any single normal ≤ 45° ──
                bool accepted = false;
                for (int i = 0; i < hitCount && !accepted; i++)
                {
                    RaycastHit h = _castHitBuffer[i];
                    if (h.collider == null) continue;
                    if (playerRb != null && h.rigidbody == playerRb) continue;
                    if (h.collider.GetComponentInParent<PlayerCharacterController>() != null) continue;
                    Vector3 ho = h.point - horizPos;
                    if ((ho - _up * Vector3.Dot(ho, _up)).magnitude > ProbeRadius + 0.1f) continue;

                    float altSlope = Vector3.Angle(_up, h.normal);
                    if (altSlope <= MaxSlope)
                    {
                        c.GroundPos   = h.point;
                        c.Height      = Vector3.Dot(h.point - _origin, _up);
                        c.Walkable    = true;
                        c.SlopeAngle  = altSlope;
                        accepted      = true;
                        BufferScanCellLog(x, z, horizPos, "WALK_ALT slope=" +
                            Vector3.Angle(_up, h.normal).ToString("F0") + "° (secondary hit)");
                    }
                }

                // ── Step 3: multi-slope paired-normal check (game's 115° rule) ─
                if (!accepted)
                {
                    // Collect projected normals for all valid hits
                    int validN = 0;
                    for (int i = 0; i < hitCount && validN < 16; i++)
                    {
                        RaycastHit h = _castHitBuffer[i];
                        if (h.collider == null) continue;
                        if (playerRb != null && h.rigidbody == playerRb) continue;
                        if (h.collider.GetComponentInParent<PlayerCharacterController>() != null) continue;
                        Vector3 ho = h.point - horizPos;
                        if ((ho - _up * Vector3.Dot(ho, _up)).magnitude > ProbeRadius + 0.1f) continue;

                        _projectedNormals[validN] = Vector3.ProjectOnPlane(h.normal, _up);
                        validN++;
                    }

                    // Compare pairs: if any pair's projected-normal angle > 115° → walkable
                    for (int k = 0; k < validN && !accepted; k++)
                    {
                        for (int l = k + 1; l < validN; l++)
                        {
                            if (Vector3.Angle(_projectedNormals[k], _projectedNormals[l]) > MaxAngleBetweenSlopes)
                            {
                                c.Walkable   = true;
                                c.SlopeAngle = slopeAngle; // store the original closest-hit angle
                                accepted     = true;
                                BufferScanCellLog(x, z, horizPos, "WALK_MULTISLOPE angle=" +
                                    Vector3.Angle(_projectedNormals[k], _projectedNormals[l]).ToString("F0") +
                                    "° between " + validN + " normals");
                                break;
                            }
                        }
                    }

                    if (!accepted)
                    {
                        _statCellsRejectedSlope++;
                        BufferScanCellLog(x, z, horizPos, "SLOPE angle=" + slopeAngle.ToString("F0") +
                            "° hit=" + bestHit.point.ToString("F2") + " normal=" + bestHit.normal.ToString("F2") +
                            " collider=" + (bestHit.collider != null ? bestHit.collider.name : "null") +
                            " multiHits=" + validN);
                    }
                }
            }

            // ── Hazard + fluid penalty (unchanged) ────────────────────────────
            if (c.Walkable || c.GroundPos != Vector3.zero)
            {
                Vector3 hazardProbe = c.GroundPos + _up * 0.5f;
                var cols = Physics.OverlapSphere(hazardProbe, 0.4f,
                    OWLayerMask.effectVolumeMask, QueryTriggerInteraction.Collide);
                if (cols != null)
                {
                    for (int i = 0; i < cols.Length; i++)
                    {
                        var hv = cols[i].GetComponent<HazardVolume>();
                        if (hv != null)
                        {
                            float penalty = GetHazardPenalty(hv.GetHazardType());
                            if (penalty >= BlockingHazardCost)
                            {
                                _statCellsBlockedByHazard++;
                                c.Walkable = false;
                                c.HazardPenalty = penalty;
                                _cells[key] = c;
                                return c;
                            }
                            c.HazardPenalty += penalty;
                        }

                        var fv = cols[i].GetComponent<FluidVolume>();
                        if (fv != null && fv.GetFluidType() == FluidVolume.Type.SAND)
                        {
                            c.HazardPenalty += FluidSandPenalty;
                        }
                    }
                }
            }

            if (c.Walkable) _statCellsWalkable++;
            if (c.HazardPenalty > 0f) _statCellsWithHazardPenalty++;

            _cells[key] = c;
            return c;
        }

        /// <summary>
        /// Appends a ScanCell diagnostic line to the deferred buffer.
        /// Dumped in FindPath END only when the call scanned ≤ DetailedDumpMaxScans cells.
        /// </summary>
        private void BufferScanCellLog(int x, int z, Vector3 horizPos, string msg)
        {
            if (_scanCellLogBuffer.Count >= ScanCellLogBudget) return;
            _scanCellLogBuffer.Add("  scan(" + x + "," + z + ") pos=" + horizPos.ToString("F2") +
                " → " + msg);
        }

        /// <summary>
        /// Maps a HazardType to its A* move-cost penalty. Values >= BlockingHazardCost
        /// cause the scanning cell to be forced non-walkable (A* will never route through).
        /// See CellInfo.HazardPenalty and ScanCell for the integration.
        /// </summary>
        private static float GetHazardPenalty(HazardVolume.HazardType type)
        {
            switch (type)
            {
                case HazardVolume.HazardType.DARKMATTER:
                    return BlockingHazardCost;  // instant death on first contact
                case HazardVolume.HazardType.ELECTRICITY:
                    return BlockingHazardCost;  // 10 m/s kickback chained every 1s → intraversable
                case HazardVolume.HazardType.HEAT:
                    return HazardCostHeat;
                case HazardVolume.HazardType.FIRE:
                    return HazardCostFire;
                case HazardVolume.HazardType.SANDFALL:
                    return HazardCostSandfall;
                case HazardVolume.HazardType.RAPIDS:
                    return HazardCostRapids;
                case HazardVolume.HazardType.GENERAL:
                case HazardVolume.HazardType.NONE:
                default:
                    return HazardCostGeneral;
            }
        }

        #endregion

        #region Edge checking

        // Slope threshold above which we skip the wall SphereCast in CheckEdge.
        // On steep terrain, the chest-height cast (GroundPos + _up * 0.9m) often
        // ends up inside the slope geometry above the player, producing a false
        // "wall" hit. Real obstacles (buildings, fences, rocks) only exist on
        // relatively flat ground. 30° is a safe cutoff — terrain features above
        // 30° are natural slopes, not artificial walls.
        private const float SteepSlopeWallSkip = 30f;

        /// <summary>
        /// Checks passability between two adjacent cells whose CellInfos are known.
        /// Returns 0 = passable, 1 = impassable wall, 2 = jumpable.
        /// </summary>
        private byte CheckEdge(CellInfo a, CellInfo b)
        {
            if (!a.Walkable || !b.Walkable)
            {
                _statEdgesImpassable++;
                _statEdgeRejectNonWalk++;
                BufferEdgeRejectLog(a, b, "NONWALK a.w=" + a.Walkable + " b.w=" + b.Walkable);
                return 1;
            }

            // Large height difference = impassable
            Vector3 diff = b.GroundPos - a.GroundPos;
            float vert = Mathf.Abs(Vector3.Dot(diff, _up));
            if (vert > MaxStepHeight)
            {
                _statEdgesImpassable++;
                _statEdgeRejectHeight++;
                BufferEdgeRejectLog(a, b, "HEIGHT vert=" + vert.ToString("F2") +
                    "m > MaxStep=" + MaxStepHeight + "m");
                return 1;
            }

            // Session 32: unified ground sampling — replicate the game's per-frame
            // cliff check from PlayerCharacterController.UpdateMovement (lines 671-684)
            // with CONTINUOUS coverage along the entire edge.
            //
            // The game checks 0.1m ahead EVERY FRAME at 60fps (one check per ~0.1m of
            // travel at 6 m/s). We sample every EdgeSampleStep (0.2m) from 0.1m to
            // near the end of the edge, giving full coverage with no gaps.
            //
            // At each sample: raycast down from foot-level + 1m. If drop > 0.2m AND
            // (slope > 45° OR drop > 1.5m), the game blocks movement.
            //
            // True cliffs (drop > MaxStepHeight or no ground): return 1 (impassable).
            // Small drops (steps/ledges): flag for wall+jump check below.
            bool cliffDetectedSmall = false;
            {
                Vector3 edgeDir = b.GroundPos - a.GroundPos;
                Vector3 edgeHoriz = edgeDir - Vector3.Project(edgeDir, _up);
                float edgeHorizLen = edgeHoriz.magnitude;
                if (edgeHorizLen > 0.01f)
                {
                    Vector3 edgeHorizN = edgeHoriz / edgeHorizLen;

                    // Interpolate foot height along edge for accurate drop measurement
                    float aFootH = Vector3.Dot(a.GroundPos, _up);
                    float bFootH = Vector3.Dot(b.GroundPos, _up);

                    for (float d = 0.1f; d < edgeHorizLen; d += EdgeSampleStep)
                    {
                        // Foot height at this sample via linear interpolation a→b
                        float t = d / edgeHorizLen;
                        float footH = Mathf.Lerp(aFootH, bFootH, t);
                        Vector3 footPos = a.GroundPos + edgeHorizN * d
                            + _up * (footH - aFootH);
                        Vector3 probeOrigin = footPos + _up * 1f;

                        if (Physics.Raycast(probeOrigin, -_up, out RaycastHit cliffHit, 20f,
                            OWLayerMask.groundMask, QueryTriggerInteraction.Ignore))
                        {
                            float dropBelowFeet = cliffHit.distance - 1f;
                            float cliffSlope = Vector3.Angle(_up, cliffHit.normal);
                            if (dropBelowFeet > 0.2f && (cliffSlope > 45f || dropBelowFeet > 1.5f))
                            {
                                if (dropBelowFeet > MaxStepHeight)
                                {
                                    _statEdgesImpassable++;
                                    _statEdgeRejectCliff++;
                                    BufferEdgeRejectLog(a, b, "CLIFF drop=" +
                                        dropBelowFeet.ToString("F2") + "m slope=" +
                                        cliffSlope.ToString("F0") + "° @" +
                                        d.ToString("F2") + "m");
                                    return 1;
                                }
                                cliffDetectedSmall = true;
                                _statEdgeRejectCliff++;
                            }
                        }
                        else
                        {
                            _statEdgesImpassable++;
                            _statEdgeRejectCliff++;
                            BufferEdgeRejectLog(a, b, "CLIFF no_ground @" +
                                d.ToString("F2") + "m");
                            return 1;
                        }
                    }
                }
            }

            // On steep terrain, skip the wall check entirely. The chest-height
            // SphereCast uses GroundPos + _up * 0.9m which, on a steep slope,
            // ends up inside the terrain geometry above the player — producing
            // false "wall" hits that block all edges. Real artificial walls
            // (buildings, fences) only exist on flat ground where the cast is
            // reliable. On slopes > SteepSlopeWallSkip, the only "walls" are
            // the terrain surface itself, which is walkable by definition (both
            // cells passed the multi-slope ground check).
            // Skip wall check on steep slopes UNLESS a small cliff was detected —
            // in that case we must fall through to the wall+jump check to verify.
            if (!cliffDetectedSmall
                && (a.SlopeAngle > SteepSlopeWallSkip || b.SlopeAngle > SteepSlopeWallSkip))
            {
                _statEdgeClear++;
                return 0;
            }

            // Chest-height wall check (SphereCast for player width)
            Vector3 fromPt = a.GroundPos + _up * WallCheckH;
            Vector3 toPt   = b.GroundPos + _up * WallCheckH;
            Vector3 dir    = toPt - fromPt;
            float   dist   = dir.magnitude;
            if (dist < 0.01f) { _statEdgeClear++; return 0; }

            float castDist = dist - ProbeRadius;
            if (castDist < 0f) castDist = 0f;
            bool wallHit = SphereCastExcludingPlayer(fromPt, ProbeRadius, dir / dist, out RaycastHit wallHitInfo,
                castDist, OWLayerMask.physicalMask, QueryTriggerInteraction.Ignore);

            if (!wallHit) { _statEdgeClear++; return 0; } // clear passage

            // Wall exists — is it jumpable? (clear above jump height)
            Vector3 fromHigh = a.GroundPos + _up * JumpClearH;
            Vector3 toHigh   = b.GroundPos + _up * JumpClearH;
            Vector3 dirHigh  = toHigh - fromHigh;
            float   distHigh = dirHigh.magnitude;

            float castDistHigh = distHigh - ProbeRadius;
            if (castDistHigh < 0f) castDistHigh = 0f;
            bool highHit = SphereCastExcludingPlayer(fromHigh, ProbeRadius, dirHigh / distHigh, out RaycastHit highHitInfo,
                castDistHigh, OWLayerMask.physicalMask, QueryTriggerInteraction.Ignore);

            if (highHit)
            {
                _statEdgesImpassable++;
                _statEdgeRejectWallAndJump++;
                BufferEdgeRejectLog(a, b, "WALL+JUMP chest=" +
                    (wallHitInfo.collider != null ? wallHitInfo.collider.name : "?") +
                    " @" + wallHitInfo.point.ToString("F2") + " jumpClear=" +
                    (highHitInfo.collider != null ? highHitInfo.collider.name : "?") +
                    " @" + highHitInfo.point.ToString("F2"));
                return 1;
            }
            _statEdgesJumpable++;
            return 2;
        }

        /// <summary>
        /// Appends an edge-reject diagnostic line to the deferred buffer.
        /// Dumped in FindPath END only on short-range failing calls.
        /// </summary>
        private void BufferEdgeRejectLog(CellInfo a, CellInfo b, string reason)
        {
            if (_edgeRejectLogBuffer.Count >= EdgeRejectLogBudget) return;
            _edgeRejectLogBuffer.Add("  edge " + a.GroundPos.ToString("F2") + " → " +
                b.GroundPos.ToString("F2") + " REJECT " + reason);
        }

        #endregion

        #region Path reconstruction

        /// <summary>Reconstructs and simplifies the path from A* parent chain.</summary>
        private List<PathWaypoint> BuildPath(
            Dictionary<(int, int, int), (int, int, int)> parent,
            Dictionary<(int, int, int), bool> parentJump,
            (int, int, int) goalKey)
        {
            var raw = new List<PathWaypoint>();
            var k   = goalKey;

            while (parent.ContainsKey(k))
            {
                CellInfo ci = _cells[k]; // guaranteed in cache — visited by A*
                bool jump = parentJump.ContainsKey(k) && parentJump[k];
                raw.Add(new PathWaypoint { Position = ci.GroundPos, NeedsJump = jump });
                k = parent[k];
            }

            raw.Reverse(); // start → goal order

            if (raw.Count <= 2) return raw;
            return SimplifyPath(raw);
        }

        /// <summary>
        /// Theta*-style line-of-sight path smoothing (session 29+ Phase A).
        ///
        /// Greedy walk through the raw A* waypoints. For each anchor waypoint, find
        /// the furthest reachable downstream waypoint via a "walkable line of sight"
        /// check, and skip everything in between. Result: paths follow direct lines
        /// across cells instead of cardinal-grid zig-zags, producing smoother walks
        /// and shorter routes (typical of Theta* output, see Nash &amp; Koenig 2010).
        ///
        /// The line-of-sight check requires THREE conditions, not just the wall test
        /// the original SimplifyPath used:
        ///   1. No wall at chest height between the two endpoints (wall raycast).
        ///   2. Ground exists at every sample along the line (no cliff/void traversal).
        ///   3. The ground at each sample is within MaxStepHeight of the linear height
        ///      interpolation between the endpoints (no big steps mid-line).
        ///   4. Each sample's ground slope is &lt;= MaxSlope (no walking through a 60° wall).
        ///
        /// Without conditions 2-4 the original simplifier could skip waypoints that
        /// bridged a cliff at chest height, walking the player straight into the void.
        /// Jump waypoints are still preserved (cannot be skipped) — those are real
        /// actions, not grid noise.
        /// </summary>
        private List<PathWaypoint> SimplifyPath(List<PathWaypoint> raw)
        {
            var simple = new List<PathWaypoint>();
            simple.Add(raw[0]);

            int current = 0;
            while (current < raw.Count - 1)
            {
                int furthest = current + 1;
                for (int i = current + 2; i < raw.Count; i++)
                {
                    // Don't skip jump waypoints — they encode real jump actions.
                    bool anyJump = false;
                    for (int j = current + 1; j <= i; j++)
                    {
                        if (raw[j].NeedsJump) { anyJump = true; break; }
                    }
                    if (anyJump) break;

                    if (HasWalkableLineOfSight(raw[current].Position, raw[i].Position))
                    {
                        furthest = i;
                    }
                    else
                    {
                        break;
                    }
                }

                simple.Add(raw[furthest]);
                current = furthest;
            }

            return simple;
        }

        /// <summary>
        /// True if a player-width SphereCast can travel from <paramref name="from"/> to
        /// <paramref name="to"/> at chest height without hitting a wall, AND there is
        /// continuous walkable ground (slope &lt;= MaxSlope, height within MaxStepHeight
        /// of the linear interpolation) sampled at every ~0.5m along the line.
        ///
        /// Used by SimplifyPath to make safe Theta*-style line-of-sight skips. The
        /// ground sampling is what makes this safe over cliff edges — the original
        /// chest-height-only check would happily skip across a void.
        /// </summary>
        private bool HasWalkableLineOfSight(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float   dist  = delta.magnitude;
            if (dist < 0.01f) return true;

            // ── Wall check at chest height ────────────────────────────────────────
            Vector3 chestFrom = from + _up * WallCheckH;
            Vector3 chestTo   = to   + _up * WallCheckH;
            Vector3 chestDir  = chestTo - chestFrom;
            float   chestDist = chestDir.magnitude;
            if (chestDist > 0.01f)
            {
                float castDist = chestDist - ProbeRadius;
                if (castDist < 0f) castDist = 0f;
                if (SphereCastExcludingPlayer(chestFrom, ProbeRadius, chestDir / chestDist,
                    out _, castDist, OWLayerMask.physicalMask, QueryTriggerInteraction.Ignore))
                {
                    return false; // wall in the way
                }
            }

            // ── Ground continuity + slope check along the line ────────────────────
            // Sample every ~0.5m. Each sample probes downward from above to find
            // ground; reject the LoS if any sample finds no ground, ground too far
            // off the linear height interpolation, or a slope above MaxSlope.
            const float SampleSpacing = 0.5f;
            int samples = Mathf.Max(2, Mathf.CeilToInt(dist / SampleSpacing));
            float fromH = Vector3.Dot(from - _origin, _up);
            float toH   = Vector3.Dot(to   - _origin, _up);

            for (int s = 1; s < samples; s++) // skip endpoints (already validated by A*)
            {
                float t = (float)s / samples;
                Vector3 samplePoint = Vector3.Lerp(from, to, t);
                float   expectedH   = Mathf.Lerp(fromH, toH, t);

                Vector3 probeOrigin = samplePoint + _up * ProbeHeight;
                if (!SphereCastExcludingPlayer(probeOrigin, ProbeRadius, -_up,
                    out RaycastHit hit, ProbeLength - ProbeRadius,
                    OWLayerMask.physicalMask, QueryTriggerInteraction.Ignore))
                {
                    return false; // no ground at sample → cliff/void
                }

                float actualH = Vector3.Dot(hit.point - _origin, _up);
                if (Mathf.Abs(actualH - expectedH) > MaxStepHeight)
                {
                    return false; // ground too far above/below the linear line
                }

                if (Vector3.Angle(_up, hit.normal) > MaxSlope)
                {
                    return false; // sample is on a non-walkable slope
                }
            }

            return true;
        }



        #endregion
    }
}
