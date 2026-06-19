using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class EnvironmentScatterer : MonoBehaviour
{
    [Header("References")]
    public Terrain terrain;
    public GameObject treePrefabA;
    public GameObject treePrefabB;
    public GameObject bushPrefab;

    [Header("Counts")]
    public int treeCount = 0;
    public int bushCount = 90;

    [Header("Placement")]
    public int seed = 275;
    public float terrainMargin = 4f;
    public float minDistanceFromSafeZones = 4f;
    public float minDistanceBetweenTrees = 3.5f;
    public float heightOffset = 0.02f;

    [Header("Scale")]
    public Vector2 treeScaleRange = new Vector2(1.2f, 2.0f);
    public Vector2 bushScaleRange = new Vector2(0.45f, 0.9f);

    [Header("Physics")]
    public bool disableGeneratedColliders = true;

    private const string GeneratedRootName = "Generated Trees And Bushes";
    void OnEnable()
    {
        if (Application.isPlaying) return;
        Regenerate();
    }

    void OnValidate()
    {
        if (Application.isPlaying) return;
        Regenerate();
    }

    [ContextMenu("Regenerate Foliage")]
    public void Regenerate()
    {
        if (Application.isPlaying) return;
        if (terrain == null || bushPrefab == null) return;

        ClearGenerated();

        Transform root = new GameObject(GeneratedRootName).transform;
        root.SetParent(transform, false);

        System.Random random = new System.Random(seed);
        List<Vector3> treePositions = new List<Vector3>();
        List<Transform> safeZones = GetSafeZones();

        for (int i = 0; i < treeCount && treePrefabA != null; i++)
        {
            GameObject prefab = treePrefabB != null && random.NextDouble() > 0.5 ? treePrefabB : treePrefabA;
            if (TryGetPlacement(random, safeZones, treePositions, minDistanceBetweenTrees, out Vector3 position))
            {
                treePositions.Add(position);
                Spawn(prefab, root, $"Tree_{i:00}", position, RandomRange(random, treeScaleRange), random);
            }
        }

        for (int i = 0; i < bushCount; i++)
        {
            if (TryGetPlacement(random, safeZones, treePositions, 1.5f, out Vector3 position))
                Spawn(bushPrefab, root, $"Bush_{i:00}", position, RandomRange(random, bushScaleRange), random);
        }
    }

    private void ClearGenerated()
    {
        Transform existing = transform.Find(GeneratedRootName);
        if (existing == null) return;

        if (Application.isPlaying)
            Destroy(existing.gameObject);
        else
            DestroyImmediate(existing.gameObject);
    }

    private List<Transform> GetSafeZones()
    {
        List<Transform> safeZones = new List<Transform>();
        GameObject[] safeZoneObjects = GameObject.FindGameObjectsWithTag("Safezone");

        foreach (GameObject safeZone in safeZoneObjects)
            safeZones.Add(safeZone.transform);

        return safeZones;
    }

    private bool TryGetPlacement(
        System.Random random,
        List<Transform> safeZones,
        List<Vector3> treePositions,
        float minTreeDistance,
        out Vector3 position)
    {
        TerrainData terrainData = terrain.terrainData;
        Vector3 terrainOrigin = terrain.transform.position;

        for (int attempt = 0; attempt < 80; attempt++)
        {
            float x = terrainOrigin.x + terrainMargin + Random01(random) * (terrainData.size.x - terrainMargin * 2f);
            float z = terrainOrigin.z + terrainMargin + Random01(random) * (terrainData.size.z - terrainMargin * 2f);
            float y = terrainOrigin.y + terrain.SampleHeight(new Vector3(x, 0f, z)) + heightOffset;
            position = new Vector3(x, y, z);

            if (IsNearSafeZone(position, safeZones)) continue;
            if (IsNearTree(position, treePositions, minTreeDistance)) continue;

            return true;
        }

        position = Vector3.zero;
        return false;
    }

    private bool IsNearSafeZone(Vector3 position, List<Transform> safeZones)
    {
        foreach (Transform safeZone in safeZones)
        {
            Vector2 a = new Vector2(position.x, position.z);
            Vector2 b = new Vector2(safeZone.position.x, safeZone.position.z);
            if (Vector2.Distance(a, b) < minDistanceFromSafeZones)
                return true;
        }

        return false;
    }

    private static bool IsNearTree(Vector3 position, List<Vector3> treePositions, float minDistance)
    {
        foreach (Vector3 treePosition in treePositions)
        {
            Vector2 a = new Vector2(position.x, position.z);
            Vector2 b = new Vector2(treePosition.x, treePosition.z);
            if (Vector2.Distance(a, b) < minDistance)
                return true;
        }

        return false;
    }

    private void Spawn(GameObject prefab, Transform root, string objectName, Vector3 position, float scale, System.Random random)
    {
        GameObject instance = Instantiate(prefab, position, Quaternion.Euler(0f, Random01(random) * 360f, 0f), root);
        instance.name = objectName;
        instance.transform.localScale = Vector3.one * scale;
        FoliageMaterialEnhancer.Enhance(instance);

        if (!disableGeneratedColliders) return;

        foreach (Collider generatedCollider in instance.GetComponentsInChildren<Collider>())
            generatedCollider.enabled = false;
    }

    private static float RandomRange(System.Random random, Vector2 range)
    {
        return Mathf.Lerp(range.x, range.y, Random01(random));
    }

    private static float Random01(System.Random random)
    {
        return (float)random.NextDouble();
    }
}
