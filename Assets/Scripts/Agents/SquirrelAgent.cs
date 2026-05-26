using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

// Observation space: 15 floats
// Actions: Continuous[0]=forward, Continuous[1]=turn  |  Discrete[0]: 0=move 1=rest
public class SquirrelAgent : Agent
{
    [Header("State")]
    public float hunger = 0f;   // 0=full, 1=starving
    public float energy = 1f;   // 0=exhausted, 1=full
    public float fear  = 0f;   // 0=calm, 1=terrified

    [Header("Spawn")]
    public Terrain terrain;
    public float spawnRadius = 20f;

    [Header("Movement")]
    public float moveSpeed = 3f;
    public float turnSpeed = 150f;

    [Header("Vision")]
    public float visionRadius = 20f;

    [Header("Rates")]
    public float hungerRate        = 0.0003f;  // slower hunger = longer episodes
    public float energyDrainRate   = 0.001f;
    public float energyRestoreRate = 0.005f;
    public float fearDecayRate     = 0.002f;

    [Header("Obstacle Avoidance")]
    public float obstacleProximityPenaltyDistance = 2f;
    public float obstacleProximityPenalty = 0.002f;

    private Rigidbody rb;
    private bool isResting;
    private bool isInSafeZone;
    private Vector3 lastPosition;
    private float prevDistToAcorn = -1f;

    // Public metrics read by MetricsRecorder
    public int   AcornsCollected { get; private set; }
    public float TotalDistance   { get; private set; }
    public int   CollisionCount  { get; private set; }

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    public override void OnEpisodeBegin()
    {
        hunger = 0f;
        energy = 1f;
        fear   = 0f;
        AcornsCollected = 0;
        TotalDistance   = 0f;
        CollisionCount  = 0;
        isResting       = false;
        prevDistToAcorn = -1f;

        // Reset position to a random point on the terrain
        Vector3 spawnPos = GetRandomSpawnPosition();
        transform.position = spawnPos;
        rb.linearVelocity  = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        lastPosition = spawnPos;
    }

    private Vector3 GetRandomSpawnPosition()
    {
        float margin = 5f;
        float size   = terrain != null ? terrain.terrainData.size.x : 50f;
        float x = Random.Range(margin, size - margin);
        float z = Random.Range(margin, size - margin);
        float y = terrain != null
            ? terrain.SampleHeight(new Vector3(x, 0f, z)) + 1f
            : 3f;
        return new Vector3(x, y, z);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Own state (3)
        sensor.AddObservation(hunger);
        sensor.AddObservation(energy);
        sensor.AddObservation(fear);

        // Context (1)
        sensor.AddObservation(isInSafeZone ? 1f : 0f);

        // Velocity normalized (2)
        sensor.AddObservation(rb.linearVelocity.x / moveSpeed);
        sensor.AddObservation(rb.linearVelocity.z / moveSpeed);

        // Nearest acorn: local dir + normalized dist (3)
        AddNearestByTag("Acorn", sensor);

        // Nearest other squirrel (3)
        AddNearestByTag("Squirrel", sensor);

        // Nearest obstacle: local dir + normalized dist (3)
        AddNearestByTag("Obstacle", sensor);
    }

    private void AddNearestByTag(string tag, VectorSensor sensor)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, visionRadius);
        Collider nearest = null;
        float minDist = float.MaxValue;
        foreach (var c in hits)
        {
            if (!c.CompareTag(tag) || c.gameObject == gameObject) continue;
            float d = Vector3.Distance(transform.position, c.transform.position);
            if (d < minDist) { minDist = d; nearest = c; }
        }

        if (nearest != null)
        {
            Vector3 toTarget = nearest.transform.position - transform.position;
            Vector3 localDir = transform.InverseTransformDirection(toTarget.normalized);
            sensor.AddObservation(localDir.x);
            sensor.AddObservation(localDir.z);
            sensor.AddObservation(minDist / visionRadius);
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(1f);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float forward = actions.ContinuousActions[0];
        float turn    = actions.ContinuousActions[1];
        isResting     = actions.DiscreteActions[0] == 1;

        if (!isResting)
        {
            transform.Rotate(0f, turn * turnSpeed * Time.deltaTime, 0f);

            // Move along terrain slope so the squirrel can climb hills naturally
            Vector3 moveDir = transform.forward * forward;
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 2f))
                moveDir = Vector3.ProjectOnPlane(moveDir, hit.normal).normalized * Mathf.Abs(forward);

            rb.linearVelocity = new Vector3(
                moveDir.x * moveSpeed,
                rb.linearVelocity.y,
                moveDir.z * moveSpeed);

            // Steeper slope = more energy drain
            float slopeCost = hit.normal != Vector3.zero
                ? 1f + (1f - hit.normal.y) * 2f
                : 1f;
            energy -= energyDrainRate * slopeCost;
        }
        else
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            energy += energyRestoreRate;
        }

        hunger = Mathf.Clamp01(hunger + hungerRate);
        energy = Mathf.Clamp01(energy);
        fear   = Mathf.Clamp01(fear - fearDecayRate);

        TotalDistance += Vector3.Distance(transform.position, lastPosition);
        lastPosition   = transform.position;

        // Per-step penalty
        AddReward(-0.0005f);

        // Reward shaping: getting closer to nearest acorn
        float currDist = DistanceToNearestAcorn();
        if (prevDistToAcorn > 0f && currDist > 0f)
            AddReward((prevDistToAcorn - currDist) * 0.05f);
        prevDistToAcorn = currDist;

        if (hunger > 0.8f) AddReward(-0.005f);
        if (energy < 0.2f) AddReward(-0.005f);

        float obstacleDist = DistanceToNearestByTag("Obstacle", visionRadius);
        if (obstacleDist > 0f && obstacleDist < obstacleProximityPenaltyDistance)
        {
            float closeness = 1f - obstacleDist / obstacleProximityPenaltyDistance;
            AddReward(-obstacleProximityPenalty * closeness);
        }

        if (energy <= 0f || hunger >= 1f)
        {
            AddReward(-1f);
            SimulationManager.Instance?.OnAgentEpisodeEnd(this);
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var ca = actionsOut.ContinuousActions;
        var da = actionsOut.DiscreteActions;
        ca[0] = Input.GetAxis("Vertical");
        ca[1] = Input.GetAxis("Horizontal");
        da[0] = Input.GetKey(KeyCode.Space) ? 1 : 0;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Acorn"))
        {
            AcornsCollected++;
            hunger = Mathf.Max(0f, hunger - 0.3f);
            energy = Mathf.Min(1f, energy + 0.1f);
            AddReward(1.0f);
            other.GetComponent<Acorn>()?.OnCollected();
        }
        else if (other.CompareTag("Safezone"))
        {
            isInSafeZone = true;
            if (fear > 0.5f) AddReward(0.1f);
        }
        else if (other.CompareTag("Obstacle"))
        {
            ApplyObstacleEffect(other.GetComponent<Obstacle>());
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Obstacle")) return;

        Obstacle obstacle = other.GetComponent<Obstacle>();
        if (obstacle != null && obstacle.type == Obstacle.ObstacleType.MudPuddle)
            energy = Mathf.Max(0f, energy - obstacle.energyPenalty * Time.deltaTime);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Safezone"))
            isInSafeZone = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Obstacle"))
        {
            CollisionCount++;
            ApplyObstacleEffect(collision.gameObject.GetComponent<Obstacle>());
        }
    }

    private void ApplyObstacleEffect(Obstacle obstacle)
    {
        if (obstacle == null)
        {
            fear = Mathf.Min(1f, fear + 0.2f);
            energy = Mathf.Max(0f, energy - 0.02f);
            AddReward(-0.1f);
            return;
        }

        fear = Mathf.Min(1f, fear + obstacle.fearIncrease);
        energy = Mathf.Max(0f, energy - obstacle.energyPenalty);
        AddReward(-obstacle.collisionPenalty);
    }

    private float DistanceToNearestAcorn()
    {
        return DistanceToNearestByTag("Acorn", 200f);
    }

    private float DistanceToNearestByTag(string tag, float radius)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        float minDist = -1f;
        foreach (var c in hits)
        {
            if (!c.CompareTag(tag) || c.gameObject == gameObject) continue;
            float d = Vector3.Distance(transform.position, c.transform.position);
            if (minDist < 0f || d < minDist) minDist = d;
        }
        return minDist;
    }

    private static Collider NearestOf(Collider[] hits, GameObject self)
    {
        Collider nearest = null;
        float minDist = float.MaxValue;
        foreach (var c in hits)
        {
            if (c.gameObject == self) continue;
            float d = Vector3.Distance(self.transform.position, c.transform.position);
            if (d < minDist) { minDist = d; nearest = c; }
        }
        return nearest;
    }
}
