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
    private const string SafeZonesRootName = "SafeZones";

    void OnEnable()
    {
        Regenerate();
    }

    void OnValidate()
    {
        Regenerate();
    }

    [ContextMenu("Regenerate Foliage")]
    public void Regenerate()
    {
        if (terrain == null || bushPrefab == null) return;

        EnsureDefaultLogSafeZones();
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

        if (!disableGeneratedColliders) return;

        foreach (Collider generatedCollider in instance.GetComponentsInChildren<Collider>())
            generatedCollider.enabled = false;
    }

    private void EnsureDefaultLogSafeZones()
    {
        EnsureLogSafeZone("SafeZone_Log_1", "LogVisual_1", TerrainPoint(0.12f, 0.82f), 0f, new Vector3(1.25f, 6.2f, 1.25f));
        EnsureLogSafeZone("SafeZone_Log_2", "LogVisual_2", TerrainPoint(0.88f, 0.78f), 90f, new Vector3(1.35f, 6.8f, 1.35f));
    }

    private void EnsureLogSafeZone(string safeZoneName, string visualName, Vector3 position, float yaw, Vector3 scale)
    {
        Transform safeZonesRoot = GetOrCreateSafeZonesRoot();
        Transform existing = safeZonesRoot.Find(safeZoneName);
        GameObject safeZoneObject;

        if (existing == null)
        {
            safeZoneObject = new GameObject(safeZoneName);
            safeZoneObject.transform.SetParent(safeZonesRoot, false);
        }
        else
        {
            safeZoneObject = existing.gameObject;
        }

        safeZoneObject.tag = "Safezone";
        safeZoneObject.transform.position = GetTerrainPosition(position);
        safeZoneObject.transform.rotation = Quaternion.identity;

        if (safeZoneObject.GetComponent<SphereCollider>() == null && safeZoneObject.GetComponent<BoxCollider>() == null)
        {
            SphereCollider trigger = safeZoneObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 3.5f;
        }

        if (safeZoneObject.GetComponent<SafeZone>() == null)
            safeZoneObject.AddComponent<SafeZone>();

        Transform visual = safeZoneObject.transform.Find(visualName);
        if (visual == null)
            visual = CreateLogVisual(visualName, safeZoneObject.transform);

        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.Euler(0f, yaw, 90f);
        visual.localScale = scale;

        Transform fallbackVisual = safeZoneObject.transform.Find($"{visualName}_GeneratedCylinder");
        if (fallbackVisual == null)
            fallbackVisual = CreateLogVisual($"{visualName}_GeneratedCylinder", safeZoneObject.transform);

        fallbackVisual.localPosition = Vector3.zero;
        fallbackVisual.localRotation = Quaternion.Euler(0f, yaw, 90f);
        fallbackVisual.localScale = scale;
    }

    private Transform GetOrCreateSafeZonesRoot()
    {
        GameObject rootObject = GameObject.Find(SafeZonesRootName);
        if (rootObject != null) return rootObject.transform;

        rootObject = new GameObject(SafeZonesRootName);
        return rootObject.transform;
    }

    private Vector3 GetTerrainPosition(Vector3 position)
    {
        if (terrain == null) return position;

        Vector3 terrainOrigin = terrain.transform.position;
        position.y = terrainOrigin.y + terrain.SampleHeight(position) + heightOffset;
        return position;
    }

    private Vector3 TerrainPoint(float normalizedX, float normalizedZ)
    {
        if (terrain == null)
            return new Vector3(normalizedX * 100f, 0f, normalizedZ * 100f);

        TerrainData terrainData = terrain.terrainData;
        Vector3 origin = terrain.transform.position;
        return new Vector3(
            origin.x + terrainData.size.x * normalizedX,
            0f,
            origin.z + terrainData.size.z * normalizedZ);
    }

    private Transform CreateLogVisual(string visualName, Transform parent)
    {
        GameObject visualObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visualObject.name = visualName;
        visualObject.transform.SetParent(parent, false);

        Collider visualCollider = visualObject.GetComponent<Collider>();
        if (visualCollider != null)
        {
            if (Application.isPlaying)
                Destroy(visualCollider);
            else
                DestroyImmediate(visualCollider);
        }

        Renderer renderer = visualObject.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = CreateLogMaterial();

        return visualObject.transform;
    }

    private Material CreateLogMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader)
        {
            name = "Generated Fallen Log Material"
        };

        Color color = new Color(0.34f, 0.2f, 0.1f, 1f);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        else
            material.color = color;

        return material;
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
