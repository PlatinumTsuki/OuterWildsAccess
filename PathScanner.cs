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

        private const float CellSize       = 1f;    // metres per grid cell
        private const float ProbeHeight    = 12f;   // cast from this far above reference height
        private const float ProbeLength    = 24f;   // max downward ray length
        private const float MaxSlope       = 45f;   // degrees — game limit
        private const float WallCheckH     = 0.9f;  // chest-height wall check
        private const float JumpClearH     = 1.2f;  // above this = not jumpable
        private const float MaxStepHeight  = 3f;    // max vertical gap between adjacent cells
        private const float HeightBand     = 3f;    // height bucket granularity (metres)
        private const float GoalHeightTol  = 4f;    // accept goal within this vertical tolerance
        private const int   MaxExplored    = 4000;  // A* cell budget

        #endregion

        #region Grid coordinate system

        private Vector3 _origin;
        private Vector3 _right;
        private Vector3 _forward;
        private Vector3 _up;
        private float   _targetHeight;  // height of target along _up relative to _origin

        #endregion

        #region Cell cache

        private struct CellInfo
        {
            public bool    Walkable;
            public Vector3 GroundPos;
            public float   Height;     // signed distance from _origin along _up
            public bool    HasHazard;
        }

        // Cell cache keyed by (x, z, heightBucket) — supports multi-layer terrain
        private readonly Dictionary<(int, int, int), CellInfo> _cells =
            new Dictionary<(int, int, int), CellInfo>();

        #endregion

        #region Result cache

        // Avoids recomputing when multiple handlers query the same path within a short window
        private const float CacheMaxAge  = 0.4f;  // seconds — valid for less than one guidance rescan
        private const float CachePosTol  = 1f;    // metres — recompute if player/target moved more

        private float               _cacheTime;
        private Vector3             _cachePlayerPos;
        private Vector3             _cacheTargetPos;
        private List<PathWaypoint>  _cacheResult;

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
            // Return cached result if still fresh and positions haven't changed much
            float age = Time.time - _cacheTime;
            if (_cacheResult != null && age < CacheMaxAge
                && (playerPos - _cachePlayerPos).sqrMagnitude < CachePosTol * CachePosTol
                && (targetPos - _cacheTargetPos).sqrMagnitude < CachePosTol * CachePosTol)
            {
                return _cacheResult;
            }

            _cells.Clear();

            // Build local coordinate system — forward points toward target
            _up = playerUp.normalized;
            Vector3 toTarget = targetPos - playerPos;
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
            _origin  = playerPos;

            // Target height and grid coordinates
            _targetHeight = Vector3.Dot(targetPos - _origin, _up);
            WorldToGrid(targetPos, out int tx, out int tz);

            // Scan the start cell to get its ground height
            // startH must match the ScanCell cache key (based on refHeight=0, not actual height)
            CellInfo startCell = ScanCell(0, 0, 0f);
            int startH = 0; // RoundToInt(0 / HeightBand) — matches cache key
            var startKey = (0, 0, startH);

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

            // 4-connected neighbors (avoids diagonal wall-clipping issues)
            int[] dx = { 1, -1, 0, 0 };
            int[] dz = { 0, 0, 1, -1 };

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
                    return CacheAndReturn(playerPos, targetPos,
                        BuildPath(parent, parentJump, ck));
                }

                // Expand 4-connected neighbors
                for (int i = 0; i < 4; i++)
                {
                    int nx = cur.x + dx[i];
                    int nz = cur.z + dz[i];

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
                    float cost = CellSize;
                    if (jump) cost += 1.5f;      // prefer non-jump paths
                    if (ni.HasHazard) cost += 50f; // heavily penalize hazards

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
            if (bestClosestDist < startHeuristic - 1f)
                return CacheAndReturn(playerPos, targetPos,
                    BuildPath(parent, parentJump, bestClosest));

            return CacheAndReturn(playerPos, targetPos, null);
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
            return result;
        }

        #endregion

        #region Grid helpers

        /// <summary>3D Euclidean heuristic including vertical distance.</summary>
        private float Heuristic(int x1, int z1, float h1, int x2, int z2, float h2)
        {
            float ddx = (x2 - x1) * CellSize;
            float ddz = (z2 - z1) * CellSize;
            float ddy = h2 - h1;
            return Mathf.Sqrt(ddx * ddx + ddz * ddz + ddy * ddy);
        }

        private void WorldToGrid(Vector3 pos, out int x, out int z)
        {
            Vector3 d = pos - _origin;
            x = Mathf.RoundToInt(Vector3.Dot(d, _right) / CellSize);
            z = Mathf.RoundToInt(Vector3.Dot(d, _forward) / CellSize);
        }

        /// <summary>Returns the horizontal world position of a grid cell (on the grid plane).</summary>
        private Vector3 GridToWorld(int x, int z)
        {
            return _origin + _right * (x * CellSize) + _forward * (z * CellSize);
        }

        #endregion

        #region Cell scanning

        /// <summary>
        /// Lazily scans a grid cell using height-relative probing.
        /// The probe starts from refHeight + ProbeHeight above the grid plane horizontal
        /// position, allowing the scanner to follow terrain as it climbs or descends.
        /// </summary>
        private CellInfo ScanCell(int x, int z, float refHeight)
        {
            int hBucket = Mathf.RoundToInt(refHeight / HeightBand);
            var key = (x, z, hBucket);
            if (_cells.TryGetValue(key, out CellInfo c)) return c;

            c = new CellInfo();

            // Horizontal position from grid, vertical from reference height
            Vector3 horizPos = GridToWorld(x, z);
            Vector3 probe    = horizPos + _up * (refHeight + ProbeHeight);

            if (Physics.Raycast(probe, -_up, out RaycastHit hit, ProbeLength,
                OWLayerMask.physicalMask, QueryTriggerInteraction.Ignore))
            {
                c.Walkable  = Vector3.Angle(_up, hit.normal) <= MaxSlope;
                c.GroundPos = hit.point;
                c.Height    = Vector3.Dot(hit.point - _origin, _up);

                // Hazard check via overlap sphere at ground level
                var cols = Physics.OverlapSphere(hit.point + _up * 0.5f, 0.4f,
                    OWLayerMask.effectVolumeMask, QueryTriggerInteraction.Collide);
                if (cols != null)
                {
                    for (int i = 0; i < cols.Length; i++)
                    {
                        if (cols[i].GetComponent<HazardVolume>() != null)
                        {
                            c.HasHazard = true;
                            break;
                        }
                    }
                }
            }
            // else: no ground → Walkable stays false

            _cells[key] = c;
            return c;
        }

        #endregion

        #region Edge checking

        /// <summary>
        /// Checks passability between two adjacent cells whose CellInfos are known.
        /// Returns 0 = passable, 1 = impassable wall, 2 = jumpable.
        /// </summary>
        private byte CheckEdge(CellInfo a, CellInfo b)
        {
            if (!a.Walkable || !b.Walkable) return 1;

            // Large height difference = impassable
            Vector3 diff = b.GroundPos - a.GroundPos;
            float vert = Mathf.Abs(Vector3.Dot(diff, _up));
            if (vert > MaxStepHeight) return 1;

            // Chest-height wall check
            Vector3 fromPt = a.GroundPos + _up * WallCheckH;
            Vector3 toPt   = b.GroundPos + _up * WallCheckH;
            Vector3 dir    = toPt - fromPt;
            float   dist   = dir.magnitude;
            if (dist < 0.01f) return 0;

            bool wallHit = Physics.Raycast(fromPt, dir / dist, dist,
                OWLayerMask.physicalMask, QueryTriggerInteraction.Ignore);

            if (!wallHit) return 0; // clear passage

            // Wall exists — is it jumpable? (clear above jump height)
            Vector3 fromHigh = a.GroundPos + _up * JumpClearH;
            Vector3 toHigh   = b.GroundPos + _up * JumpClearH;
            Vector3 dirHigh  = toHigh - fromHigh;
            float   distHigh = dirHigh.magnitude;

            bool highHit = Physics.Raycast(fromHigh, dirHigh / distHigh, distHigh,
                OWLayerMask.physicalMask, QueryTriggerInteraction.Ignore);

            return highHit ? (byte)1 : (byte)2;
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
        /// Removes unnecessary intermediate waypoints using line-of-sight checks.
        /// Preserves jump waypoints.
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
                    // Don't skip jump waypoints
                    bool anyJump = false;
                    for (int j = current + 1; j <= i; j++)
                    {
                        if (raw[j].NeedsJump) { anyJump = true; break; }
                    }
                    if (anyJump) break;

                    // Line-of-sight check at chest height
                    Vector3 from = raw[current].Position + _up * WallCheckH;
                    Vector3 to   = raw[i].Position + _up * WallCheckH;
                    Vector3 dir  = to - from;
                    float   dist = dir.magnitude;

                    if (dist > 0.01f && !Physics.Raycast(from, dir / dist, dist,
                        OWLayerMask.physicalMask, QueryTriggerInteraction.Ignore))
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

        #endregion
    }
}
