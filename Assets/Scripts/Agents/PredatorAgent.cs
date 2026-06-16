using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PredatorAgent : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 4.0f;
    public float wanderSpeedMultiplier = 0.5f;
    public float turnSpeed = 180f;

    [Header("Detection")]
    public float detectionRadius = 25f;

    [Header("Fear")]
    public float fearRadius = 22f;
    public float fearIncreaseRate = 0.4f;
    public float catchRadius = 2.5f;
    public float stopDistanceToTarget = 2.8f;

    [Header("Avoidance")]
    public float predatorAvoidRadius = 2.0f;
    public float predatorAvoidStrength = 1.5f;
    public float obstacleCheckDistance = 4f;
    public float obstacleAvoidStrength = 2.5f;

    [Header("Boundary")]
    public Terrain terrain;
    public float boundaryMargin = 3f;
    public float heightOffset = 0.8f;

    [Header("Stuck Detection")]
    public float stuckCheckInterval = 8f;
    public float stuckDistanceThreshold = 1f;

    private Rigidbody rb;
    private SquirrelAgent target;

    private Vector3 wanderDirection;
    private float wanderTimer;

    private Vector3 spawnPosition;
    private Vector3 lastCheckedPosition;
    private float stuckTimer;
    private int stuckCount;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        if (terrain == null)
            terrain = Terrain.activeTerrain;

        spawnPosition = transform.position;
        lastCheckedPosition = transform.position;
    }

    private void CheckIfStuck()
    {
        stuckTimer += Time.fixedDeltaTime;
        if (stuckTimer < stuckCheckInterval) return;

        stuckTimer = 0f;
        float moved = Vector3.Distance(transform.position, lastCheckedPosition);
        lastCheckedPosition = transform.position;

        if (moved < stuckDistanceThreshold)
        {
            stuckCount++;
            if (stuckCount >= 2)
            {
                stuckCount = 0;
                Vector3 respawn = spawnPosition;
                respawn.y = terrain != null
                    ? terrain.SampleHeight(spawnPosition) + terrain.transform.position.y + heightOffset
                    : spawnPosition.y;
                transform.position = respawn;
                rb.linearVelocity = Vector3.zero;
                wanderDirection = Vector3.zero;
            }
        }
        else
        {
            stuckCount = 0;
        }
    }

    void FixedUpdate()
    {
        target = FindNearestForager();

        if (target != null)
        {
            ChaseTarget();
            ApplyFearToTarget();
        }
        else
        {
            Wander();
        }

        AlignToSlope();
        CheckIfStuck();
    }

    private void AlignToSlope()
    {
        if (terrain == null) return;

        Vector3 terrainPos = terrain.transform.position;
        TerrainData data = terrain.terrainData;

        float normX = Mathf.InverseLerp(terrainPos.x, terrainPos.x + data.size.x, transform.position.x);
        float normZ = Mathf.InverseLerp(terrainPos.z, terrainPos.z + data.size.z, transform.position.z);

        Vector3 normal = data.GetInterpolatedNormal(normX, normZ);
        Quaternion slopeRot = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, slopeRot, Time.fixedDeltaTime * 10f);
    }

    private SquirrelAgent FindNearestForager()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius);

        SquirrelAgent nearest = null;
        float minDist = float.MaxValue;

        foreach (Collider c in hits)
        {
            SquirrelAgent forager = c.GetComponentInParent<SquirrelAgent>();
            if (forager == null) continue;

            float d = Vector3.Distance(transform.position, forager.transform.position);

            if (d < minDist)
            {
                minDist = d;
                nearest = forager;
            }
        }

        return nearest;
    }

    private bool IsPositionInSafeZone(Vector3 pos)
    {
        Collider[] hits = Physics.OverlapSphere(pos, 0.5f);
        foreach (var c in hits)
            if (c.CompareTag("Safezone")) return true;
        return false;
    }

    private bool IsTargetInSafeZone()
    {
        if (target == null) return false;
        return IsPositionInSafeZone(target.transform.position);
    }

    private void ChaseTarget()
    {
        if (IsTargetInSafeZone())
        {
            Wander();
            return;
        }

        Vector3 toTarget = target.transform.position - transform.position;
        toTarget.y = 0f;

        float dist = toTarget.magnitude;

        if (dist <= stopDistanceToTarget)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

        Vector3 direction = toTarget.normalized;
        direction += GetPredatorAvoidanceDirection() * predatorAvoidStrength;
        direction += GetObstacleAvoidanceDirection() * obstacleAvoidStrength;
        direction += GetBoundaryAvoidanceDirection();
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f) return;

        MoveInDirection(direction.normalized, moveSpeed);
    }

    private void Wander()
    {
        wanderTimer -= Time.fixedDeltaTime;

        if (wanderTimer <= 0f || wanderDirection == Vector3.zero)
            PickNewWanderDirection();

        Vector3 direction = wanderDirection;
        direction += GetPredatorAvoidanceDirection() * predatorAvoidStrength;
        direction += GetObstacleAvoidanceDirection() * obstacleAvoidStrength;
        direction += GetBoundaryAvoidanceDirection();
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            PickNewWanderDirection();

        MoveInDirection(direction.normalized, moveSpeed * wanderSpeedMultiplier);
    }

    private void PickNewWanderDirection()
    {
        wanderTimer = Random.Range(2f, 5f);

        Vector2 random2D = Random.insideUnitCircle.normalized;
        wanderDirection = new Vector3(random2D.x, 0f, random2D.y);
    }

    private Vector3 GetBoundaryAvoidanceDirection()
    {
        if (terrain == null) return Vector3.zero;

        Vector3 terrainPos = terrain.transform.position;
        TerrainData data = terrain.terrainData;
        Vector3 pos = transform.position;

        float distMinX = pos.x - (terrainPos.x + boundaryMargin);
        float distMaxX = (terrainPos.x + data.size.x - boundaryMargin) - pos.x;
        float distMinZ = pos.z - (terrainPos.z + boundaryMargin);
        float distMaxZ = (terrainPos.z + data.size.z - boundaryMargin) - pos.z;

        float threshold = 8f;
        Vector3 push = Vector3.zero;

        if (distMinX < threshold) push.x += (threshold - distMinX) / threshold;
        if (distMaxX < threshold) push.x -= (threshold - distMaxX) / threshold;
        if (distMinZ < threshold) push.z += (threshold - distMinZ) / threshold;
        if (distMaxZ < threshold) push.z -= (threshold - distMaxZ) / threshold;

        return push;
    }

    private Vector3 GetObstacleAvoidanceDirection()
    {
        Vector3 avoidDir = Vector3.zero;
        Vector3 origin = transform.position + Vector3.up * 0.5f;

        Vector3[] checkDirs = {
            transform.forward,
            Quaternion.Euler(0f, -40f, 0f) * transform.forward,
            Quaternion.Euler(0f,  40f, 0f) * transform.forward,
        };

        foreach (var dir in checkDirs)
        {
            if (Physics.Raycast(origin, dir, out RaycastHit hit, obstacleCheckDistance))
            {
                if (!hit.collider.isTrigger &&
                    !hit.collider.CompareTag("Squirrel") &&
                    !hit.collider.CompareTag("Predator"))
                {
                    float weight = 1f - hit.distance / obstacleCheckDistance;
                    avoidDir += Vector3.Cross(Vector3.up, hit.normal) * weight;
                }
            }
        }

        return avoidDir;
    }

    private Vector3 GetPredatorAvoidanceDirection()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, predatorAvoidRadius);
        Vector3 avoidDir = Vector3.zero;

        foreach (Collider c in hits)
        {
            PredatorAgent other = c.GetComponentInParent<PredatorAgent>();
            if (other == null || other == this) continue;

            Vector3 away = transform.position - other.transform.position;
            away.y = 0f;

            float dist = away.magnitude;
            if (dist < 0.001f) continue;

            avoidDir += away.normalized * (1f - dist / predatorAvoidRadius);
        }

        return avoidDir;
    }

    private void MoveInDirection(Vector3 direction, float speed)
    {
        if (direction.sqrMagnitude < 0.01f) return;

        Quaternion targetRot = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRot,
            turnSpeed * Time.fixedDeltaTime
        );

        Vector3 moveDir = transform.forward;

        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 5f))
            moveDir = Vector3.ProjectOnPlane(moveDir, hit.normal).normalized;

        Vector3 nextPos = transform.position + moveDir * speed * Time.fixedDeltaTime;

        if (!IsInsideTerrain(nextPos) || IsPositionInSafeZone(nextPos))
        {
            rb.linearVelocity = Vector3.zero;
            wanderDirection = GetDirectionToTerrainCenter();
            return;
        }

        if (terrain != null)
        {
            float y = terrain.SampleHeight(nextPos) + terrain.transform.position.y + heightOffset;
            nextPos.y = y;
        }

        rb.MovePosition(nextPos);
    }

    private bool IsInsideTerrain(Vector3 pos)
    {
        if (terrain == null) return true;

        Vector3 terrainPos = terrain.transform.position;
        TerrainData data = terrain.terrainData;

        float minX = terrainPos.x + boundaryMargin;
        float maxX = terrainPos.x + data.size.x - boundaryMargin;
        float minZ = terrainPos.z + boundaryMargin;
        float maxZ = terrainPos.z + data.size.z - boundaryMargin;

        return pos.x >= minX && pos.x <= maxX &&
               pos.z >= minZ && pos.z <= maxZ;
    }

    private Vector3 GetDirectionToTerrainCenter()
    {
        if (terrain == null)
            return -transform.forward;

        Vector3 terrainPos = terrain.transform.position;
        TerrainData data = terrain.terrainData;

        Vector3 center = terrainPos + new Vector3(data.size.x / 2f, 0f, data.size.z / 2f);
        Vector3 dir = center - transform.position;
        dir.y = 0f;

        return dir.normalized;
    }

    private void ApplyFearToTarget()
    {
        if (target == null) return;
        if (IsTargetInSafeZone()) return;  // safezone内fear不增加

        Vector3 diff = target.transform.position - transform.position;
        float dist = new Vector3(diff.x, 0f, diff.z).magnitude;
        float yDiff = Mathf.Abs(diff.y);

        if (dist <= catchRadius && yDiff <= 3f)
        {
            target.IncreaseFear(1f);
            target.CaughtByPredator();
        }
        else if (dist <= fearRadius)
        {
            float closeness = 1f - dist / fearRadius;
            target.IncreaseFear(fearIncreaseRate * closeness * Time.fixedDeltaTime);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, fearRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, catchRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, predatorAvoidRadius);
    }
}