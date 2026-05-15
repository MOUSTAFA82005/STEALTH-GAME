using System.Collections.Generic; // Needed for List<T>
using UnityEngine; // Unity framework

public class GuardAI : MonoBehaviour // Main guard AI controller
{
    private enum GuardState { Patrol, Suspicious, Chase, Search, Attack } // Guard FSM states

    [Header("Patrol")] // Patrol configuration section
    public Transform[] points; // Waypoint array for patrol
    public float patrolSpeed = 1.2f, chaseSpeed = 4f, searchSpeed = 2.5f, suspiciousSpeed = 1.5f; // Movement speeds per state
    public float pointStopDistance = 0.5f; // Distance to consider waypoint reached
    [Range(0f, 1f)] public float patrolLookChance = 0.4f; // Probability of looking around at patrol points

    [Header("Vision")] // Vision detection section
    public Transform player; // Reference to player transform
    public float visionRange = 12f; // Maximum vision distance
    [Range(0f, 360f)] public float visionAngle = 90f; // Main vision cone angle
    public float peripheralRange = 4f; // Peripheral vision range
    [Range(0f, 360f)] public float peripheralAngle = 200f; // Peripheral vision cone angle
    public float loseSightDelay = 0.5f; // Delay before losing sight of player

    [Header("Hearing")] // Hearing detection section
    public float hearingRange = 7f, hearingRunThreshold = 1.5f; // Hearing range and run velocity threshold

    [Header("Suspicious")] public float suspiciousInvestigateTime = 2.2f; // Duration to investigate suspicious sound

    [Header("Attack")] // Attack state section
    public float attackRange = 4f, attackExitRange = 9f, attackCooldown = 1.2f, attackStateMinTime = 1f; // Attack timing/ranges
    public int damagePerShot = 15; // Damage dealt per shot

    [Header("Search")] // Search state section
    public float waitAtSearchPoint = 1.5f; // Wait time at each search waypoint
    public int searchPointCount = 5; // Number of search waypoints to generate

    [Header("Alert")] public float alertRadius = 8f; // Radius for alerting nearby guards

    [Header("Obstacle Avoidance")] // Collision avoidance section
    public float avoidRadius = 0.35f, detectDistance = 1.2f, stuckTimeLimit = 0.4f, stuckMoveThreshold = 0.08f; // Avoidance tuning
    public int avoidRaySamples = 7; // Number of samples for avoidance raycast

    [Header("Pathfinding (A*)")] // A* algorithm section
    public float cellSize = 1f; // Grid cell size for pathfinding
    [Range(10, 60)] public int gridSize = 20; // Grid resolution for pathfinding

    [HideInInspector] public bool isDead; // Death flag

    // Components // Component cache
    private Rigidbody rb, playerRb; // Guard and player rigidbodies
    private Animator animator; // Animator for animations
    private PlayerHealth playerHealth; // Player health component
    private int obstacleLayerMask; // Mask for obstacle detection
    private GuardAI[] allGuards; // Array of all guards in scene

    // FSM + timers // Finite state machine state
    private GuardState state = GuardState.Patrol; // Current guard state
    private int patrolIndex, searchIndex, pathIndex; // Indices for patrol, search, and path
    private bool isPatrolLooking, attackAnimStarted, isWallFollowing; // Behavior flags
    private float patrolLookTimer, attackTimer, attackStateTimer, loseSightTimer; // Combat/sight timers
    private float waitTimer, suspiciousTimer, stuckTimer, stuckCheckTimer, repathTimer, wallFollowTimer; // Other timers
    private const float stuckCheckInterval = 0.3f, repathInterval = 0.8f, wallFollowDuration = 1.5f; // Constant intervals

    // Positions / paths // Stored positions and paths
    private Vector3 lastCheckedPosition, lastPosition, lastKnownPlayerPos, suspiciousPos, lockedWallDir, lastRepathPlayerPos; // World-space references
    private float computedSpeed; // Speed computed from position delta
    private List<Vector3> searchPath = new List<Vector3>(); // Search waypoints
    private List<Vector3> currentPath = new List<Vector3>(); // A* path waypoints

    // ── Pathfinding ──────────────────────────────────────────────────────

    private class PNode // Pathfinding node
    {
        public int x, z; // Grid coordinates
        public Vector3 pos; // World position
        public bool walkable; // Walkability flag
        public float g = float.MaxValue, h; // Cost from start, heuristic to goal
        public float F => g + h; // Total cost
        public PNode parent; // Parent node for backtracking
    }

    private class MinHeap // Min-heap for open set
    {
        private readonly List<PNode> data = new List<PNode>(); // Heap storage
        public int Count => data.Count; // Heap size
        public void Clear() => data.Clear(); // Clear heap

        public void Push(PNode node) // Add node to heap
        {
            data.Add(node); // Add to end
            for (int i = data.Count - 1, p; i > 0 && data[p = (i - 1) >> 1].F > data[i].F; i = p) // Bubble up
                (data[p], data[i]) = (data[i], data[p]); // Swap with parent
        }

        public PNode Pop() // Remove minimum
        {
            PNode top = data[0]; // Save top
            int last = data.Count - 1; // Last index
            data[0] = data[last]; // Move last to top
            data.RemoveAt(last); // Remove last slot
            for (int i = 0, n = data.Count; ;) // Sink down
            {
                int l = (i << 1) + 1, r = l + 1, s = i; // Child indices
                if (l < n && data[l].F < data[s].F) s = l; // Check left
                if (r < n && data[r].F < data[s].F) s = r; // Check right
                if (s == i) break; // Already ordered
                (data[s], data[i]) = (data[i], data[s]); // Swap
                i = s; // Move down
            }
            return top; // Return minimum
        }
    }

    // Pre-allocated neighbor buffer — avoids GC per frame // Reusable buffers
    private readonly List<PNode> neighborBuffer = new List<PNode>(8); // Reusable neighbors
    private readonly MinHeap openHeap = new MinHeap(); // A* open set

    PNode[,] BuildGrid(Vector3 center) // Build navigation grid
    {
        int G = gridSize; // Grid size
        float half = G * cellSize * 0.5f; // Half grid size
        Vector3 origin = new Vector3(center.x - half, transform.position.y, center.z - half); // Grid origin
        var grid = new PNode[G, G]; // Create grid
        for (int x = 0; x < G; x++) // For each x
            for (int z = 0; z < G; z++) // For each z
            {
                Vector3 wp = origin + new Vector3(x * cellSize, 0f, z * cellSize); // World position
                grid[x, z] = new PNode
                {
                    x = x,
                    z = z,
                    pos = wp, // Create node
                    walkable = !Physics.CheckSphere(wp + Vector3.up * 0.5f, avoidRadius + 0.3f, obstacleLayerMask)
                }; // Walkable check
            }
        return grid; // Return grid
    }

    PNode GetNode(PNode[,] grid, Vector3 gridCenter, Vector3 world) // Get node at world position
    {
        int G = gridSize; // Grid size
        float half = G * cellSize * 0.5f; // Half grid size
        int xi = Mathf.Clamp(Mathf.FloorToInt((world.x - (gridCenter.x - half)) / cellSize), 0, G - 1); // X index
        int zi = Mathf.Clamp(Mathf.FloorToInt((world.z - (gridCenter.z - half)) / cellSize), 0, G - 1); // Z index
        return grid[xi, zi]; // Return node
    }

    void FillNeighbors(PNode[,] grid, PNode node) // Fill neighbor buffer
    {
        neighborBuffer.Clear(); // Clear buffer
        int G = gridSize; // Grid size
        for (int dx = -1; dx <= 1; dx++) // For each neighbor x
            for (int dz = -1; dz <= 1; dz++) // For each neighbor z
            {
                if (dx == 0 && dz == 0) continue; // Skip self
                int nx = node.x + dx, nz = node.z + dz; // Neighbor coordinates
                if (nx >= 0 && nx < G && nz >= 0 && nz < G) // In bounds
                    neighborBuffer.Add(grid[nx, nz]); // Add neighbor
            }
    }

    List<Vector3> AStarPath(Vector3 start, Vector3 goal) // A* pathfinding
    {
        Vector3 center = (start + goal) * 0.5f; // Grid center
        PNode[,] grid = BuildGrid(center); // Build grid
        PNode startNode = GetNode(grid, center, start), goalNode = GetNode(grid, center, goal); // Endpoints
        startNode.walkable = goalNode.walkable = true; // Force traversability

        var closed = new HashSet<PNode>(); // Closed set
        openHeap.Clear(); // Reset open set
        startNode.g = 0f; // Start cost
        startNode.h = Vector3.Distance(startNode.pos, goalNode.pos); // Start heuristic
        openHeap.Push(startNode); // Seed open set

        while (openHeap.Count > 0) // While open not empty
        {
            PNode current = openHeap.Pop(); // Get best node
            if (closed.Contains(current)) continue; // Skip stale
            if (current == goalNode) return ReconstructPath(goalNode); // Found goal
            closed.Add(current); // Mark visited

            FillNeighbors(grid, current); // Get neighbors
            foreach (PNode nb in neighborBuffer) // For each neighbor
            {
                if (!nb.walkable || closed.Contains(nb)) continue; // Skip blocked/visited
                float tentG = current.g + Vector3.Distance(current.pos, nb.pos); // Tentative cost
                if (tentG < nb.g) // If better path found
                {
                    nb.g = tentG; // Update cost
                    nb.h = Vector3.Distance(nb.pos, goalNode.pos); // Update heuristic
                    nb.parent = current; // Set parent
                    openHeap.Push(nb); // Add to open
                }
            }
        }
        return new List<Vector3> { goal }; // Fallback: direct movement
    }

    List<Vector3> ReconstructPath(PNode end) // Reconstruct path from goal
    {
        var path = new List<Vector3>(); // Path list
        for (PNode n = end; n != null; n = n.parent) path.Add(n.pos); // Backtrack to start
        path.Reverse(); // Reverse to start-to-goal
        return SmoothPath(path); // Smooth path
    }

    List<Vector3> SmoothPath(List<Vector3> raw) // Smooth path by removing redundant waypoints
    {
        if (raw.Count <= 2) return raw; // Too short to smooth
        var smooth = new List<Vector3> { raw[0] }; // Start with first point
        int i = 0; // Current index
        while (i < raw.Count - 1) // While not at end
        {
            int j = raw.Count - 1; // Try furthest point first
            while (j > i + 1) // While not adjacent
            {
                Vector3 from = raw[i] + Vector3.up * 0.5f, to = raw[j] + Vector3.up * 0.5f; // Endpoints lifted
                Vector3 dir = to - from; // Direction
                if (!Physics.SphereCast(from, avoidRadius * 0.8f, dir.normalized, out _, dir.magnitude, obstacleLayerMask)) // Clear LOS
                    break; // Found clear point
                j--; // Try closer point
            }
            i = j; // Advance
            smooth.Add(raw[i]); // Add to smooth path
        }
        return smooth; // Return smoothed path
    }

    List<Vector3> BFSSearchWaypoints(Vector3 origin) // BFS to generate search waypoints
    {
        PNode[,] grid = BuildGrid(origin); // Build grid
        PNode start = GetNode(grid, origin, origin); // Start node
        start.walkable = true; // Force walkable

        var visited = new HashSet<PNode> { start }; // Visited set
        var queue = new Queue<PNode>(); // BFS queue
        var waypoints = new List<Vector3>(); // Waypoint list
        queue.Enqueue(start); // Enqueue start
        PNode prev = start; // Previous waypoint
        int step = 0; // Step counter

        while (queue.Count > 0 && waypoints.Count < searchPointCount) // While queue and waypoints
        {
            PNode cur = queue.Dequeue(); // Dequeue node
            if (++step % 3 == 0 && Vector3.Distance(cur.pos, prev.pos) > cellSize * 1.5f) // Sample every 3 steps if far enough
            {
                waypoints.Add(cur.pos); // Add waypoint
                prev = cur; // Update previous
            }
            FillNeighbors(grid, cur); // Get neighbors
            foreach (PNode nb in neighborBuffer) // For each neighbor
                if (nb.walkable && visited.Add(nb)) queue.Enqueue(nb); // Enqueue if walkable and unvisited
        }
        return waypoints.Count > 0 ? waypoints : new List<Vector3> { origin }; // Return waypoints or origin fallback
    }

    // ── Unity Lifecycle ──────────────────────────────────────────────────

    void Start() // Initialize
    {
        rb = GetComponent<Rigidbody>(); // Cache rigidbody
        animator = GetComponentInChildren<Animator>(); // Cache animator
        playerHealth = player ? player.GetComponent<PlayerHealth>() : null; // Cache player health
        playerRb = player ? player.GetComponent<Rigidbody>() : null; // Cache player rigidbody
        obstacleLayerMask = ~LayerMask.GetMask("Guard", "Player"); // Create obstacle mask
        allGuards = FindObjectsByType<GuardAI>(FindObjectsSortMode.None); // Find all guards
        lastCheckedPosition = lastPosition = transform.position; // Initialize positions
    }

    public void ReceiveAlert(Vector3 knownPos) // Receive alert from nearby guard
    {
        if (isDead || state == GuardState.Chase || state == GuardState.Attack) return; // Ignore if dead or already hunting
        lastKnownPlayerPos = knownPos; // Store alert position
        StartSearch(); // Begin search
    }

    void Update() => UpdateAnimator(); // Per-frame: update animations

    void FixedUpdate() // Physics update
    {
        if (isDead) return; // Skip if dead
        SeparateFromWalls(); // Separate from geometry
        UpdateVision(); // Update perception
        UpdateStuckDetection(); // Check for stuck state

        computedSpeed = Vector3.Distance(transform.position, lastPosition) / Time.fixedDeltaTime; // Calculate actual speed
        lastPosition = transform.position; // Update tracked position

        switch (state) // State machine dispatch
        {
            case GuardState.Patrol: Patrol(); break; // Handle patrol
            case GuardState.Suspicious: Suspicious(); break; // Handle suspicious
            case GuardState.Chase: Chase(); break; // Handle chase
            case GuardState.Search: Search(); break; // Handle search
            case GuardState.Attack: Attack(); break; // Handle attack
        }
    }

    // ── Perception ───────────────────────────────────────────────────────

    void UpdateVision() // Update vision and hearing
    {
        bool canSee = CanSeePlayer(), canHear = CanHearPlayer(); // Sample senses

        if (canSee) { loseSightTimer = loseSightDelay; lastKnownPlayerPos = player.position; } // Refresh tracking
        else loseSightTimer -= Time.fixedDeltaTime; // Decrement timer

        if (loseSightTimer > 0f) // If still tracking
        {
            bool inAttackRange = Vector3.Distance(transform.position, player.position) <= attackRange; // Close enough to shoot
            if (inAttackRange && state != GuardState.Attack) // Enter attack
            {
                AlertNearbyGuards(); // Alert others
                SetState(GuardState.Attack); // Switch state
            }
            else if (!inAttackRange) // Out of attack range
            {
                if (state == GuardState.Patrol || state == GuardState.Suspicious || state == GuardState.Search) // From idle states
                {
                    AlertNearbyGuards(); // Alert others
                    SetState(GuardState.Chase); // Begin chase
                }
                else if (state == GuardState.Attack && attackStateTimer >= attackStateMinTime) // From attack after min time
                    SetState(GuardState.Chase); // Return to chase
            }
        }
        else if (canHear && (state == GuardState.Patrol || state == GuardState.Search)) // Hearing in idle states
        {
            Vector3 toPlayer = player.position - transform.position; // Vector to player
            toPlayer.y = 0f; // Flatten
            float dist = toPlayer.magnitude; // Distance
            Vector3 dir = toPlayer / dist; // Normalized direction

            // If wall blocks sound source, investigate near the wall instead of through it
            suspiciousPos = Physics.Raycast(transform.position + Vector3.up * 0.5f, dir, out RaycastHit wallHit, dist, obstacleLayerMask)
                ? FindClearPoint(wallHit.point, dir)
                : player.position;

            SetState(GuardState.Suspicious); // Enter suspicious
        }
        else if (state == GuardState.Chase || state == GuardState.Attack) // Lost player while hunting
            StartSearch(); // Begin search
    }

    bool CanSeePlayer() // Check if can see player
    {
        Vector3 eye = transform.position + Vector3.up, target = player.position + Vector3.up; // Eye and target points
        Vector3 dir = target - eye; // Direction
        float dist = dir.magnitude; // Distance
        if (dist > visionRange) return false; // Too far

        float angle = Vector3.Angle(transform.forward, new Vector3(dir.x, 0, dir.z)); // Horizontal angle
        if (angle > visionAngle * 0.5f && (dist > peripheralRange || angle > peripheralAngle * 0.5f)) // Outside both cones
            return false; // Not in view

        return Physics.Raycast(eye, dir.normalized, out RaycastHit hit, visionRange) && hit.transform == player; // Line of sight
    }

    bool CanHearPlayer() // Check if can hear player
    {
        if (!playerRb) return false; // No player rigidbody
        Vector3 v = playerRb.linearVelocity; v.y = 0f; // Flat velocity
        return v.magnitude >= hearingRunThreshold && Vector3.Distance(transform.position, player.position) <= hearingRange; // Running and in range
    }

    void AlertNearbyGuards() // Alert all nearby guards
    {
        foreach (var g in allGuards) // For each guard
            if (g != this && !g.isDead && Vector3.Distance(transform.position, g.transform.position) <= alertRadius) // Skip self/dead, in range
                g.ReceiveAlert(lastKnownPlayerPos); // Send alert
    }

    // ── State Machine ────────────────────────────────────────────────────

    void ResetPath() { currentPath.Clear(); pathIndex = 0; } // Clear A* path and reset index

    void SetState(GuardState next) // Set new state
    {
        if (state == next) return; // No change
        GuardState prev = state; // Save old state
        state = next; // Apply new state
        ResetPath(); // Drop any stale path

        switch (next) // Per-state entry setup
        {
            case GuardState.Chase:
                repathTimer = repathInterval; // Force immediate repath
                lastRepathPlayerPos = Vector3.positiveInfinity; // Invalidate cached player pos
                if (prev == GuardState.Attack && animator) // Coming from attack
                {
                    attackAnimStarted = false; // Reset animation flag
                    SetAnim(false, true, "infantry_combat_run"); // Switch to run anim
                }
                break;
            case GuardState.Attack:
                rb.linearVelocity = Vector3.zero; // Kill chase momentum so guard doesn't slide into player
                rb.angularVelocity = Vector3.zero;
                break;
            case GuardState.Suspicious:
                suspiciousTimer = 0f; // Reset investigation timer
                break;
        }
    }

    void SetAnim(bool shooting, bool running, string clip) // Helper: configure animator in one call
    {
        animator.SetBool("isShooting", shooting); // Set shooting flag
        animator.SetBool("isRunning", running); // Set running flag
        animator.CrossFade(clip, 0f); // Blend to clip
    }

    void Patrol() // Patrol state
    {
        if (points.Length == 0) return; // No points to patrol

        if (isPatrolLooking) // If currently looking around
        {
            patrolLookTimer += Time.fixedDeltaTime; // Increment timer
            rb.MoveRotation(rb.rotation * Quaternion.Euler(0, 70f * Time.fixedDeltaTime, 0)); // Rotate in place
            if (patrolLookTimer >= 1.8f) { isPatrolLooking = false; AdvancePatrol(); } // Done looking
            return; // Early exit
        }

        if (isWallFollowing) // If wall-following while patrolling
        {
            AdvancePatrol(); // Skip current waypoint
            isWallFollowing = false; wallFollowTimer = 0f; stuckTimer = 0f; // Reset wall-follow state
        }

        MoveTo(points[patrolIndex].position, patrolSpeed); // Move toward current waypoint

        if (Vector3.Distance(transform.position, points[patrolIndex].position) <= pointStopDistance) // Reached waypoint
        {
            if (Random.value < patrolLookChance) { isPatrolLooking = true; patrolLookTimer = 0f; } // Maybe stop and look
            else AdvancePatrol(); // Otherwise next waypoint
        }
    }

    void AdvancePatrol() => patrolIndex = (patrolIndex + 1) % points.Length; // Move to next patrol point (wrap)

    void Suspicious() // Suspicious state
    {
        suspiciousTimer += Time.fixedDeltaTime; // Increment timer

        if (Vector3.Distance(transform.position, suspiciousPos) > 2f) // Far from investigation point
            MoveRaw(suspiciousPos, suspiciousSpeed); // Move toward point
        else // At investigation point
        {
            Vector3 dir = suspiciousPos - transform.position; dir.y = 0f; // Direction (flat)
            if (dir.sqrMagnitude > 0.01f) // Has direction
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(dir), 5f * Time.fixedDeltaTime)); // Look around
        }

        if (suspiciousTimer >= suspiciousInvestigateTime) // Done investigating
        {
            lastKnownPlayerPos = suspiciousPos; // Treat suspicious pos as last known
            StartSearch(); // Begin search
        }
    }

    void Chase() // Chase state
    {
        repathTimer += Time.fixedDeltaTime; // Increment repath timer
        if (repathTimer >= repathInterval) // Time to repath
        {
            repathTimer = 0f; // Reset timer
            // Only rebuild when the player has moved enough to matter // Optimization
            if (Vector3.Distance(player.position, lastRepathPlayerPos) > 1.5f || isWallFollowing) // Player moved or guard is stuck
            {
                lastRepathPlayerPos = player.position; // Update cached pos
                currentPath = AStarPath(transform.position, player.position); // Calculate new path
                pathIndex = 0; // Reset index
            }
        }
        MoveTo(player.position, chaseSpeed); // Follow path/player
    }

    void Attack() // Attack state
    {
        rb.linearVelocity = Vector3.zero; // Pin guard in place — prevents player collider from pushing it each frame
        if (!attackAnimStarted && animator) // First frame of attack
        {
            attackAnimStarted = true; // Mark started
            attackStateTimer = 0f; // Reset state timer
            SetAnim(true, false, "infantry_combat_shoot"); // Play shoot animation
        }

        attackTimer += Time.fixedDeltaTime; // Increment cooldown timer
        attackStateTimer += Time.fixedDeltaTime; // Increment state timer

        Vector3 dir = player.position - transform.position; dir.y = 0f; // Direction to player (flat)
        if (dir.sqrMagnitude > 0.001f) // Has direction
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(dir), 6f * Time.fixedDeltaTime)); // Face player smoothly

        if (attackTimer >= attackCooldown) // Time to fire
        {
            attackTimer = 0f; // Reset cooldown
            playerHealth?.TakeDamage(damagePerShot); // Deal damage
        }

        if (Vector3.Distance(transform.position, player.position) > attackExitRange && attackStateTimer >= attackStateMinTime) // Too far and stayed long enough
        {
            attackAnimStarted = false; // Reset flag
            SetState(GuardState.Chase); // Return to chase
        }
    }

    void StartSearch() // Start search state
    {
        if (animator) { animator.speed = 1f; SetAnim(false, true, "infantry_combat_run"); } // Reset animator and play run
        state = GuardState.Search; // Enter search
        searchIndex = 0; waitTimer = 0f; // Reset search progress
        ResetPath(); // Drop A* path
        searchPath = BFSSearchWaypoints(lastKnownPlayerPos); // Generate search waypoints around last known pos
    }

    void Search() // Search state
    {
        if (searchPath == null || searchIndex >= searchPath.Count) { state = GuardState.Patrol; return; } // No more waypoints → patrol

        Vector3 target = searchPath[searchIndex]; // Current waypoint
        MoveRaw(target, searchSpeed); // Move toward waypoint

        if (Vector3.Distance(transform.position, target) <= pointStopDistance) // Reached waypoint
        {
            waitTimer += Time.fixedDeltaTime; // Increment wait timer
            rb.MoveRotation(rb.rotation * Quaternion.Euler(0, 80f * Time.fixedDeltaTime, 0)); // Look around
            if (waitTimer >= waitAtSearchPoint) { waitTimer = 0f; searchIndex++; } // Done waiting → next waypoint
        }
    }

    // ── Movement & Avoidance ─────────────────────────────────────────────

    void UpdateAnimator() // Update animation state
    {
        if (!animator || !animator.runtimeAnimatorController) return; // No animator

        if (isDead) // If dead
        {
            animator.SetBool("isRunning", false); // Not running
            animator.SetBool("isShooting", false); // Not shooting
            animator.speed = 0f; // Freeze
            return; // Early exit
        }

        bool isShooting = state == GuardState.Attack; // Attacking?
        animator.speed = 1f; // Normal speed
        animator.SetBool("isRunning", !isShooting && computedSpeed > 0.05f); // Run if moving and not attacking
        animator.SetBool("isShooting", isShooting); // Set shoot flag
    }

    void UpdateStuckDetection() // Detect if stuck on obstacle
    {
        if (state == GuardState.Attack) // Stuck detection disabled during attack
        {
            stuckTimer = wallFollowTimer = 0f; // Reset timers
            isWallFollowing = false; // Stop wall-following
            lastCheckedPosition = transform.position; // Sync position
            return; // Early exit
        }

        stuckCheckTimer += Time.fixedDeltaTime; // Increment check timer
        if (stuckCheckTimer >= stuckCheckInterval) // Time to sample movement
        {
            stuckCheckTimer = 0f; // Reset timer
            if (Vector3.Distance(transform.position, lastCheckedPosition) >= stuckMoveThreshold) // Moved enough
            {
                stuckTimer = wallFollowTimer = 0f; // Not stuck
                isWallFollowing = false; // Stop wall-following
                lockedWallDir = Vector3.zero; // Clear locked direction
            }
            else // Didn't move enough
            {
                stuckTimer += stuckCheckInterval; // Increment stuck timer
                if (stuckTimer >= stuckTimeLimit && !isWallFollowing) // Stuck too long
                {
                    stuckTimer = wallFollowTimer = 0f; // Reset
                    isWallFollowing = true; // Start wall-following
                }
            }
            lastCheckedPosition = transform.position; // Update sample
        }

        if (isWallFollowing) // Wall-following active
        {
            wallFollowTimer += Time.fixedDeltaTime; // Increment duration
            if (wallFollowTimer >= wallFollowDuration) // Done wall-following
            {
                isWallFollowing = false; // Stop
                wallFollowTimer = 0f; // Reset timer
                lockedWallDir = Vector3.zero; // Clear direction
            }
        }
    }

    int PickWallFollowSide() // Pick wall following side
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f; // Origin from chest
        if (!Physics.SphereCast(origin, avoidRadius, transform.right, out _, detectDistance * 2f, obstacleLayerMask)) return 1; // Right is free
        if (!Physics.SphereCast(origin, avoidRadius, -transform.right, out _, detectDistance * 2f, obstacleLayerMask)) return -1; // Left is free
        return 1; // Default right
    }

    void MoveTo(Vector3 target, float speed) // Move to target using path
    {
        if (currentPath.Count > 0 && pathIndex < currentPath.Count) // Has active path
        {
            Vector3 wp = currentPath[pathIndex]; // Current waypoint
            wp.y = transform.position.y; // Keep y at guard height

            if (Vector3.Distance(transform.position, wp) <= pointStopDistance) // Reached waypoint
            {
                if (++pathIndex >= currentPath.Count) currentPath.Clear(); // Path consumed
                else target = currentPath[pathIndex]; // Use next waypoint
            }
            else target = wp; // Use current waypoint
        }
        MoveRaw(target, speed); // Move toward target with avoidance
    }

    void MoveRaw(Vector3 target, float speed) // Raw movement with avoidance
    {
        Vector3 dir = target - transform.position; dir.y = 0f; // Direction (flat)
        if (dir.sqrMagnitude < 0.001f) return; // No movement needed
        dir.Normalize(); // Normalize

        Vector3 moveDir = (currentPath.Count > 0 && !isWallFollowing) ? dir : FindBestDirection(dir); // Trust path unless stuck
        if (moveDir.sqrMagnitude < 0.001f) return; // No valid direction

        Vector3 origin = transform.position + Vector3.up * 0.5f; // Cast from chest
        if (Physics.SphereCast(origin, avoidRadius, moveDir, out RaycastHit hit, speed * Time.fixedDeltaTime + 0.05f, obstacleLayerMask)) // Wall ahead
        {
            Vector3 normal = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized; // Flat wall normal
            Vector3 slide = Vector3.ProjectOnPlane(moveDir, normal).normalized; // Slide along wall
            if (slide.sqrMagnitude <= 0.01f) return; // Can't slide
            moveDir = slide; // Use slide direction
        }

        rb.MovePosition(rb.position + moveDir * speed * Time.fixedDeltaTime); // Move

        // Look at player when chasing (if visible), otherwise look at move direction
        Vector3 lookDir = moveDir; // Default: face movement
        if (state == GuardState.Chase && loseSightTimer > 0f) // Chasing visible player
        {
            Vector3 toPlayer = player.position - transform.position; toPlayer.y = 0f; // Direction to player (flat)
            if (toPlayer.sqrMagnitude > 0.01f) lookDir = toPlayer.normalized; // Face player instead of path
        }

        rb.MoveRotation(Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(lookDir), 6f * Time.fixedDeltaTime)); // Rotate smoothly toward look direction
    }

    void SeparateFromWalls() // Separate from overlapping geometry
    {
        Vector3 head = transform.position + Vector3.up * 0.5f; // Sample point
        foreach (var col in Physics.OverlapSphere(head, avoidRadius, obstacleLayerMask)) // For each overlap
        {
            Vector3 push = head - col.ClosestPoint(head); push.y = 0f; // Push direction (flat)
            if (push.sqrMagnitude < 0.0001f) continue; // Tiny push — ignore
            float overlap = avoidRadius - push.magnitude; // Overlap amount
            if (overlap > 0f) rb.MovePosition(rb.position + push.normalized * (overlap + 0.02f)); // Push out with margin
        }
    }

    Vector3 FindClearPoint(Vector3 wallHit, Vector3 dirToWall) // Find clear point pulled back from wall
    {
        float clearRadius = avoidRadius + 0.3f; // Clear radius
        float maxPullback = Vector3.Distance(transform.position, wallHit) + 1f; // Max distance to pull back
        for (float pullback = clearRadius + 0.1f; pullback <= maxPullback; pullback += 0.25f) // Try progressively farther
        {
            Vector3 candidate = wallHit - dirToWall * pullback; // Candidate position
            candidate.y = transform.position.y; // Keep y
            if (!Physics.CheckSphere(candidate + Vector3.up * 0.5f, clearRadius, obstacleLayerMask)) // Clear
                return candidate; // Return position
        }
        return transform.position; // Fallback to current position
    }

    Vector3 FindBestDirection(Vector3 preferredDir) // Find avoidance direction
    {
        if (isWallFollowing) // Wall-following branch
        {
            if (!IsBlocked(preferredDir)) { isWallFollowing = false; lockedWallDir = Vector3.zero; return preferredDir; } // Exit if preferred is clear
            if (!IsBlocked(lockedWallDir)) return lockedWallDir; // Continue in locked direction
            Vector3 opp = Quaternion.Euler(0, 180f, 0) * lockedWallDir; // Try opposite direction
            if (!IsBlocked(opp)) return lockedWallDir = opp; // Switch to opposite
            return Vector3.zero; // Blocked everywhere
        }

        if (!IsBlocked(preferredDir)) return preferredDir; // Preferred clear

        float step = 160f / (avoidRaySamples + 1); // Step angle between samples
        for (int i = 1; i <= avoidRaySamples; i++) // For each sample pair
        {
            float a = step * i; // Angle offset
            Vector3 left = Quaternion.Euler(0, a, 0) * preferredDir; // Left side
            Vector3 right = Quaternion.Euler(0, -a, 0) * preferredDir; // Right side
            if (!IsBlocked(left)) return left; // Left clear
            if (!IsBlocked(right)) return right; // Right clear
        }

        // All directions blocked — start wall-following
        isWallFollowing = true; // Enable wall-following
        wallFollowTimer = 0f; // Reset timer
        return lockedWallDir = Quaternion.Euler(0, 90f * PickWallFollowSide(), 0) * preferredDir; // Lock 90° to a free side
    }

    bool IsBlocked(Vector3 dir) => // Check if direction is blocked
        Physics.SphereCast(transform.position + Vector3.up * 0.5f, avoidRadius, dir, out _, detectDistance, obstacleLayerMask); // SphereCast ahead

    // ── Editor ───────────────────────────────────────────────────────────

#if UNITY_EDITOR
    void OnDrawGizmosSelected() // Draw debug gizmos in scene view
    {
        Gizmos.color = Color.yellow;             Gizmos.DrawWireSphere(transform.position, visionRange);     // Vision range
        Gizmos.color = new Color(1f, 0.8f, 0f);  Gizmos.DrawWireSphere(transform.position, peripheralRange); // Peripheral range
        Gizmos.color = Color.red;                Gizmos.DrawWireSphere(transform.position, attackRange);     // Attack range
        Gizmos.color = new Color(1f, 0.5f, 0f);  Gizmos.DrawWireSphere(transform.position, hearingRange);    // Hearing range
        Gizmos.color = Color.magenta;            Gizmos.DrawWireSphere(transform.position, alertRadius);     // Alert radius

        if (currentPath != null && currentPath.Count > 1) // Draw A* path
        {
            Gizmos.color = Color.blue; // Blue path
            for (int i = 0; i < currentPath.Count - 1; i++) // For each segment
                Gizmos.DrawLine(currentPath[i] + Vector3.up * 0.3f, currentPath[i + 1] + Vector3.up * 0.3f); // Draw segment
        }

        if (searchPath != null) // Draw search waypoints
        {
            Gizmos.color = Color.green; // Green spheres
            foreach (var p in searchPath) Gizmos.DrawSphere(p, 0.25f); // Draw waypoint
        }
    }
#endif
}