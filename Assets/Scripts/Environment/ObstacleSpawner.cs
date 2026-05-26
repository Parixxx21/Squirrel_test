using System.Collections.Generic;
using UnityEngine;

public class ObstacleSpawner : MonoBehaviour
{
    [Header("Setup")]
    public List<GameObject> obstaclePrefabs = new();
    public Terrain terrain;

    [Header("Spawn Area")]
    public int obstacleCount = 10;
    public float spawnRadius = 25f;
    public float heightOffset = 0.2f;
    public float minDistanceBetweenObstacles = 3f;

    private readonly List<GameObject> spawnedObstacles = new();

    void Start()
    {
        SpawnAll();
    }

    public void ResetAll()
    {
        foreach (var obstacle in spawnedObstacles)
            Destroy(obstacle);

        spawnedObstacles.Clear();
        SpawnAll();
    }

    private void SpawnAll()
    {
        if (obstaclePrefabs.Count == 0) return;

        int attempts = 0;
        while (spawnedObstacles.Count < obstacleCount && attempts < obstacleCount * 20)
        {
            attempts++;
            Vector3 position = RandomTerrainPosition();
            if (!IsFarEnoughFromExisting(position)) continue;

            GameObject prefab = obstaclePrefabs[Random.Range(0, obstaclePrefabs.Count)];
            GameObject obstacle = Instantiate(prefab, position, RandomYRotation(), transform);
            EnsureObstacleSetup(obstacle);
            spawnedObstacles.Add(obstacle);
        }
    }

    private Vector3 RandomTerrainPosition()
    {
        Vector3 center = transform.position;
        float x = center.x + Random.Range(-spawnRadius, spawnRadius);
        float z = center.z + Random.Range(-spawnRadius, spawnRadius);
        float y = terrain != null
            ? terrain.SampleHeight(new Vector3(x, 0f, z)) + heightOffset
            : center.y + heightOffset;
        return new Vector3(x, y, z);
    }

    private bool IsFarEnoughFromExisting(Vector3 position)
    {
        foreach (var obstacle in spawnedObstacles)
        {
            if (Vector3.Distance(position, obstacle.transform.position) < minDistanceBetweenObstacles)
                return false;
        }

        return true;
    }

    private static Quaternion RandomYRotation()
    {
        return Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
    }

    private static void EnsureObstacleSetup(GameObject obstacle)
    {
        if (!obstacle.TryGetComponent(out Collider obstacleCollider))
            obstacleCollider = obstacle.AddComponent<BoxCollider>();

        if (!obstacle.TryGetComponent(out Obstacle obstacleSettings))
            obstacleSettings = obstacle.AddComponent<Obstacle>();

        obstacleCollider.isTrigger = obstacleSettings.IsAreaHazard();
    }
}
