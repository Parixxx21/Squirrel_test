using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

// Observation space: 36 floats
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
    public float boundaryMargin = 3f;

    [Header("Movement")]
    public float moveSpeed = 3f;
    public float turnSpeed = 150f;

    [Header("Vision")]
    public float visionRadius = 20f;

    [Header("Map Knowledge")]
    public int maxSafeZonesObserved = 6;
    public float safeZoneDistanceNormalization = 75f;

    [Header("Rates")]
    public float hungerRate        = 0.0003f;  // slower hunger = longer episodes
    public float energyDrainRate   = 0.001f;
    public float energyRestoreRate = 0.005f;
    public float fearDecayRate     = 0.002f;

    [Header("State Thresholds")]
    public float highHungerThreshold = 0.8f;
    public float lowEnergyThreshold = 0.25f;
    public float highFearThreshold = 0.5f;
    public float terminalHungerThreshold = 1.0f;
    public float terminalEnergyThreshold = 0.0f;

    [Header("Acorn Effects")]
    public float acornHungerReduction = 0.3f;
    public float acornEnergyBonus = 0.1f;

    [Header("Reward Shaping")]
    public float stepPenalty = 0.0005f;
    public float acornReward = 1.0f;
    public float acornFearMultiplier = 0.3f;  // acorn reward multiplier when fear > highFearThreshold
    public float acornApproachRewardScale = 0.05f;
    public float hungryPenalty = 0.005f;
    public float lowEnergyPenalty = 0.005f;
    public float goodRestReward = 0.0015f;
    public float unnecessaryRestPenalty = 0.001f;
    public float safeZoneFearReliefReward = 0.003f;
    public float safeZoneApproachRewardScale = 0.05f;
    public float safeZoneEntryReward = 0.1f;
    public float terminalFailurePenalty = 1.0f;

    [Header("Predator Interface")]
    public float caughtByPredatorPenalty = 1.0f;
    public float safeZoneFearDecayMultiplier = 5.0f;

    [Header("Obstacle Avoidance")]
    public float obstacleProximityPenaltyDistance = 2f;
    public float obstacleProximityPenalty = 0.002f;

    private Rigidbody rb;
    private bool isResting;
    private bool isInSafeZone;
    private Vector3 lastPosition;
    private float prevDistToAcorn = -1f;
    private float prevDistToSafeZone = -1f;
    private Transform[] knownSafeZones = new Transform[0];

    // Public metrics read by MetricsRecorder
    public int   AcornsCollected { get; private set; }
    public float TotalDistance   { get; private set; }
    public int   CollisionCount  { get; private set; }

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.None;
        RefreshKnownSafeZones();
    }

    private void FixedUpdate()
    {
        EnforceTerrainBoundary();
        AlignToSlope();
    }

    private void AlignToSlope()
    {
        if (terrain == null || terrain.terrainData == null) return;

        Vector3 terrainPosition = terrain.transform.position;
        TerrainData terrainData = terrain.terrainData;

        float normalizedX = Mathf.InverseLerp(
            terrainPosition.x,
            terrainPosition.x + terrainData.size.x,
            transform.position.x);
        float normalizedZ = Mathf.InverseLerp(
            terrainPosition.z,
            terrainPosition.z + terrainData.size.z,
            transform.position.z);

        if (normalizedX < 0f || normalizedX > 1f || normalizedZ < 0f || normalizedZ > 1f)
            return;

        Vector3 normal = terrainData.GetInterpolatedNormal(normalizedX, normalizedZ);
        if (normal.sqrMagnitude < 0.001f) return;

        Quaternion targetRotation = Quaternion.FromToRotation(Vector3.up, normal)
            * Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        rb.angularVelocity = Vector3.zero;
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * 10f);
    }

    public override void OnEpisodeBegin()
    {
        hunger = 0f;
        energy = 1f;
        fear   = 0f;
        AcornsCollected = 0;
        TotalDistance   = 0f;
        CollisionCount  = 0;
        isResting           = false;
        prevDistToAcorn     = -1f;
        prevDistToSafeZone  = -1f;
        RefreshKnownSafeZones();

        // Reset position to a random point on the terrain
        Vector3 spawnPos = GetRandomSpawnPosition();
        transform.position = spawnPos;
        rb.linearVelocity  = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        lastPosition = spawnPos;
    }

    private Vector3 GetRandomSpawnPosition()
    {
        if (terrain == null)
            return new Vector3(25f, 3f, 25f);

        Vector3 terrainPos = terrain.transform.position;
        TerrainData data = terrain.terrainData;
        float minX = terrainPos.x + boundaryMargin;
        float maxX = terrainPos.x + data.size.x - boundaryMargin;
        float minZ = terrainPos.z + boundaryMargin;
        float maxZ = terrainPos.z + data.size.z - boundaryMargin;

        float x = Random.Range(minX, maxX);
        float z = Random.Range(minZ, maxZ);
        float y = terrain.SampleHeight(new Vector3(x, 0f, z)) + terrainPos.y + 1f;
        return new Vector3(x, y, z);
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

    private void EnforceTerrainBoundary()
    {
        if (terrain == null) return;

        Vector3 pos = transform.position;
        if (IsInsideTerrain(pos)) return;

        Vector3 terrainPos = terrain.transform.position;
        TerrainData data = terrain.terrainData;

        float minX = terrainPos.x + boundaryMargin;
        float maxX = terrainPos.x + data.size.x - boundaryMargin;
        float minZ = terrainPos.z + boundaryMargin;
        float maxZ = terrainPos.z + data.size.z - boundaryMargin;

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.z = Mathf.Clamp(pos.z, minZ, maxZ);
        pos.y = terrain.SampleHeight(pos) + terrainPos.y + 1f;

        transform.position = pos;
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
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

        // Known safe zones: local dir + normalized dist for each slot (18)
        AddKnownSafeZones(sensor);

        // Nearest obstacle: local dir + normalized dist (3)
        AddNearestByTag("Obstacle", sensor);

        // Nearest predator: local dir + normalized dist (3)
        AddNearestByTag("Predator", sensor);
    }

    private void RefreshKnownSafeZones()
    {
        GameObject[] safeZoneObjects = GameObject.FindGameObjectsWithTag("Safezone");
        System.Array.Sort(safeZoneObjects, (a, b) => string.CompareOrdinal(a.name, b.name));

        int count = Mathf.Min(maxSafeZonesObserved, safeZoneObjects.Length);
        knownSafeZones = new Transform[count];

        for (int i = 0; i < count; i++)
            knownSafeZones[i] = safeZoneObjects[i].transform;
    }

    private void AddKnownSafeZones(VectorSensor sensor)
    {
        for (int i = 0; i < maxSafeZonesObserved; i++)
        {
            Transform safeZone = i < knownSafeZones.Length ? knownSafeZones[i] : null;

            if (safeZone == null)
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(1f);
                continue;
            }

            Vector3 toSafeZone = safeZone.position - transform.position;
            Vector3 flatDirection = new Vector3(toSafeZone.x, 0f, toSafeZone.z);
            float distance = flatDirection.magnitude;

            Vector3 localDir = distance > 0.001f
                ? transform.InverseTransformDirection(flatDirection.normalized)
                : Vector3.zero;

            sensor.AddObservation(localDir.x);
            sensor.AddObservation(localDir.z);
            sensor.AddObservation(Mathf.Clamp01(distance / safeZoneDistanceNormalization));
        }
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
            bool hasGround = Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 2f);
            if (hasGround)
                moveDir = Vector3.ProjectOnPlane(moveDir, hit.normal).normalized * Mathf.Abs(forward);

            Vector3 horizontalVel = new Vector3(
                moveDir.x * moveSpeed,
                0f,
                moveDir.z * moveSpeed);

            Vector3 nextPos = transform.position + horizontalVel * Time.fixedDeltaTime;
            if (!IsInsideTerrain(nextPos))
                horizontalVel = Vector3.zero;

            rb.linearVelocity = new Vector3(
                horizontalVel.x,
                rb.linearVelocity.y,
                horizontalVel.z);

            // Steeper slope = more energy drain
            float slopeCost = hasGround && hit.normal != Vector3.zero
                ? 1f + (1f - hit.normal.y) * 2f
                : 1f;
            energy -= energyDrainRate * slopeCost;
        }
        else
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            energy += energyRestoreRate;

            if (energy < lowEnergyThreshold)
                AddReward(goodRestReward);
            else if (hunger > highHungerThreshold)
                AddReward(-unnecessaryRestPenalty);
        }

        hunger = Mathf.Clamp01(hunger + hungerRate);
        energy = Mathf.Clamp01(energy);
        fear   = Mathf.Clamp01(fear - fearDecayRate);

        TotalDistance += Vector3.Distance(transform.position, lastPosition);
        lastPosition   = transform.position;

        AddReward(-stepPenalty);

        // Reward shaping: getting closer to nearest acorn
        float currDist = DistanceToNearestAcorn();
        if (prevDistToAcorn > 0f && currDist > 0f)
            AddReward((prevDistToAcorn - currDist) * acornApproachRewardScale);
        prevDistToAcorn = currDist;

        if (hunger > highHungerThreshold) AddReward(-hungryPenalty);
        if (energy < lowEnergyThreshold) AddReward(-lowEnergyPenalty);
        if (isInSafeZone && fear > highFearThreshold) AddReward(safeZoneFearReliefReward);

        // Approach shaping: reward getting closer to safezone when scared
        if (fear > highFearThreshold && !isInSafeZone)
        {
            float currSafeDist = DistanceToNearestByTag("Safezone", 200f);
            if (prevDistToSafeZone > 0f && currSafeDist > 0f)
                AddReward((prevDistToSafeZone - currSafeDist) * safeZoneApproachRewardScale);
            prevDistToSafeZone = currSafeDist;
        }
        else
        {
            prevDistToSafeZone = -1f;
        }

        float obstacleDist = DistanceToNearestByTag("Obstacle", visionRadius);
        if (obstacleDist > 0f && obstacleDist < obstacleProximityPenaltyDistance)
        {
            float closeness = 1f - obstacleDist / obstacleProximityPenaltyDistance;
            AddReward(-obstacleProximityPenalty * closeness);
        }

        if (energy <= terminalEnergyThreshold || hunger >= terminalHungerThreshold)
        {
            AddReward(-terminalFailurePenalty);
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
            hunger = Mathf.Max(0f, hunger - acornHungerReduction);
            energy = Mathf.Min(1f, energy + acornEnergyBonus);
            AddReward(fear > highFearThreshold ? acornReward * acornFearMultiplier : acornReward);
            other.GetComponent<Acorn>()?.OnCollected();
        }
        else if (other.CompareTag("Safezone"))
        {
            isInSafeZone = true;
            if (fear > highFearThreshold) AddReward(safeZoneEntryReward);
        }
        else if (other.CompareTag("Obstacle"))
        {
            ApplyObstacleEffect(other.GetComponent<Obstacle>());
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Safezone"))
        {
            fear = Mathf.Max(0f, fear - fearDecayRate * safeZoneFearDecayMultiplier * Time.deltaTime);
            return;
        }

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

    public void IncreaseFear(float amount)
    {
        fear = Mathf.Clamp01(fear + Mathf.Max(0f, amount));
    }

    public void CaughtByPredator()
    {
        AddReward(-caughtByPredatorPenalty);
        SimulationManager.Instance?.OnAgentEpisodeEnd(this);
        EndEpisode();
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
