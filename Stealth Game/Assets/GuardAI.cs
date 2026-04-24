using UnityEngine;

public class GuardAI : MonoBehaviour
{
    private enum GuardState
    {
        Patrol,
        Chase,
        Search,
        Attack
    }

    [Header("Patrol")]
    public Transform[] points;
    public float patrolSpeed = 2f;
    public float chaseSpeed = 4f;
    public float searchSpeed = 2.5f;
    public float pointStopDistance = 0.5f;

    [Header("Vision")]
    public Transform player;
    public float visionRange = 12f;
    [Range(0f, 360f)] public float visionAngle = 90f;
    public float loseSightDelay = 0.5f;

    [Header("Attack")]
    public float attackRange = 4f;
    public float attackExitRange = 6f;
    public float attackCooldown = 1.2f;
    public int damagePerShot = 50;

    [Header("Search")]
    public float searchRadius = 4f;
    public int searchPointCount = 4;
    public float waitAtSearchPoint = 1f;

    [HideInInspector] public bool isDead = false;

    private Rigidbody rb;
    private Renderer rend;
    private Animator animator;
    private PlayerHealth playerHealth;

    private int currentPatrolPoint = 0;
    private GuardState currentState = GuardState.Patrol;

    private Vector3 lastKnownPlayerPosition;
    private Vector3[] searchPoints;
    private int currentSearchIndex = 0;
    private float waitTimer = 0f;
    private bool goingToLastKnownPosition = false;

    private float attackTimer = 0f;
    private float loseSightTimer = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rend = GetComponent<Renderer>();
        animator = GetComponentInChildren<Animator>();

        if (player != null)
            playerHealth = player.GetComponent<PlayerHealth>();
    }

    void FixedUpdate()
    {
        if (isDead)
        {
            SetAnimationState(false, false, 1f);
            return;
        }

        if (rb == null || player == null)
            return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        bool directSeePlayer = CanSeePlayer();

        if (directSeePlayer)
        {
            loseSightTimer = loseSightDelay;
            lastKnownPlayerPosition = player.position;
            goingToLastKnownPosition = true;
        }
        else
        {
            loseSightTimer -= Time.fixedDeltaTime;
        }

        bool canStillTrackPlayer = loseSightTimer > 0f;

        if (canStillTrackPlayer)
        {
            if (currentState == GuardState.Attack)
            {
                if (distanceToPlayer > attackExitRange)
                    currentState = GuardState.Chase;
            }
            else
            {
                if (distanceToPlayer <= attackRange)
                    currentState = GuardState.Attack;
                else
                    currentState = GuardState.Chase;
            }
        }
        else
        {
            if (currentState == GuardState.Chase || currentState == GuardState.Attack)
            {
                StartSearch();
            }
        }

        switch (currentState)
        {
            case GuardState.Patrol:
                SetColor(Color.yellow);
                Patrol();
                break;

            case GuardState.Chase:
                SetColor(Color.red);
                ChasePlayer();
                break;

            case GuardState.Search:
                SetColor(new Color(1f, 0.5f, 0f));
                SearchForPlayer();
                break;

            case GuardState.Attack:
                SetColor(Color.magenta);
                AttackPlayer();
                break;
        }
    }

    void SetColor(Color color)
    {
        if (rend != null)
            rend.material.color = color;
    }

    void SetAnimationState(bool isRunning, bool isShooting, float animationSpeed = 1f)
    {
        if (animator != null)
        {
            animator.SetBool("isRunning", isRunning);
            animator.SetBool("isShooting", isShooting);
            animator.speed = animationSpeed;
        }
    }

    void Patrol()
    {
        SetAnimationState(true, false, 0.5f);

        if (points == null || points.Length == 0 || points[currentPatrolPoint] == null)
            return;

        Vector3 target = new Vector3(
            points[currentPatrolPoint].position.x,
            transform.position.y,
            points[currentPatrolPoint].position.z
        );

        MoveTo(target, patrolSpeed);

        float distance = FlatDistance(transform.position, target);

        if (distance <= pointStopDistance)
        {
            currentPatrolPoint++;
            if (currentPatrolPoint >= points.Length)
                currentPatrolPoint = 0;
        }
    }

    void ChasePlayer()
    {
        SetAnimationState(true, false, 1f);

        Vector3 playerPos = new Vector3(player.position.x, transform.position.y, player.position.z);
        float distanceToPlayer = Vector3.Distance(transform.position, playerPos);

        if (distanceToPlayer > attackRange - 1f)
        {
            MoveTo(playerPos, chaseSpeed);
        }
        else
        {
            Vector3 lookDirection = playerPos - transform.position;
            lookDirection.y = 0f;

            if (lookDirection.sqrMagnitude > 0.001f)
            {
                Quaternion lookRotation = Quaternion.LookRotation(lookDirection.normalized);
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, lookRotation, 8f * Time.fixedDeltaTime));
            }
        }
    }

    void AttackPlayer()
    {
        SetAnimationState(false, true, 1f);

        Vector3 playerPos = new Vector3(player.position.x, transform.position.y, player.position.z);
        Vector3 lookDirection = playerPos - transform.position;
        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude > 0.001f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(lookDirection.normalized);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, lookRotation, 8f * Time.fixedDeltaTime));
        }

        attackTimer += Time.fixedDeltaTime;

        if (attackTimer >= attackCooldown)
        {
            attackTimer = 0f;

            if (playerHealth != null)
                playerHealth.TakeDamage(damagePerShot);

            Debug.DrawLine(transform.position + Vector3.up, player.position + Vector3.up, Color.red, 0.2f);
            Debug.Log(name + " shot the player!");
        }

        float distanceToPlayer = Vector3.Distance(transform.position, playerPos);

        if (distanceToPlayer > attackExitRange)
            currentState = GuardState.Chase;
    }

    void StartSearch()
    {
        currentState = GuardState.Search;
        goingToLastKnownPosition = true;
        currentSearchIndex = 0;
        waitTimer = 0f;
        GenerateSearchPoints();
    }

    void SearchForPlayer()
    {
        SetAnimationState(true, false, 0.8f);

        Vector3 flatLastKnown = new Vector3(
            lastKnownPlayerPosition.x,
            transform.position.y,
            lastKnownPlayerPosition.z
        );

        if (goingToLastKnownPosition)
        {
            MoveTo(flatLastKnown, searchSpeed);

            if (FlatDistance(transform.position, flatLastKnown) <= pointStopDistance)
                goingToLastKnownPosition = false;

            return;
        }

        if (searchPoints == null || searchPoints.Length == 0)
        {
            currentState = GuardState.Patrol;
            return;
        }

        Vector3 target = searchPoints[currentSearchIndex];
        MoveTo(target, searchSpeed);

        if (FlatDistance(transform.position, target) <= pointStopDistance)
        {
            waitTimer += Time.fixedDeltaTime;

            if (waitTimer >= waitAtSearchPoint)
            {
                waitTimer = 0f;
                currentSearchIndex++;

                if (currentSearchIndex >= searchPoints.Length)
                {
                    currentState = GuardState.Patrol;
                    currentSearchIndex = 0;
                }
            }
        }
    }

    void GenerateSearchPoints()
    {
        searchPoints = new Vector3[searchPointCount];

        for (int i = 0; i < searchPointCount; i++)
        {
            float angle = i * Mathf.PI * 2f / searchPointCount;
            Vector3 offset = new Vector3(
                Mathf.Cos(angle) * searchRadius,
                0f,
                Mathf.Sin(angle) * searchRadius
            );

            searchPoints[i] = new Vector3(
                lastKnownPlayerPosition.x + offset.x,
                transform.position.y,
                lastKnownPlayerPosition.z + offset.z
            );
        }
    }

    void MoveTo(Vector3 target, float speed)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;

        if (direction.magnitude < 0.05f)
            return;

        direction = direction.normalized;

        Vector3 newPosition = rb.position + direction * speed * Time.fixedDeltaTime;
        rb.MovePosition(newPosition);

        Quaternion lookRotation = Quaternion.LookRotation(direction);
        rb.MoveRotation(lookRotation);
    }

    float FlatDistance(Vector3 a, Vector3 b)
    {
        Vector3 aFlat = new Vector3(a.x, 0f, a.z);
        Vector3 bFlat = new Vector3(b.x, 0f, b.z);
        return Vector3.Distance(aFlat, bFlat);
    }

    bool CanSeePlayer()
    {
        Vector3 guardEye = transform.position + Vector3.up * 1f;
        Vector3 playerTarget = player.position + Vector3.up * 1f;

        Vector3 dirToPlayer = playerTarget - guardEye;
        float distanceToPlayer = dirToPlayer.magnitude;

        if (distanceToPlayer > visionRange)
            return false;

        Vector3 flatDirToPlayer = new Vector3(dirToPlayer.x, 0f, dirToPlayer.z);
        float angle = Vector3.Angle(transform.forward, flatDirToPlayer);

        if (angle > visionAngle * 0.5f)
            return false;

        if (Physics.Raycast(guardEye, dirToPlayer.normalized, out RaycastHit hit, visionRange))
        {
            if (hit.transform == player)
                return true;
        }

        return false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, visionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackExitRange);

        Vector3 leftRay = Quaternion.Euler(0, -visionAngle * 0.5f, 0) * transform.forward * visionRange;
        Vector3 rightRay = Quaternion.Euler(0, visionAngle * 0.5f, 0) * transform.forward * visionRange;

        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position + Vector3.up, leftRay);
        Gizmos.DrawRay(transform.position + Vector3.up, rightRay);
    }
}